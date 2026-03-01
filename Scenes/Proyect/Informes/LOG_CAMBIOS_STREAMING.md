# Log de Cambios Aplicados para Solución de Problemas de Streaming

## Resumen Ejecutivo

Este documento registra todos los cambios de código realizados para corregir los problemas de streaming en el eje X, incluyendo el código añadido y eliminado, el diagnóstico detallado de las líneas en blanco persistentes, y el análisis de la caída de rendimiento.

---

## 1. REGISTRO DE CAMBIOS POR ARCHIVO

### 1.1 DensitySamplerQueueAsync.cs

#### Código AÑADIDO

```csharp
// En DensitySamplerJob - nuevo campo
public int mGenerationIdAtEnqueue;

// En constructor DensitySamplerJob
mGenerationIdAtEnqueue = pChunk.mGenerationId;

// Cambio de tipo de cola
public ConcurrentQueue<(Chunk chunk, int generationId)> DensitySamplerResult = new();

// Nuevo método ForceEnqueue
/// <summary>
/// Fuerza re-encolado aunque el chunk esté en mInWait (p. ej. tras ReassignChunk por streaming).
/// Evita la franja sin geometría cuando se recicla una capa antes de que termine el sampleo anterior.
/// </summary>
public void ForceEnqueue(Chunk pChunk)
{
    if (pChunk == null) return;

    mInWait.TryRemove(pChunk, out _);
    if (mInWait.TryAdd(pChunk, 0))
    {
        mQueue.Enqueue(new DensitySamplerJob(pChunk));
    }
    StartWorker();
}

// En ThreadEntry - validación y nuevo formato de resultado
if (vChunk.mGenerationId == vJob.mGenerationIdAtEnqueue)
    DensitySamplerResult.Enqueue((vChunk, vJob.mGenerationIdAtEnqueue));
```

#### Código ELIMINADO / REEMPLAZADO

```csharp
// ANTES
public ConcurrentQueue<Chunk> DensitySamplerResult = new();
// ...
DensitySamplerResult.Enqueue(vChunk);
```

---

### 1.2 Grid.cs

#### Código AÑADIDO

```csharp
// En ReassignChunk
chunk.ClearMesh(); // Evita mostrar geometría vieja en la nueva posición

// Reemplazo de Enqueue por ForceEnqueue
samplerQueue.ForceEnqueue(chunk);

// Nuevo método GetChunkByGlobalCoord
/// <summary>
/// Obtiene el chunk en la ventana activa por coordenadas globales.
/// Necesario para streaming: las coords globales pueden estar fuera de [0, size-1].
/// </summary>
public Chunk GetChunkByGlobalCoord(int cx, int cy, int cz)
{
    if (cx < mActiveMin.x || cx > mActiveMax.x ||
        cy < mActiveMin.y || cy > mActiveMax.y ||
        cz < mActiveMin.z || cz > mActiveMax.z)
        return null;

    int lx = cx - mActiveMin.x;
    int ly = cy - mActiveMin.y;
    int lz = cz - mActiveMin.z;
    return mChunks[ChunkIndex(lx, ly, lz)];
}
```

#### Código REEMPLAZADO (UpdateStreamingX - reciclado múltiple)

```csharp
// ANTES (una sola capa por llamada)
if (deltaX > 0)
{
    int outgoingX = mActiveMin.x;
    int incomingX = newMax.x;
    RecycleLayerX(outgoingX, incomingX, samplerQueue);
}
else
{
    int outgoingX = mActiveMax.x;
    int incomingX = newMin.x;
    RecycleLayerX(outgoingX, incomingX, samplerQueue);
}
mActiveMin = newMin;
mActiveMax = newMax;

// DESPUÉS (múltiples capas si |deltaX| > 1)
if (deltaX > 0)
{
    for (int i = 0; i < deltaX; i++)
    {
        int outgoingX = mActiveMin.x;
        int incomingX = mActiveMax.x + 1;
        RecycleLayerX(outgoingX, incomingX, samplerQueue);
        mActiveMin = new Vector3Int(mActiveMin.x + 1, mActiveMin.y, mActiveMin.z);
        mActiveMax = new Vector3Int(mActiveMax.x + 1, mActiveMax.y, mActiveMax.z);
    }
}
else
{
    int absDelta = -deltaX;
    for (int i = 0; i < absDelta; i++)
    {
        int outgoingX = mActiveMax.x;
        int incomingX = mActiveMin.x - 1;
        RecycleLayerX(outgoingX, incomingX, samplerQueue);
        mActiveMin = new Vector3Int(mActiveMin.x - 1, mActiveMin.y, mActiveMin.z);
        mActiveMax = new Vector3Int(mActiveMax.x - 1, mActiveMax.y, mActiveMax.z);
    }
}
mActiveMin = newMin;
mActiveMax = newMax;
```

#### Código REEMPLAZADO (ModifyWorld)

```csharp
// ANTES
if (!VoxelUtils.IsInBounds(targetCx, targetCy, targetCz, mSizeInChunks))
    continue;
int vCIdx = VoxelUtils.GetChunkIndex(targetCx, targetCy, targetCz, mSizeInChunks);
Chunk vTargetChunk = mChunks[vCIdx];
// ...
vAffectedChunks.Add(vCIdx);

// DESPUÉS
Chunk vTargetChunk = GetChunkByGlobalCoord(targetCx, targetCy, targetCz);
if (vTargetChunk == null)
    continue;
// ...
vAffectedChunks.Add(vTargetChunk.mIndex);
```

---

### 1.3 Chunk.cs

#### Código AÑADIDO

```csharp
/// <summary>
/// Limpia la malla visual. Necesario al reciclar: evita mostrar geometría de la posición anterior.
/// </summary>
public void ClearMesh()
{
    if (mViewGO == null) return;
    MeshFilter mf = mViewGO.GetComponent<MeshFilter>();
    if (mf != null && mf.sharedMesh != null)
    {
        Object.Destroy(mf.sharedMesh);
        mf.sharedMesh = null;
    }
}
```

---

### 1.4 RenderQueueAsync.cs

#### Código AÑADIDO / REEMPLAZADO

```csharp
// Cambio de tipo de cola
public ConcurrentQueue<(Chunk chunk, MeshData mesh, int generationId)> mResultsLOD = new();

// En Execute - captura y validación
int genIdAtStart = vChunk.mGenerationId;
// ...
if (vData != null && vChunk.mGenerationId == genIdAtStart)
    mResultsLOD.Enqueue((vChunk, vData, genIdAtStart));

// En Apply - nueva firma y validación
public void Apply(Chunk pChunk, MeshData pData, int expectedGenerationId)
{
    if (pChunk.mViewGO == null) return;
    if (pChunk.mGenerationId != expectedGenerationId)
    {
        mGrid.SetProcessing(pChunk.mIndex, false); // Desbloquear aunque descartemos
        return;
    }
    // ... resto igual
}
```

#### Código ELIMINADO / REEMPLAZADO

```csharp
// ANTES
public ConcurrentQueue<KeyValuePair<Chunk, MeshData>> mResultsLOD = new();
// ...
mResultsLOD.Enqueue(new KeyValuePair<Chunk, MeshData>(vChunk, vData));
// ...
public void Apply(Chunk pChunk, MeshData pData)
```

---

### 1.5 SDFWorld.cs

#### Código AÑADIDO

```csharp
// En Update - streaming
// 0.5. Streaming en eje X
if (mGrid.TryGetNewPlayerChunk(mCamera.transform.position, out Vector3Int newChunk))
    mGrid.UpdateStreamingX(newChunk, mDensitySampler);

// Procesamiento de DensitySamplerResult con validación
if (r.chunk.mGenerationId != r.generationId) continue; // Resultado obsoleto por streaming
mGrid.MarkSurface(r.chunk);
mRenderQueueAsync.Enqueue(r.chunk, mMeshGenerator);

// Procesamiento de mResultsLOD con nueva firma
mRenderQueueAsync.Apply(r.chunk, r.mesh, r.generationId);
```

#### Código REEMPLAZADO

```csharp
// ANTES
while (mDensitySampler.DensitySamplerResult.TryDequeue(out var r))
{
    mGrid.MarkSurface(r);
    mRenderQueueAsync.Enqueue(r, mMeshGenerator);
}
while (mRenderQueueAsync.mResultsLOD.TryDequeue(out var r))
    mRenderQueueAsync.Apply(r.Key, r.Value);
```

---

## 2. DIAGNÓSTICO DETALLADO: LÍNEAS EN BLANCO PERSISTENTES

### 2.1 Causa raíz principal: ClearMesh + latencia del pipeline

**Mecanismo:**
1. Al reciclar un chunk, `ReassignChunk` llama a `chunk.ClearMesh()`.
2. El chunk queda **sin malla** hasta que el pipeline (Sampler → Render) termine.
3. La ventana temporal entre "ClearMesh" y "Apply(malla correcta)" es visible como **línea/franja en blanco**.

**Por qué persiste:**
- El pipeline es **asíncrono** y tiene latencia inherente.
- Cada chunk reciclado debe: (1) Samplear 3 resoluciones LOD, (2) Generar malla, (3) Aplicar.
- Con `maxParallel = 10` en ambas colas, 256 chunks por capa implican ~26 "oleadas" de trabajo.
- A velocidad alta, se reciclan más capas por segundo de las que el pipeline puede procesar.

### 2.2 Causa secundaria: Descarte de resultados por mGenerationId

**Mecanismo:**
1. Si un chunk se recicla **durante** el sampleo o la generación de malla, el resultado se descarta.
2. El chunk queda sin malla y depende de un **nuevo** job (ForceEnqueue).
3. A velocidad alta, múltiples reciclados pueden provocar cadenas de descartes: el chunk nunca recibe una malla aplicada a tiempo.

**Flujo problemático:**
```
Frame 1: Reciclar chunk A (genId=2) → ForceEnqueue
Frame 2: Job sampler completa → resultado (genId=2) → OK, encolar render
Frame 3: Jugador acelera → Reciclar chunk A de nuevo (genId=3) → ClearMesh
Frame 4: Job render completa → resultado (genId=2) → DESCARTAR (chunk tiene genId=3)
         Chunk A queda sin malla. Nuevo job sampler (genId=3) aún en cola.
```

### 2.3 Causa terciaria: Cuello de botella en SDFGenerator.Sample

**Mecanismo:**
- `SDFGenerator.Sample(Chunk)` rellena **3 arrays** (mSample0, mSample1, mSample2) por chunk.
- Resoluciones: (32+3)³ + (16+3)³ + (8+3)³ ≈ 42.875 + 6.859 + 1.331 ≈ **51.000 evaluaciones** por chunk.
- Con 10 workers en paralelo, 256 chunks por capa → tiempo de procesamiento elevado.
- Las "líneas en blanco" son literalmente chunks que aún no han pasado por el sampler.

### 2.4 Causa cuaternaria: Debug.Log en ReassignChunk

**Mecanismo:**
- `Debug.Log(DebugState(chunk))` se ejecuta **256 veces** por capa reciclada.
- En Unity, `Debug.Log` es costoso (formateo, cola de mensajes, consola).
- Puede añadir latencia perceptible y contribuir a que el pipeline no drene a tiempo.

### 2.5 Información adicional: patrón determinista (no es race condition)

**Observación del usuario:**
- La geometría que queda **por detrás** de la línea va desapareciendo.
- Esa geometría queda fuera de ventana y va apareciendo **delante** de la línea.
- El proceso sigue hasta que la línea por detrás ha quedado **completamente fuera de la ventana**.
- **En ese instante exacto** (independientemente de la velocidad) la línea reaparece de nuevo en la frontera entrante.
- No es un race condition: es un **error de sincronización** que ocurre a **exactamente las mismas distancias**.
- El patrón es **perfecto y repetible**.

**Diagnóstico:**

La "línea" y la "geometría que desaparece por detrás" son **los mismos chunks**. El flujo es:

1. **Ventana activa:** `mActiveMin.x` a `mActiveMax.x` (p. ej. 16 chunks de ancho).
2. **Avance:** Al mover +X, la ventana se desplaza. Los chunks en el borde izquierdo (`mActiveMin.x`) van **saliendo** de la ventana.
3. **Momento del reciclado:** Cuando el jugador cruza un chunk boundary, `UpdateStreamingX` recicla la capa en `outgoingX = mActiveMin.x`.
4. **ReassignChunk** hace: `ClearMesh()` → actualiza `transform.position` al frente (`incomingX`).
5. **Efecto visual:** La geometría "por detrás" (los chunks reciclados) **desaparece** porque `ClearMesh` borra su malla. Esos mismos chunks **reaparecen** en la frontera entrante, pero **sin malla** hasta que el pipeline termine.

**Por qué el patrón es perfecto y repetible:**

- El reciclado ocurre en **límites exactos de chunk** (cada 32 voxels en X).
- Con grid 16×16×16, cada 16 chunks de avance se completa un "ciclo": los mismos slots físicos se reciclan.
- La "línea" aparece siempre en la **frontera entrante** = la capa de chunks recién reciclados.
- La "línea por detrás" que sale de la ventana = la capa que está a punto de reciclarse.
- No hay aleatoriedad: el momento en que la línea por detrás sale de la ventana **es** el momento del reciclado.

**Conclusión:** No es un race condition: es un **comportamiento determinista** del diseño actual. La "línea en blanco" es la capa de chunks reciclados que se muestran vacíos (`ClearMesh`) hasta que el pipeline (Sampler → Render) los rellena. La solución pasa por reducir la ventana visible de chunks vacíos (p. ej. sampleo lazy, más paralelismo, placeholder) o por cambiar el momento en que se hace visible el chunk reciclado.

### 2.6 Resumen de causas y soluciones propuestas

| Causa | Severidad | Solución propuesta |
|-------|-----------|-------------------|
| ClearMesh + latencia pipeline | Alta | Reducir latencia: sampleo lazy (solo LOD actual), aumentar paralelismo, o mostrar placeholder (cubo wireframe) |
| Descarte por genId en cascada | Media | Throttling de reciclado: no reciclar si cola sampler > umbral |
| Cuello SDFGenerator (3 LOD) | Alta | Samplear solo la resolución actual (mSize) en vez de las 3 |
| Debug.Log por chunk | Baja | Eliminar o condicionar a `#if UNITY_EDITOR` / flag debug |

---

## 3. ANÁLISIS DE RENDIMIENTO: CAÍDA BRUSCA CON STREAMING

### 3.1 Comparación: mundo estático vs streaming

| Aspecto | Mundo 64×4×64 (estático) | Streaming (16×16×16) |
|---------|--------------------------|----------------------|
| Carga inicial | 16.384 chunks en una sola oleada | 4.096 chunks en InitWorld |
| Patrón de trabajo | Burst único, luego inactividad | Oleadas continuas de 256 chunks por cruce de chunk |
| Overhead por frame | Ninguno (salvo Vigilante LOD) | TryGetNewPlayerChunk + posible UpdateStreamingX |
| Iteración RecycleLayerX | No aplica | Recorre 4.096 chunks para encontrar 256 |

### 3.2 Causas identificadas de la caída de rendimiento

#### A) Sobrecarga de sampleo redundante

- **Antes:** Cada chunk se sampleaba una vez en InitWorld. El SDF es determinista; no hay re-sampleo.
- **Ahora:** Cada chunk reciclado se samplea de nuevo. `SDFGenerator.Sample` rellena **3 resoluciones** (32, 16, 8) aunque el chunk solo use una en ese momento.
- **Impacto:** ~51.000 evaluaciones de densidad por chunk reciclado. 256 chunks × 51.000 ≈ **13 millones** de evaluaciones por capa.

#### B) Arquitectura de colas asíncronas

- **ProcessLoop** usa `async/await` + `Task.Run` para cada job.
- Cada job implica: `WaitAsync` (semáforo), `Task.Run`, sincronización, `TryRemove` de `mInWait`.
- El coste de scheduling y context switching puede ser significativo con cientos de jobs.

#### C) Semáforos limitantes

- `DensitySamplerQueueAsync` y `RenderQueueAsync` usan `maxParallel = 10`.
- Con 256 chunks por capa, solo 10 se procesan a la vez en cada etapa.
- En un mundo 64×4×64, la carga inicial podía usar más paralelismo efectivo (por ejemplo, si el bucle era más directo).

#### D) Iteración completa en RecycleLayerX

- `RecycleLayerX` recorre **todos** los chunks (`mChunks.Length`) para filtrar por `mGlobalCoord.x == outgoingX`.
- Con 4.096 chunks, son 4.096 comparaciones por capa reciclada.
- Solución: indexar por `mGlobalCoord.x` o mantener listas por capa X.

#### E) Debug.Log en producción

- 256 logs por capa reciclada generan I/O y formateo de strings.
- En builds de desarrollo puede degradar FPS de forma notable.

#### F) ClearMesh y Object.Destroy

- `ClearMesh` llama a `Object.Destroy(sharedMesh)` por chunk reciclado.
- En Unity, `Destroy` es diferido (end-of-frame) pero añade trabajo al main thread.
- 256 destroys por capa pueden sumar coste perceptible.

### 3.3 Resumen de causas de rendimiento

| Causa | Impacto estimado | Mitigación |
|-------|------------------|------------|
| Sampleo de 3 LOD por chunk | Muy alto | Samplear solo LOD actual |
| maxParallel = 10 | Alto | Aumentar a 16–32 si hay CPU disponible |
| RecycleLayerX O(n) completo | Medio | Indexar chunks por X |
| Debug.Log × 256 | Medio | Eliminar en producción |
| Overhead async/Task | Medio | Evaluar procesamiento por lotes síncronos |
| ClearMesh + Destroy | Bajo–medio | Pool de meshes o desactivar en vez de destruir |

---

## 4. RECOMENDACIONES PRIORITARIAS

### 4.1 Para líneas en blanco

| # | Recomendación | Severidad | Acción concreta |
|---|---------------|-----------|-----------------|
| 1 | **Samplear solo LOD actual** | Alta | En `SDFGenerator.Sample(Chunk)`, reemplazar el bucle sobre 3 resoluciones por sampleo únicamente de la resolución que usa `pChunk.mSize` en ese momento. |
| 2 | **Quitar o condicionar Debug.Log** | Baja | Eliminar `Debug.Log(DebugState(chunk))` en `ReassignChunk`, o envolver en `#if UNITY_EDITOR` / flag `mDebugStreaming` desactivado por defecto. |
| 3 | **Throttling de reciclado** | Media | En `UpdateStreamingX`, no reciclar nueva capa si `samplerQueue.mQueue.Count > 512` (o umbral configurable). Retornar sin actualizar `mCenterChunk` hasta que la cola drene. |
| 4 | **Placeholder visual** | Media | En vez de `ClearMesh()` puro, mostrar un cubo wireframe o malla placeholder hasta que llegue la malla definitiva (reduce sensación de “agujero”). |
| 5 | **Reducir latencia del pipeline** | Alta | Combinar sampleo lazy (1) con mayor paralelismo (4.2.2) para que los chunks reciclados completen el pipeline más rápido. |

### 4.2 Para rendimiento

| # | Recomendación | Impacto | Acción concreta |
|---|---------------|---------|-----------------|
| 1 | **Sampleo lazy (solo LOD actual)** | Muy alto | Mismo cambio que 4.1.1: en `SDFGenerator.Sample(Chunk)` samplear solo la resolución activa. Reduce ~51.000 a ~17.000 evaluaciones por chunk. |
| 2 | **Aumentar paralelismo** | Alto | Subir `maxParallel` en `DensitySamplerQueueAsync` y `RenderQueueAsync` de 10 a 20–32 según núcleos disponibles. |
| 3 | **Indexar chunks por X** | Medio | Mantener `Dictionary<int, List<Chunk>>` o array de listas por `mGlobalCoord.x` para que `RecycleLayerX` acceda en O(1) a los chunks de la capa en vez de iterar 4.096. |
| 4 | **Eliminar Debug.Log en producción** | Medio | Mismo cambio que 4.1.2. Evita 256 logs por capa reciclada. |
| 5 | **Evaluar procesamiento por lotes** | Medio | Valorar procesamiento síncrono por lotes (p. ej. 20 chunks por frame) en vez de `Task.Run` por job para reducir overhead de scheduling. |
| 6 | **Pool de meshes / desactivar en vez de destruir** | Bajo–medio | En `ClearMesh`, en lugar de `Object.Destroy(sharedMesh)`, desactivar el `MeshRenderer` o reutilizar meshes vacías desde un pool para reducir trabajo en el main thread. |

### 4.3 Orden de implementación sugerido

1. **Quick wins (bajo esfuerzo):** 4.1.2, 4.2.4 (quitar Debug.Log).
2. **Alto impacto:** 4.1.1, 4.2.1 (sampleo lazy).
3. **Siguiente:** 4.2.2 (aumentar paralelismo), 4.1.3 (throttling).
4. **Optimizaciones estructurales:** 4.2.3 (indexar por X), 4.2.5 (lotes), 4.2.6 (pool meshes).

---

*Documento generado como registro de cambios para solución de problemas de streaming.*
