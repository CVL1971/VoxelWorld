# Diagnóstico del proyecto VoxelWorld

**Fecha:** 13 Feb 2025  
**Alcance:** Estructura, arquitectura, estado actual y puntos a revisar.

---

## 1. Resumen ejecutivo

**VoxelWorld** es un proyecto Unity (URP) que implementa un **terreno voxel procedural** con:

- **SDF (Signed Distance Field)** para terreno generado por ruido (Perlin, warp, montañas).
- **Chunks** de tamaño fijo (32 unidades físicas) con resolución variable (LOD: 32, 16 u 8 voxels por eje).
- **Surface Nets** (y variante QEF) para generar mallas suaves a partir de densidades.
- **Cola de renderizado monohilo** con procesamiento gradual opcional por frame.
- **Sistema LOD** con “Vigilante” (detecta distancia a cámara) y “DecimationManager” (encola chunks para remesh).
- **Edición en tiempo real** con pincel (VoxelBrush) que modifica densidades y vuelve a generar mallas de chunks afectados.

El código está en `Assets/Scenes/` (scripts de juego y librería fNbt para NBT). La arquitectura es coherente; hay algunos bugs conocidos y una herramienta de diagnóstico desactualizada que ya está corregida.

---

## 2. Estructura del proyecto

```
Assets/
├── Scenes/                    # Scripts principales del voxel world
│   ├── SDFWorld.cs            # MonoBehaviour "World" – orquestador
│   ├── Grid.cs                # Malla de chunks, modificaciones, SDF inicial
│   ├── Chunk.cs               # Datos voxel (mVoxels), LOD, vista (mViewGO)
│   ├── DSFDensityGenerator.cs # SDFGenerator – ruido y Sample(Chunk)
│   ├── RenderQueueMonohilo.cs # Cola de generación de mallas + Apply
│   ├── Vigilante.cs           # Task async – distancia cámara → mTargetSize
│   ├── DecimationManager.cs   # Dispatch a cola + debug visual por LOD
│   ├── VoxelUtils.cs          # Constantes, GetDensityGlobal, LOD_DATA, índices
│   ├── VoxelData.cs / VoxelArrayPool.cs
│   ├── VoxelBrush.cs          # Edición (excavar/rellenar)
│   ├── MeshGenerator.cs      # Interfaz Generate(Chunk, Chunk[], size)
│   ├── MeshData.cs
│   ├── SurfaceNetsGenerator.cs / SurfaceNetsGeneratorQEF.cs
│   ├── ChunkDiagnosticTool.cs # Auditoría chunks vs GameObjects (corregido)
│   ├── fNbt/                  # Lectura/escritura NBT (ej. Schematicreader)
│   └── ... (Fpscamera, PlayerFisico, DebugLinesManager, etc.)
├── Settings/                  # URP, renderers, volumen
├── TutorialInfo/              # Readme URP
└── InputSystem_Actions.inputactions
```

- **Unity:** URP, Input System.  
- **Origen de verdad del terreno:** `SDFGenerator` (ruido); los arrays `mVoxels` son caché por chunk.

---

## 3. Flujo de datos (arquitectura)

1. **Inicio (`World.Start`)**  
   - Crea `Grid` (ej. 8×2×8 chunks), `RenderQueueMonohilo`, `Grid.ReadFromSDFGenerator()` (llena todos los chunks con `SDFGenerator.Sample`).  
   - `BuildSurfaceNets()`: crea GameObjects `Chunk_<coord>`, encola todos los chunks, `ProcessSequential()`, luego aplica resultados en `Update`.  
   - Inicializa `DecimationManager` y `Vigilante`; arranca `Task.Run(() => mVigilante.Run(cts.Token))`.

2. **Cada frame (`World.Update`)**  
   - Actualiza `mVigilante.vCurrentCamPos` con la cámara.  
   - Aplica hasta 8 resultados de la cola (`mResults`) con `RenderQueueMonohilo.Apply`.  
   - Si hay suficientes ítems en cola o ha pasado `mMaxWaitTime`, mueve la cola a `mProcessingBuffer` y en los siguientes frames procesa gradualmente (resample LOD si `mTargetSize > 0`, luego `Generate`, encola en `mResults`), con límite de tiempo por frame.

3. **Vigilante (async)**  
   - Cada 500 ms recorre todos los chunks, calcula distancia al centro del chunk, `GetInfoDist` → resolución deseada. Si la resolución actual (derivada de `mVoxels.Length`) difiere y `mTargetSize == 0`, asigna `mTargetSize` y llama `DecimationManager.DispatchToRender(chunk)`.

4. **DecimationManager**  
   - Pinta debug por LOD (blanco/azul/rojo) y hace `mRenderQueue.Enqueue(chunk, mGenerator)`.

5. **RenderQueueMonohilo**  
   - `Enqueue`: evita duplicados con `mInWait`.  
   - `ProcessSequential`: para cada petición, si LOD debe cambiar (`mTargetSize > 0`, no editado), `Redim` + `SDFGenerator.Sample`; luego `generator.Generate(...)`; encola en `mResults`.  
   - `Apply`: crea `Mesh`, asigna a `MeshFilter`/`MeshCollider`, PropertyBlock por altura.

6. **Edición**  
   - `World.ExecuteModification`: crea `VoxelBrush`, `Grid.ModifyWorld(brush)` devuelve chunks afectados; para cada uno `mRenderQueue.Enqueue(chunk, mSurfaceNet)` y después `ProcessSequential()`.

7. **Generadores de malla**  
   - Consultan vecinos vía `VoxelUtils.GetDensityGlobal` / `IsSolidGlobal` (interpolación trilineal y paso según LOD en `LOD_DATA`).

---

## 4. Decisiones de diseño (según TODO.cs)

- **Datos “sucios”:** Se decidió minimizar el tiempo en estado sucio: no levantar bandera de cambio de LOD hasta tener el nuevo array de datos; swap de arrays en el pool; resample antes de remesh (barrera implícita en la cola).  
- **Límites del mundo:** No se espera excavación en los bordes; el foco es ocupación de CPU, no memoria en fronteras.  
- **Complejidad:** Se evita doble buffer y barreras explícitas; se mantiene procesamiento secuencial/gradual y resample antes de generar malla.

---

## 5. Estado actual y observaciones

### 5.1 Lo que está bien

- Separación clara: `Grid` (datos), `RenderQueueMonohilo` (trabajo), `World` (orquestación).  
- Pool de arrays (`VoxelArrayPool`) con lock para tamaños 8³, 16³, 32³.  
- `VoxelUtils` como único punto de acceso para densidad/sólido global y LOD.  
- LOD por distancia con `LOD_DATA` y procesamiento en dos fases (resample → generate) en la cola.  
- Liberación de mallas en `Chunk.OnDestroy` para no perder VRAM.

### 5.2 Puntos a revisar o corregir

1. **ChunkDiagnosticTool**  
   - Antes leía `mChunks` y `mWorldChunkSize` de `World` por reflexión; en la arquitectura actual esos datos están en `mGrid` (`mGrid.mChunks`, `mGrid.mSizeInChunks`).  
   - Los nombres de GameObjects son `"Chunk_" + mCoord` (ej. `Chunk_(0, 0, 0)`), no `SurfaceNet_Chunk_x_y_z`.  
   - **Acción:** Actualizado para obtener `Grid` desde `World` y usar `mGrid.mChunks` / `mGrid.mSizeInChunks`, y para buscar chunks por el prefijo `"Chunk_"` en lugar del nombre antiguo.

2. **Vigilante – logs**  
   - `Debug.Log` cada comprobación puede generar mucho spam; valorar reducir a cambios de LOD o desactivar en build.

3. **SDFWorld.cs – mensaje de log**  
   - Línea ~252: "Preparate para el error" es claramente de depuración; se puede quitar o sustituir por un mensaje neutro.

4. **VoxelUtils.GetInfoRes**  
   - Devuelve 0, 4 u 8 como índice en `LOD_DATA`. Código correcto pero frágil si se añaden más LODs; un comentario o constante ayudaría.

5. **GeneralData**  
   - Campos estáticos (SchematicReader, dimensiones, chunking, Terrain) no se ven usados en el flujo SDF actual; podría ser código legacy o para otra escena.

6. **Vigilante – centro del chunk**  
   - Usa `UNIVERSAL_CHUNK_SIZE * 0.5f` para el centro; los chunks pueden tener `mSize` 8, 16 o 32. El centro físico del volumen sigue siendo el del cubo de 32 unidades, así que es coherente si el mundo está definido en “unidades físicas” por chunk (32).

---

## 6. Archivos modificados (git)

- `Chunk.cs`, `DSFDensityGenerator.cs`, `Grid.cs`, `RenderQueueMonohilo.cs`, `SDFWorld.cs`, `VoxelUtils.cs`  

Conviene revisar que no queden referencias rotas ni comportamientos duplicados tras estos cambios.

---

## 7. Próximos pasos sugeridos

1. Ejecutar **ChunkDiagnosticTool** (Context Menu en el componente) en Play con un `World` en escena y comprobar que no hay errores de chunks nulos ni nombres incorrectos.  
2. Reducir o condicionar los `Debug.Log` del Vigilante.  
3. Limpiar el mensaje "Preparate para el error" en `BuildSurfaceNets`.  
4. Si se usa SchematicReader / GeneralData en otra parte del proyecto, documentarlo; si no, considerar mover o aislar ese código.

Con esto el proyecto queda diagnosticado y la herramienta de diagnóstico alineada con la arquitectura actual.
