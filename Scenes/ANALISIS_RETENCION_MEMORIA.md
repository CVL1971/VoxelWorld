# Análisis: Retención de Memoria al Parar Unity

## Resumen

La memoria sigue retenida tras `OnDisable` / parar Play mode. Este documento recoge las hipótesis más probables y recomendaciones.

---

## Hipótesis ordenadas por probabilidad

### 1. **Tasks fire-and-forget sin espera (ALTA probabilidad)**

**Problema:** `Task.Run(() => ExecuteJob(vJob))` y `Task.Run(() => Execute(job))` son fire-and-forget. No se guarda el `Task` ni se espera a que termine.

**Efecto:** Al hacer `Shutdown()`:
- Se vacían colas y se libera el semáforo.
- Pero los Tasks que **ya estaban ejecutándose** siguen hasta completar.
- Cada Task captura en su closure: `Chunk`, `Grid` (vía `mGrid` en `RenderQueueAsync`), `MeshGenerator`, etc.
- Mientras el Task corre, mantiene vivo todo el grafo: Chunk → Grid → mChunks → Chunk[].

**Evidencia:** En `RenderQueueAsync.Execute()`:
```csharp
MeshData vData = vRequest.mMeshGenerator.Generate(
    vChunk,
    mGrid.mChunks,      // ← referencia a todo el array de chunks
    mGrid.mSizeInChunks
);
```
El Task mantiene `this` (RenderQueueAsync) → `mGrid` → `mChunks` (91×7×91 chunks).

**Solución propuesta:** Rastrear los Tasks activos y hacer `Task.WhenAll(...)` o equivalente antes de anular referencias en `Shutdown()`. O usar un `CountdownEvent` / barrera para esperar a que todos los workers terminen.

---

### 2. **Grid.mWorldRoot y GameObjects no destruidos explícitamente (MEDIA)**

**Problema:** `Grid.mWorldRoot` es un GameObject raíz con hijos (`Chunk.mViewGO`). En `OnDisable` se hace `mGrid = null` pero **no** se llama a `Object.Destroy(mGrid.mWorldRoot)`.

**Efecto:** 
- Si los Tasks siguen ejecutando, `mGrid` sigue referenciado y no se recolecta.
- Cuando todo termina, `mWorldRoot` y sus hijos pueden quedar en la escena hasta que Unity limpie al cambiar de escena o salir.
- En el Editor, al parar Play mode, Unity destruye la escena, pero el orden puede provocar referencias huérfanas temporales.

**Solución propuesta:** Antes de `mGrid = null`, destruir explícitamente:
```csharp
if (mGrid?.mWorldRoot != null)
{
    Object.Destroy(mGrid.mWorldRoot);
}
```

---

### 3. **Chunk.OnDestroy nunca se invoca (MEDIA)**

**Problema:** `Chunk` no es `MonoBehaviour`. `OnDestroy()` es un método custom que **nunca se llama** automáticamente.

**Efecto:** 
- `Chunk.OnDestroy()` pone `mSample0 = mSample1 = mSample2 = null` y destruye `mViewGO`.
- Si no se invoca, los arrays de densidad (35³ + 19³ + 11³ floats ≈ 50KB por chunk) permanecen hasta que el GC recolecte el Chunk.
- Con 91×7×91 ≈ 57.000 chunks, son varios GB de arrays.

**Solución propuesta:** En `Shutdown()`, antes de anular el pipeline, iterar sobre `mGrid.mChunks` y llamar explícitamente a `chunk.OnDestroy()` (o un método `Release()` que haga lo mismo). O asegurarse de que al destruir `mWorldRoot`, los Chunks se liberen cuando el Grid sea recolectado (el GC los recogerá, pero puede tardar).

---

### 4. **Thread Pool y delegados (MEDIA)**

**Problema:** El Thread Pool de .NET mantiene los delegados de los Tasks hasta que terminan. Los delegados capturan las closures con Chunk, Grid, etc.

**Efecto:** Mientras el Task está en cola o ejecutándose, el delegado mantiene las referencias. No hay forma de "cancelar" un Task ya en ejecución desde fuera (salvo `CancellationToken` dentro del método, que ya usamos parcialmente).

**Nota:** `Execute` y `ExecuteJob` hacen trabajo síncrono pesado (Sample, Generate). Un `CancellationToken` dentro de esos métodos permitiría abortar antes, pero `SDFGenerator.Sample` y `MeshGenerator.Generate` no aceptan token.

---

### 5. **Variables estáticas (BAJA–MEDIA)**

**Problema:** Varias clases tienen estáticos que pueden retener memoria:

| Clase | Estático | Riesgo |
|-------|----------|--------|
| `DensitySamplerQueueAsync` | `CustomSampler s_TaskSampler`, `s_MathSampler` | Bajo (samplers ligeros) |
| `VoxelArrayPool` | `ConcurrentStack mPool` | Se limpia con `Clear()` ✓ |
| `GeneralData` | `mVolumeData`, `mTerrain`, etc. | Si se usan, retienen |
| `SDFGenerator` | `Stopwatch watch` | Bajo |
| `RenderQueueAsync` | `sTryAddRejectCount`, etc. | Solo ints, sin impacto |

**Solución:** Si `GeneralData` se usa en este flujo, limpiar sus referencias en `OnDisable`.

---

### 6. **Unity Editor: dominio y orden de destrucción (BAJA)**

**Problema:** Al parar Play mode, Unity puede recargar el dominio de scripts. Si hay hilos/Tasks aún ejecutándose con referencias al dominio anterior, pueden:
- Retrasar la descarga del dominio.
- Provocar excepciones o comportamiento impredecible.

**Solución:** Asegurar que todos los Tasks terminen **antes** de que Unity proceda con la descarga. `Application.quitting` o `EditorApplication.quitting` se disparan antes que algunos `OnDisable`; podría ser necesario registrar un handler para cancelar y esperar de forma más temprana.

---

### 7. **ConcurrentDictionary / ConcurrentQueue (BAJA)**

**Problema:** `mInWait`, `mPendingResamples`, etc. pueden tener entradas con referencias a `Chunk`.

**Estado actual:** `ClearAll()` vacía las colas y hace `mInWait.Clear()`, `mPendingResamples.Clear()`. Las instancias de las colas viven en el pipeline; al hacer `mChunkPipeline = null`, dejan de ser accesibles. Si los Tasks ya terminaron, no hay referencias adicionales.

---

### 8. **Mesh no destruidos (BAJA)**

**Problema:** En `Apply()` se crea `new Mesh()` y se asigna a `MeshFilter.sharedMesh`. Si `Chunk.OnDestroy()` no se llama, las mallas antiguas podrían no destruirse explícitamente.

**Estado:** Al destruir `mWorldRoot`, Unity destruye los hijos y sus componentes. Los `MeshFilter` y sus `sharedMesh` deberían limpiarse. El riesgo es si `mWorldRoot` no se destruye explícitamente y queda huérfano.

---

## Recomendaciones prioritarias

1. **Esperar a que los Tasks terminen antes de Shutdown**
   - Añadir tracking de Tasks activos (p. ej. `List<Task>` o `CountdownEvent`).
   - En `Shutdown()`, esperar con timeout: `Task.WaitAll(tasks, TimeSpan.FromSeconds(2))`.
   - O usar un flag + `Thread.Sleep` en bucle hasta que `mWorkerRunning == 0` y no haya Tasks pendientes.

2. **Destruir mWorldRoot explícitamente**
   - En `OnDisable`, antes de `mGrid = null`: `Object.Destroy(mGrid?.mWorldRoot)`.

3. **Invocar liberación explícita de Chunks**
   - Si el Grid existe, iterar `mGrid.mChunks` y llamar a un método de liberación (p. ej. `chunk.Release()` que ponga arrays a null y destruya `mViewGO`).

4. **Añadir delay antes de GC.Collect**
   - Dar tiempo a que los Tasks terminen: `await Task.Delay(100)` o `Thread.Sleep(200)` antes de `GC.Collect()`.

5. **Diagnóstico**
   - Añadir logs en `Execute`/`ExecuteJob` al inicio: "Task started for chunk X".
   - En `Shutdown`, log "Shutdown called, waiting for tasks...".
   - Tras esperar, log "Shutdown complete".
   - Usar el Memory Profiler de Unity para ver qué objetos mantienen vivos a Chunk/Grid.

---

## Orden sugerido en OnDisable

```csharp
void OnDisable()
{
    s_AppIsRunning = false;
    mCTS?.Cancel();
    mCTS?.Dispose();
    mCTS = null;

    // 1. Pedir shutdown al pipeline (vacía colas, libera semáforos)
    mChunkPipeline?.Shutdown();

    // 2. ESPERAR a que los Tasks en ejecución terminen (nuevo)
    //    (requiere implementar tracking en las colas)
    mChunkPipeline?.WaitForPendingWork(TimeSpan.FromSeconds(2));

    // 3. Destruir GameObjects del mundo
    if (mGrid?.mWorldRoot != null)
        Object.Destroy(mGrid.mWorldRoot);

    // 4. Liberar chunks explícitamente (opcional, acelera GC)
    if (mGrid?.mChunks != null)
        foreach (var c in mGrid.mChunks)
            c?.Release(); // o OnDestroy()

    mChunkPipeline = null;
    mGrid = null;
    mVigilante = null;
    VoxelArrayPool.Clear();

    // 5. Dar tiempo al GC
    System.Threading.Thread.Sleep(100);
    System.GC.Collect();
    System.GC.WaitForPendingFinalizers();
    System.GC.Collect();
}
```

---

## Conclusión

La causa más probable es la **combinación de (1) Tasks fire-and-forget** que siguen ejecutándose tras `Shutdown` y mantienen referencias a Chunk/Grid, y **(2) ausencia de destrucción explícita** de `mWorldRoot` y de liberación de Chunks. Implementar espera a Tasks y limpieza explícita debería reducir de forma notable la retención de memoria.
