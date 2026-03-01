# Auditoría de Memoria - Resultados

## 1. Native Memory Check

**Búsqueda realizada:** `Marshal.AllocHGlobal`, `GCHandle.Alloc`, `GCHandle.`, `Pinned`

**Resultado:** No se encontró ningún uso de:
- `Marshal.AllocHGlobal`
- `GCHandle.Alloc` (arrays fijados en memoria)
- Ningún patrón de "Pinned" que fije memoria para hilos nativos

**Conclusión:** El GC puede mover y recolectar toda la memoria gestionada. No hay arrays fijados que impidan la recolección.

---

## 2. Script de Auditoría de Referencias

**Ubicación:** `MemoryAudit.cs`

**Limitación importante:** `Resources.FindObjectsOfTypeAll<T>()` solo funciona con tipos que heredan de `UnityEngine.Object`. `Chunk` es una clase C# pura, por tanto **no puede encontrarse** con FindObjectsOfTypeAll.

**Solución implementada:**
- Contador estático `Chunk.s_AliveCount` (incremento en constructor, decremento en `Release`/`OnDestroy`)
- `MemoryAudit.RunAudit()` muestra:
  - `Chunk.s_AliveCount` — conteo exacto de instancias Chunk vivas
  - `Mesh` (FindObjectsOfTypeAll) — meshes en memoria (Editor)
  - `GameObject 'Chunk_*'` — GameObjects de chunks (Editor)
  - `GameObject 'WorldRoot'` — raíz del mundo (Editor)

**Cuándo se ejecuta:**
1. Automáticamente al final de `World.OnDisable` (post-Shutdown)
2. Manualmente: menú **Herramientas > Auditoría de Memoria (Memory Audit)**

---

## 3. Prueba de Escena Vacía

**Procedimiento:**
1. Abre una escena nueva vacía → anota RAM
2. Vuelve a tu escena → Play → Stop
3. Si la RAM no vuelve al nivel de la escena vacía → probable fuga por **objeto static** con referencia

**Objetos static a revisar si hay fuga:**
- `World.s_AppIsRunning` (bool, no retiene)
- `Chunk.s_AliveCountValue` (int, no retiene)
- `VoxelArrayPool.mPool` (se limpia con `Clear()`)
- `GeneralData` (mVolumeData, mTerrain, etc.)
- `DensitySamplerQueueAsync.s_TaskSampler`, `s_MathSampler` (CustomSampler)
- `RenderQueueAsync` contadores estáticos (solo ints)
