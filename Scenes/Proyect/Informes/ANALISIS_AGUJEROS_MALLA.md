# Análisis: Agujeros en la malla

**Síntoma:** La malla presenta agujeros (áreas sin geometría) en ciertas zonas. Los agujeros persisten aunque se desactiven filtros del sampler, se considere todo como superficie y se desactive el Vigilante.

---

## 1. Análisis previo de causas posibles

### 1.1. Chunks sin mViewGO (GameObject no asignado)

**Flujo:** `AdaptChunk` solo asigna `mViewGO` cuando `mBool1 && mBool2`. Si no, devuelve el ViewGO al pool y pone `mViewGO = null`.

**Efecto:** Esos chunks nunca se encolan para render (`EnqueueRender` solo si `mBool1 && mBool2`). No deberían producir agujeros si todo se considera superficie, salvo que haya otra ruta que encole render sin ViewGO.

### 1.2. Apply descarta cuando mViewGO es null

**Código (RenderQueueAsyncEpoch.Apply):**
```csharp
if (pChunk.mViewGO == null)
{
    mGrid.SetProcessing(index, false);
    return;  // No aplica la malla
}
```

**Causa posible:** Entre `EnqueueRender` y `Apply` el chunk pierde el ViewGO (p. ej. por `AdaptChunk` en otro resultado de densidad). El resultado de malla se descarta y el chunk queda sin geometría.

### 1.3. Malla vacía o degenerada

**Código:** Si `MeshData` tiene 0 vértices o triángulos inválidos, la malla se aplica pero no se ve.

**Causa posible:** El generador SurfaceNets devuelve mallas vacías para chunks que no cruzan la isosuperficie en ninguna celda. Con `mDCache` local, no debería haber huecos por vecinos, pero sí si la densidad no cruza el umbral en todo el chunk.

### 1.4. Desfase de coordenadas (WorldOrigin)

**Flujo:** `Chunk.WorldOrigin = mGlobalCoord * UNIVERSAL_CHUNK_SIZE`. Si `mGlobalCoord` está mal tras streaming, el chunk se dibuja en la posición incorrecta y puede verse como “agujero” en la posición esperada.

### 1.5. Streaming y reciclaje

**Flujo:** `ReassignChunk` cambia `mGlobalCoord`, desactiva el renderer y encola densidad. Hasta que termine el muestreo y el render, el chunk puede estar sin malla o en transición.

### 1.6. Índice de chunks en GetDensityGlobal (no aplica al generador actual)

**Nota:** `SurfaceNetsGeneratorQEF3caches` usa solo la caché local (`mDCache`), no `GetDensityGlobal`. Los generadores que sí usan `GetDensityGlobal` podrían indexar mal con streaming. El generador actual no entra en este caso.

### 1.7. Lógica de asignación de MeshFilter/GameObject

**Flujo:** `GameObjectPool.Get` crea o reutiliza un GameObject. `Apply` asigna `vMf.sharedMesh = vMesh`. Si el MeshFilter o el mesh se comparten mal entre chunks, podría haber conflictos.

---

## 2. MarkSurface: qué controla y qué sistemas dependen

### 2.1. Definición

```csharp
// Grid.cs
public void MarkSurface(Chunk pChunk)
{
    Surface(pChunk, pChunk.mBool1 && pChunk.mBool2);
}

public void Surface(Chunk pChunk, bool pValue)
{
    int pIndex = ChunkIndex(...);
    mStatusGrid[pIndex] = (ushort)((mStatusGrid[pIndex] & ~BIT_SURFACE) | vBitValue);
    SetLod(pIndex, lodIdx);
}
```

`MarkSurface` actualiza el bit `BIT_SURFACE` en `mStatusGrid` según `mBool1 && mBool2` y ajusta el LOD del chunk.

### 2.2. Dónde se llama

| Ubicación | Cuándo |
|-----------|--------|
| `ChunkPipeline.Update` (TryDequeueDensityResult) | Tras cada resultado de densidad |
| `SDFGenerator.Sample` (ruta normal) | Tras rellenar arrays y antes de AssignDCache/ReturnDCache |
| `SDFGenerator.Sample` (early exits aire/sólido) | Tras la corrección reciente |

### 2.3. Qué sistemas usan BIT_SURFACE

| Sistema | Uso |
|---------|-----|
| **Vigilante** | Solo procesa chunks con `(status & BIT_SURFACE) != 0` para LOD |
| **ResetChunkState** (RenderQueue, jobs estructurales) | Pone `Surface(index, false)` al resetear |
| **DebugState** (Grid) | Muestra el estado de superficie |

### 2.4. Qué no controla MarkSurface

- **AdaptChunk:** Usa `mBool1 && mBool2` directamente, no `BIT_SURFACE`.
- **EnqueueRender:** Se decide por `mBool1 && mBool2`, no por `BIT_SURFACE`.
- **Apply:** No consulta `BIT_SURFACE`.

Por tanto, con el Vigilante desactivado, `MarkSurface` no debería influir en los agujeros.

---

## 3. Visualización de chunks no dibujados

Se ha añadido en `OnDrawGizmos` la opción de marcar con un cuadro rojo los chunks que se consideran “no dibujados”:

- `mViewGO == null`
- `mViewGO != null` pero `MeshRenderer.enabled == false`
- `mViewGO != null` pero `MeshFilter.sharedMesh == null` o `vertexCount == 0`

Si los agujeros coinciden con esos cuadros, el problema está en la asignación de ViewGO o en la aplicación de la malla. Si no coinciden, el origen está en la geometría o en las coordenadas.

---

## 4. Hipótesis: chunk inferior faltante (depresiones)

**Observación:** Los agujeros en depresiones podrían deberse a que el **chunk inferior** (menor Y) no existe o no está dibujado. En las cimas de colinas el terreno es visible porque la montaña supera la resolución de altura.

**Visualización añadida:** Toggle `mDrawChunksWithMissingLowerNeighbor`. Cuando está activo, se marcan en **amarillo** los chunks de superficie cuyo vecino inferior (Y-1) es null o no está dibujado. Sirve para comprobar si los agujeros en depresiones coinciden con chunks que carecen de chunk inferior.
