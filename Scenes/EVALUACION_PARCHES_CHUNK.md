# Evaluación: Parches del tamaño de un chunk al desplazarse

## Síntoma
- **Con ForceEnqueue**: parches aparecen muy ocasionalmente
- **Con Enqueue normal**: parches aparecen con mucha frecuencia

Los "parches" son huecos o zonas sin geometría del tamaño de un chunk en el terreno al moverse.

---

## Causa raíz: rechazo silencioso en Enqueue

### Flujo en `DensitySamplerQueueAsync.Enqueue`

```csharp
if (mInWait.TryAdd(pChunk, 0))  // ← Si el chunk YA está en mInWait, FALLA
{
    mQueue.Enqueue(new DensitySamplerJob(pChunk));
    StartWorker();
}
// Si TryAdd falla: no se encola, no se hace nada. Rechazo silencioso.
```

### Escenario que provoca el bug

1. **T0**: El jugador se mueve → `RecycleLayerX` recicla una capa de chunks.
2. **T1**: Para cada chunk reciclado se llama `onChunkRecycled(chunk)` → `EnqueueDensity(chunk)`.
3. **T2**: Chunk A entra en `mInWait` y en `mQueue`. El worker aún no lo ha procesado.
4. **T3**: El jugador se mueve de nuevo antes de que el sampleo termine.
5. **T4**: La misma capa (o una adyacente) se recicla otra vez. Chunk A recibe nuevas coordenadas globales en `ReassignChunk`.
6. **T5**: Se llama `EnqueueDensity(Chunk A)` de nuevo.
7. **T6**: `TryAdd(Chunk A)` **falla** porque Chunk A sigue en `mInWait` (el sampleo anterior no ha terminado).
8. **Resultado**: Chunk A **nunca** se encola para la nueva posición. El slot físico queda sin geometría válida → **parche visible**.

---

## Por qué ForceEnqueue lo mitiga

```csharp
mInWait.TryRemove(pChunk, out _);   // Libera el slot
if (mInWait.TryAdd(pChunk, 0))     // Vuelve a reservar con el chunk actualizado
{
    mQueue.Enqueue(new DensitySamplerJob(pChunk));  // Usa mGenerationId actual
}
StartWorker();
```

- `TryRemove` saca el chunk de `mInWait` aunque el job anterior siga en ejecución.
- `TryAdd` vuelve a reservar el chunk con su nuevo `mGenerationId`.
- El resultado del sampleo anterior se descarta por el chequeo `vChunk.mGenerationId == vJob.mGenerationIdAtEnqueue`.
- El chunk queda encolado para la nueva posición.

---

## Alternativas si se quiere eliminar ForceEnqueue

### Opción A: Reemplazar en el flujo de streaming

En `ReassignChunk` (o en el callback que se pasa a `UpdateStreamingX/Y/Z`), usar siempre `ForceEnqueue` solo para ese flujo, y dejar `Enqueue` para el resto (InitWorld, etc.). No elimina ForceEnqueue, solo lo separa por contexto.

### Opción B: Cambiar la semántica de Enqueue en streaming

En lugar de rechazar cuando el chunk está en `mInWait`, hacer un "upsert" en streaming:

```csharp
// En ChunkPipeline: método específico para streaming que internamente fuerza
public void EnqueueDensityForStreaming(Chunk pChunk)
{
    mDensitySampler.ForceEnqueue(pChunk);
}
```

El callback de `Grid.UpdateStreaming*` usaría este método. El nombre deja claro que es para reciclado.

### Opción C: Eliminar mInWait para chunks reciclados

`mInWait` evita duplicados cuando el mismo chunk se encola varias veces sin reciclar. En streaming, el chunk **sí** debe reencolarse porque cambió de posición. Una opción sería:

- Detectar en `Enqueue` si el chunk fue reciclado (p. ej. `mGenerationId` muy reciente o un flag).
- En ese caso, hacer `TryRemove` + `TryAdd` como en ForceEnqueue.

### Opción D: Cola de reciclado separada

Tener una cola distinta para chunks reciclados por streaming que no use `mInWait`, o que tenga prioridad y no rechace.

---

## Conclusión

| Causa | Impacto |
|-------|---------|
| `Enqueue` rechaza chunks que siguen en `mInWait` | **Alto**: el chunk se recicla pero no se vuelve a encolar |
| Movimiento rápido del jugador | **Alto**: aumenta la probabilidad de reciclar antes de que termine el sampleo |
| Rechazo silencioso (sin log) | **Medio**: dificulta el diagnóstico |

**Recomendación**: Mantener `ForceEnqueue` en el flujo de streaming. El comentario en `DensitySamplerQueueAsync` lo describe correctamente: *"Evita la franja sin geometría cuando se recicla una capa antes de que termine el sampleo anterior"*.

Si se quiere eliminar por diseño, la Opción B (método específico para streaming que internamente fuerza) es la más limpia sin cambiar la lógica interna de `DensitySamplerQueueAsync`.
