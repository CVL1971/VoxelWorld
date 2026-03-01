# Análisis: Ghosts en fronteras de streaming

## 1. ¿Es efectiva la estrategia (enabled=false + position)?

**Sí, en principio es efectiva** para los chunks reciclados en streaming:

- `MeshRenderer.enabled = false` oculta el chunk de forma inmediata.
- `transform.position = ...` lo mueve a la nueva posición.
- Con el renderer desactivado, no debería verse geometría durante la transición.

---

## 2. Posibles causas de que sigan apareciendo ghosts

### A) Ruta LOD (ProcessPendingResamples) — muy probable

Los chunks que cambian de LOD **no se desactivan**:

```
Vigilante.RequestLODChange → mPendingResamples
ChunkPipeline.ProcessPendingResamples → Redim + EnqueueRender
```

En `ProcessPendingResamples` no se llama a `enabled = false`. El chunk mantiene la malla antigua visible hasta que llega la nueva. En las fronteras, donde hay cambios de LOD frecuentes, esto puede verse como geometría “fantasma” (resolución incorrecta o geometría desfasada).

**Comprobación:** ¿Los ghosts aparecen cerca de transiciones de LOD (cambios de distancia a la cámara)?

---

### B) Falta de ClearMesh en ReassignChunk

En `ReassignChunk` no se llama a `ClearMesh()`. El chunk conserva la malla antigua, aunque con `enabled = false` no debería verse.

Posibles problemas:

- Si algo reactiva el renderer antes de tiempo, se vería la malla vieja.
- `Object.Destroy` en `ClearMesh` es diferido; sin `ClearMesh`, la malla sigue existiendo.

**Recomendación:** Añadir `ClearMesh()` en `ReassignChunk` como medida de seguridad.

---

### C) Orden de ejecución en ChunkPipeline.Update

Orden actual:

1. `UpdateStreamingX/Y/Z` → `ReassignChunk` (disable + position)
2. `ProcessPendingResamples` → `EnqueueRender` (sin disable)
3. `TryDequeueDensityResult` → `EnqueueRender`
4. `TryDequeueRenderResult` → `Apply` (enable + mesh)

Los chunks reciclados se desactivan en el paso 1. Los resultados de render se aplican en el paso 4. Eso es coherente.

---

### D) Apply siempre activa el renderer

En `Apply` se hace siempre `enabled = true`, incluso para chunks que no venían de streaming. Para chunks de InitWorld o LOD no es un problema, pero confirma que el único lugar donde se reactiva el renderer es `Apply`.

---

### E) Resultados descartados por genId

Cuando se descarta un resultado por `genId`:

- En `ChunkPipeline.Update`: `ForceEnqueueDensity`, no se llama a `Apply`.
- En `Apply`: `SetProcessing(false)` y `return`, sin `enabled = true`.

En ambos casos el chunk permanece desactivado si ya lo estaba. Correcto.

---

## 3. Resumen de comprobaciones

| Fuente                    | ¿Se desactiva? | ¿Puede causar ghost? |
|---------------------------|----------------|----------------------|
| ReassignChunk (streaming) | Sí             | No (si la estrategia se aplica bien) |
| ProcessPendingResamples   | No             | Sí (geometría antigua visible) |
| TryDequeueDensityResult   | Depende del chunk | Solo si el chunk no se desactivó antes |
| InitWorld                 | N/A (sin malla inicial) | No |

---

## 4. Recomendaciones

1. **Añadir `ClearMesh()` en ReassignChunk** para evitar que quede malla antigua:
   ```csharp
   chunk.mViewGO.GetComponent<MeshRenderer>().enabled = false;
   chunk.ClearMesh();  // ← añadir
   chunk.mViewGO.transform.position = ...
   ```

2. **Desactivar también en la ruta LOD** cuando se encola un cambio de LOD:
   - En `ProcessPendingResamples`, antes de `EnqueueRender`, hacer `chunk.mViewGO.GetComponent<MeshRenderer>().enabled = false` para los chunks que van a cambiar de LOD.

3. **Comprobar si los ghosts coinciden con cambios de LOD** (Vigilante cerca de fronteras de distancia).
