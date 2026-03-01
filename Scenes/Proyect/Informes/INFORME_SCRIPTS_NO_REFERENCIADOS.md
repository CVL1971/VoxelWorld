# Informe: Scripts no referenciados desde SDFWorld

**Punto de entrada:** `SDFWorld.cs` (clase `World`)  
**Fecha:** Análisis post-reorganización en carpetas

---

## Criterio

Se considera que un script **está referenciado** si existe una cadena de referencias (tipos, `new`, herencia, llamadas estáticas) que parte de `World` en `SDFWorld.cs`.

---

## Scripts referenciados (cadena desde World)

| Script | Ruta | Motivo |
|--------|------|--------|
| World | `Scenes/SDFWorld.cs` | Punto de entrada |
| VoxelUtils | `Scenes/VoxelUtils.cs` | Uso estático en World |
| Grid | `Scenes/Grid.cs` | `new Grid()` en World |
| ChunkPipeline | `Scenes/ChunkPipeline.cs` | `new ChunkPipeline()` en World |
| Chunk | `Scenes/Chunk.cs` | `mGrid.mChunks`, `ChunkPipeline`, etc. |
| VoxelBrush | `Scenes/VoxelBrush.cs` | `new VoxelBrush()` en ExecuteModification |
| Vigilante | `Scenes/Vigilante.cs` | `new Vigilante()` en World |
| SurfaceNetsGeneratorQEF3caches | `Scenes/Isosurfaces/SurfeceNetsGeneratorQEF3caches.cs` | `new` en World (mSurfaceNet, mSurfaceNetQEF) |
| MeshGenerator | `Scenes/MeshGenerator.cs` | Base de SurfaceNetsGeneratorQEF3caches |
| MeshData | `Scenes/MeshData.cs` | Usado por MeshGenerator, RenderQueueAsyncEpoch |
| DSFDensityGenerator (SDFGenerator) | `Scenes/DSFDensityGenerator.cs` | Llamado desde VoxelUtils, DensitySamplerQueueEpoch |
| RenderQueueAsyncEpoch | `Scenes/Scheduler/RenderQueueAsyncEpoch.cs` | Usado por ChunkPipeline |
| RenderQueueAsyncPlus | `Scenes/Scheduler/RenderQueueAsyncPlus.cs` | Define `RenderJobPlus` usado por RenderQueueAsyncEpoch |
| DensitySamplerQueueEpoch | `Scenes/Scheduler/DensitySamplerQueueEpoch.cs` | Usado por ChunkPipeline |
| DensitySamplerQueueAsync | `Scenes/Scheduler/DensitySamplerQueueAsync.cs` | Define `DensitySamplerJob` usado por DensitySamplerQueueEpoch |

---

## Scripts no referenciados (nunca usados desde World)

### Scenes (raíz)

| # | Script | Ruta |
|---|--------|------|
| 1 | ChunkRenderer | `Scenes/ChunkRenderer.cs` |
| 2 | CHANGESVEQ | `Scenes/CHANGESVEQ.cs` |
| 3 | DebugLinesManager | `Scenes/DebugLinesManager.cs` |
| 4 | Fpscamera | `Scenes/Fpscamera.cs` |
| 5 | GeneralData | `Scenes/GeneralData.cs` |
| 6 | GeneratorVNOEQ | `Scenes/GeneratorVNOEQ.cs` |
| 7 | HeightmapGenerator | `Scenes/HeightmapGenerator.cs` |
| 8 | MesherTest | `Scenes/MesherTest.cs` |
| 9 | PlayerFisico | `Scenes/PlayerFisico.cs` |
| 10 | SamplerQueue | `Scenes/SamplerQueue.cs` |
| 11 | Schematicreader | `Scenes/Schematicreader.cs` |
| 12 | TestVelocidad | `Scenes/TestVelocidad.cs` |
| 13 | versionworld | `Scenes/versionworld.cs` |
| 14 | VoxelAddressing | `Scenes/VoxelAddressing.cs` |
| 15 | VoxelArrayPool | `Scenes/VoxelArrayPool.cs` |
| 16 | VoxelCubeGeometry | `Scenes/VoxelCubeGeometry.cs` |
| 17 | VoxelData | `Scenes/VoxelData.cs` |
| 18 | VoxelFaceGeometry | `Scenes/VoxelFaceGeometry.cs` |

### Scenes/Informes

| # | Script | Ruta |
|---|--------|------|
| 19 | TODO | `Scenes/Informes/TODO.cs` |

### Scenes/Isosurfaces

| # | Script | Ruta |
|---|--------|------|
| 20 | DualContouringGenerator3caches | `Scenes/Isosurfaces/DualContouringGenerator3caches.cs` |
| 21 | SurfeceNetsGeneratorQEFOriginal | `Scenes/Isosurfaces/SurfeceNetsGeneratorQEFOriginal.cs` |
| 22 | SurfeceNetsGeneratorQEFOriginal2 | `Scenes/Isosurfaces/SurfeceNetsGeneratorQEFOriginal2.cs` |
| 23 | SurfaceNetsGenerator | `Scenes/Isosurfaces/SurfaceNetsGenerator.cs` |
| 24 | SurfaceNetsGeneratorQEF | `Scenes/Isosurfaces/SurfaceNetsGeneratorQEF.cs` |
| 25 | WeightedNeighborDensity | `Scenes/Isosurfaces/WeightedNeighborDensity.cs` |

### Scenes/Scheduler

| # | Script | Ruta |
|---|--------|------|
| 26 | RenderQueue | `Scenes/Scheduler/RenderQueue.cs` |
| 27 | RenderQueueAsync | `Scenes/Scheduler/RenderQueueAsync.cs` |
| 28 | RenderQueueAsyncEpoch2 | `Scenes/Scheduler/RenderQueueAsyncEpoch2.cs` |
| 29 | RenderQueueMono | `Scenes/Scheduler/RenderQueueMono.cs` |
| 30 | RenderStackAsync | `Scenes/Scheduler/RenderStackAsync.cs` |
| 31 | RenderStackSyncMono | `Scenes/Scheduler/RenderStackSyncMono.cs` |

### Scenes/iu

| # | Script | Ruta |
|---|--------|------|
| 32 | CpuMonitor | `Scenes/iu/CpuMonitor.cs` |

### fNbt (biblioteca NBT)

| # | Scripts | Ruta |
|---|---------|------|
| 33 | Todo el directorio fNbt (25 archivos) | `Scenes/fNbt/` |

Solo es usado por `Schematicreader.cs`, que no está referenciado desde World.

---

## Resumen

| Categoría | Cantidad |
|-----------|----------|
| Scripts referenciados desde World | 15 |
| Scripts no referenciados (excl. fNbt) | 32 |
| Archivos fNbt (transitivamente no referenciados) | 25 |

---

## Notas

1. **Fpscamera** y **PlayerFisico** usan `World` (FindObjectOfType, SerializeField), pero World no los referencia. Son componentes que llaman a `World.ExecuteModification`; si no están en la escena, no afectan al flujo desde World.

2. **Archivos comentados o solo documentación:** `versionworld.cs`, `CHANGESVEQ.cs`, `GeneratorVNOEQ.cs`, `TODO.cs` son principalmente comentarios o documentación.

3. **RenderQueueAsyncEpoch2.cs** está completamente comentado.

4. **Rutas:** Tras la reorganización, los scripts están en `Scenes/`, `Scenes/Scheduler/`, `Scenes/Isosurfaces/`, `Scenes/Informes/`, `Scenes/iu/`, `Scenes/fNbt/`. El `.csproj` se regenera automáticamente por Unity; si hay errores de ruta, abrir el proyecto en Unity para actualizar referencias.
