# Informe: Uso de los procedimientos de ChunkPipeline

**Objetivo:** Documentar qué clase utiliza cada procedimiento de ChunkPipeline y para qué propósito.

---

## Procedimientos públicos

### 1. Constructor `ChunkPipeline(Grid, int, int)`

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **World** (SDFWorld) | `Start()` línea 70 | Crear el pipeline al iniciar el mundo. Pasa `mGrid` y `maxParallelRender=8`. El constructor registra el pipeline en el Grid (`SetPipeline`) y crea las colas de density y render. |

---

### 2. `Setup(MeshGenerator)`

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **World** (SDFWorld) | `Start()` línea 71 | Configurar el generador de mallas que usará el pipeline para LOD y remesh. Se pasa `mSurfaceNetQEF` (SurfaceNetsGeneratorQEF3caches). |

---

### 3. `EnqueueDensity(Chunk)`

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **World** (SDFWorld) | `InitWorld()` línea 103 | En el arranque, encolar cada chunk para muestreo de densidad. Todos los chunks del grid pasan por el pipeline de density antes de generar malla. |
| **Grid** | `ReassignChunk()` línea 275 | Al reciclar un chunk por streaming, encolar el chunk para re-muestreo de densidad en su nueva posición. El chunk se recicla cuando el jugador cambia de chunk y se reasigna una capa entera. |

---

### 4. `EnqueueRender(Chunk, MeshGenerator)` (2 parámetros)

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **World** (SDFWorld) | `ExecuteModification()` línea 124 | Tras editar con el brush, encolar los chunks afectados para regenerar malla. Usa `mSurfaceNet` en lugar de `mSurfaceNetQEF`. |
| **ChunkPipeline** | `ProcessPendingResamples()` línea 135 | Internamente, al procesar un cambio de LOD: tras hacer `Redim`, encolar el chunk para re-mallado con el generador configurado. |

---

### 5. `EnqueueRender(Chunk, MeshGenerator, bool)` (3 parámetros)

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **ChunkPipeline** | `Update()` línea 173 | Tras consumir un resultado de density, encolar el chunk para mallado. El tercer parámetro (`pStructural`) indica si el resultado era de un job estructural (reset del chunk) para que el scheduler de render lo trate con prioridad. |

---

### 6. `RequestLODChange(Chunk, int)`

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **Vigilante** | `Run()` línea 57 | Cuando un chunk con superficie visible tiene distinto LOD actual al objetivo (según distancia a cámara), solicitar cambio de LOD. Marca el chunk en `mPendingResamples` con la resolución objetivo (32, 16 u 8). El Vigilante corre en un Task separado y revisa periódicamente el estado. |

---

### 7. `Update(Vector3)`

| Clase que invoca | Ubicación | Propósito |
|------------------|-----------|-----------|
| **World** (SDFWorld) | `Update()` línea 137 | Cada frame, ejecutar el bucle principal del pipeline: streaming, LOD pendientes, consumir resultados de density y aplicar resultados de render. Se pasa la posición de la cámara para detectar cambio de chunk del jugador. |

---

## Procedimientos internos (solo usados por ChunkPipeline)

| Procedimiento | Usado en | Propósito |
|---------------|----------|-----------|
| `TryDequeueDensityResult` | `Update()` | Obtener resultados de density completados y consumirlos. |
| `TryDequeueRenderResult` | `Update()` | Obtener mallas generadas. |
| `ApplyRenderResult` | `Update()` | Aplicar la malla al chunk en el hilo principal. |
| `ProcessPendingResamples` | `Update()` | Procesar chunks de LOD pendientes: Redim, encolar render, dibujar debug. |

---

## Flujo resumido

```
World.Start
  ├── new ChunkPipeline
  ├── Setup(mSurfaceNetQEF)
  └── InitWorld → EnqueueDensity (todos los chunks)

World.Update (cada frame)
  └── ChunkPipeline.Update(posCámara)
        ├── Grid.TryGetNewPlayerChunk → streaming (UpdateStreamingX/Y/Z)
        ├── ProcessPendingResamples(20) → LOD
        ├── TryDequeueDensityResult → MarkSurface → EnqueueRender(..., pStructural)
        └── TryDequeueRenderResult → ApplyRenderResult

Vigilante.Run (Task asíncrono)
  └── RequestLODChange(chunk, targetRes) cuando LOD actual ≠ objetivo

Grid.ReassignChunk (streaming)
  └── EnqueueDensity(chunk) para chunk reciclado

World.ExecuteModification (edición con brush)
  └── EnqueueRender(chunk, mSurfaceNet) para chunks afectados
```
