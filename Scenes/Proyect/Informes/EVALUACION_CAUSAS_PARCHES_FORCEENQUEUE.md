# Por qué siguen apareciendo parches con ForceEnqueue

## Resumen

Hay **4 puntos** donde se descarta trabajo por descoherencia de versión. Solo **1** tenía reencolado (DensitySamplerQueueAsync). Los otros 3 descartaban sin reintentar.

## Los 4 puntos de descarte

| # | Ubicación | Cuándo descarta | ¿Reencolaba? |
|---|-----------|-----------------|--------------|
| 1 | DensitySamplerQueueAsync.ThreadEntry | Chunk reciclado durante el sampleo | ✓ Sí (implementado) |
| 2 | ChunkPipeline.Update (resultados density) | Resultado desencolado tiene genId distinto al chunk actual | ✗ No |
| 3 | RenderQueueAsync.Execute | Chunk reciclado durante el mallado | ✗ No |
| 4 | ChunkPipeline.Update (resultados render) / Apply | Chunk reciclado entre mallado y Apply | ✗ No |

## Causas de parches con ForceEnqueue

- **#2**: Un resultado de density llega al main thread, pero el chunk ya fue reciclado en streaming este mismo frame. Se descarta y no se reencola (aunque el streaming ya ForceEnqueue, puede haber condiciones de carrera).
- **#3**: El worker está mallando. El jugador se mueve, se recicla el chunk. El mallado termina y se descarta por genId. No se reencola → parche.
- **#4**: El mallado termina y encola el resultado. Antes de que el main thread haga Apply, el chunk se recicla en streaming. Apply descarta. No se reencola → parche.

## Solución

Añadir reencolado en los 3 puntos restantes. **Importante**: cuando el chunk fue reciclado, sus densidades son obsoletas. Hay que re-muestrear primero, no solo re-mallar.

- **#2**: Al descartar resultado density → `ForceEnqueueDensity(chunk)`
- **#3**: Al descartar en Execute → `ForceEnqueueDensity(chunk)` (no Render: las densidades son de la posición vieja)
- **#4**: Antes de Apply, si hay descoherencia → `ForceEnqueueDensity(chunk)` (mismo motivo: densidades obsoletas)

## Por qué al volver desaparece el parche

Al volver, el streaming recicla de nuevo ese slot. Se llama `ForceEnqueueDensity` y el chunk pasa por sample → mark → mesh. Las densidades son correctas y el parche desaparece.
