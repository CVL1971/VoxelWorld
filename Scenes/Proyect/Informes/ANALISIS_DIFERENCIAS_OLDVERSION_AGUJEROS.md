# Análisis: Diferencias OldVersion vs Actual que pueden explicar agujeros en la geometría

**Nota:** Los agujeros los crea únicamente la versión **Surface Nets**. DualContouring no se modifica.

---

## Resumen ejecutivo

Se han identificado **varias diferencias significativas** entre la versión antigua (sin agujeros) y la actual. La **salida anticipada** (early exit) ya está desactivada mediante `DisableQuickEvaluate`. Surface Nets usa el rango correcto (celdas 0..size, consenso 2 caras por chunk).

---

## 1. GENERADOR DE DENSIDAD (DSFDensityGenerator / SDFGenerator)

### 1.1 Early exit sin rellenar datos (CRÍTICO) — **YA TRATADO**

| Aspecto | OldVersion | Actual |
|---------|------------|--------|
| Early exit (aire/sólido puro) | Llama a `SetChunkConstant()` y rellena los 3 arrays con 0 o 1 | `ReturnDCache()` y `return` — **no rellena nada** |
| Chunks aire/sólido | Siempre tienen datos de densidad | No tienen `mDCache` (nunca se asigna) |

**Estado actual:** La salida anticipada está **desactivada**. En `SDFGenerator.Sample` se usa `DisableQuickEvaluate(pChunk)` en lugar de `HeightFivePointStrategy(pChunk)`. `DisableQuickEvaluate` siempre devuelve `Unknown`, por lo que todos los chunks pasan por el muestreo completo y nunca se toma la rama de early exit.

### 1.2 Estructura de almacenamiento

| Aspecto | OldVersion | Actual |
|---------|------------|--------|
| Arrays de densidad | `Chunk.mSample0/1/2` (arrays propios del chunk) | `Chunk.mDCache` (ArrayPool.DCache compartido) |
| Asignación | Siempre (arrays en el chunk) | Solo si `isSurface` → `AssignDCache()` |
| Early exit | `SetChunkConstant()` escribe en los arrays del chunk | No hay escritura; el chunk queda sin cache (early exit desactivado) |

### 1.3 Terreno generado

| Aspecto | OldVersion | Actual |
|---------|------------|--------|
| `GetGeneratedHeight` | Onda seno simple: `(sx + sz) * 0.5f * AMPLITUDE` | Perlin + warp + montañas + valles + detalle |
| Complejidad | Terreno muy suave | Terreno más irregular |

---

## 2. SURFACE NETS

Surface Nets usa celdas 0..size, vmap (size+1)³ y EmitCorrectFaces. El consenso es 2 caras por chunk; no añadir padding -1..size (provoca geometría duplicada, superpuesta y z-fighting).

---

## 3. CHUNK Y PIPELINE

### 3.1 ChunkPipeline.Update — Encolado de render

| Aspecto | OldVersion | Actual |
|---------|------------|--------|
| Tras `TryDequeueDensityResult` | `EnqueueRender(densityChunk, mMeshGenerator)` — **siempre** | `if (densityChunk.mBool1 && densityChunk.mBool2) EnqueueRender(...)` — **solo si superficie** |

### 3.2 Inicialización (InitWorld)

| Aspecto | OldVersion | Actual |
|---------|------------|--------|
| PrepareView | `vChunk.PrepareView(...)` para cada chunk | No se llama; se usa `GameObjectPool` |
| Redim inicial | No explícito | `vChunk.Redim(VoxelUtils.LOD_DATA[VoxelUtils.GetInfoLod(2)])` |

### 3.3 DensitySamplerQueueEpoch — Jobs estructurales — **NO CAUSANTE**

Los jobs estructurales ahora llaman a `SDFGenerator.Sample(chunk)` antes de emitir. Tras aplicar el cambio, los agujeros permanecen → **no es la causa** de los agujeros.

---

## 4. OTRAS DIFERENCIAS

### 4.1 Chunk.GetDensity / SetDensity

- **OldVersion:** Usa `GetActiveCache()` que devuelve `mSample0/1/2` según `mSize`.
- **Actual:** Usa `mActiveCache`, que puede ser `null` si `mDCache == null`.

### 4.2 Chunk.Redim

- **Actual:** Si `mDCache == null`, pone `mActiveCache = null`.

---

## 5. PRIORIDADES DE INVESTIGACIÓN

### Descartadas
1. ~~**Early exit en DSFDensityGenerator:**~~ **Resuelto:** Desactivado con `DisableQuickEvaluate`.
2. ~~**Jobs estructurales:**~~ **No causante:** Aplicado Sample en ruta estructural; agujeros permanecen.

### Nuevas prioridades a evaluar
3. ~~**AdaptChunk + Apply:**~~ **Resuelto:** AdaptChunk desactivado. Todos los GameObjects se crean en InitWorld; `mViewGO` asignado a cada chunk al inicio. Apply ya no puede descartar por `mViewGO == null`.
4. **Clasificación mBool1 && mBool2:** Chunks con superficie que no cruzan el umbral en el rango interior (x,y,z en 1..N) podrían quedar sin `isSurface`. Revisar si el muestreo marca correctamente ambos bools en bordes.
5. **AssignDCache solo si isSurface:** Chunks que muestrean pero no cumplen `mBool1 && mBool2` no reciben `AssignDCache`. Si se encolaran para render por otra ruta, tendrían `mDCache == null` y malla vacía.
6. **GameObjectPool / reciclaje:** Posible reutilización incorrecta de GameObjects o meshes entre chunks (compartir mesh, posición equivocada).
7. **Streaming y mGlobalCoord:** Chunks reciclados con `ReassignChunk`; verificar que `mGlobalCoord` y `WorldOrigin` son correctos antes de mallar.
8. **Chunk inferior faltante (depresiones):** Hipótesis de ANALISIS_AGUJEROS_MALLA: agujeros en depresiones cuando el vecino Y-1 no existe o no está dibujado. Usar `mDrawChunksWithMissingLowerNeighbor` para comprobar.

---

## 6. ACCIONES SUGERIDAS

1. ~~Desactivar el early exit~~ **Hecho:** Se usa `DisableQuickEvaluate`.
2. ~~Jobs estructurales~~ **No causante:** Cambio aplicado sin efecto en los agujeros.
3. **Comprobar condición de carrera AdaptChunk/Apply:** Añadir logs o breakpoints para ver si chunks pierden mViewGO entre EnqueueRender y Apply.
4. **Validar clasificación isSurface:** Revisar en qué chunks `mBool1 && mBool2` es false pero hay geometría esperada.
5. **Probar mDrawChunksWithMissingLowerNeighbor:** Ver si los agujeros coinciden con chunks sin vecino inferior.
