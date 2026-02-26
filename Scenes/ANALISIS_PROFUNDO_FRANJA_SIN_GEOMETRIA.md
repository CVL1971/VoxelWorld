# Análisis en Profundidad: Franja Sin Geometría (Patrón Cíclico Determinista)

## 1. OBSERVACIONES DEL USUARIO (INMUTABLES)

- **Patrón perfecto y reproducible** como un reloj.
- **Franja transversal** al movimiento (plano YZ) **sin geometría**.
- Aparece cada **cierto número exacto de chunks** en la dirección X.
- **"Cuando ya no queda nada, la línea reaparece"**: cuando toda la ventana por la espalda ha salido, la línea reaparece en la frontera entrante.
- **No es race condition** ni comportamiento indeterminado.
- **Completamente determinista** – ocurre a las mismas distancias siempre.

---

## 2. ESTRUCTURA DEL SISTEMA (HECHOS VERIFICADOS)

### 2.1 Grid y ventana activa

- **mSizeInChunks** = (16, 16, 16) → 4096 chunks.
- **mHalfSize** = (8, 8, 8).
- **Ventana activa**: `mActiveMin` a `mActiveMax` en coordenadas globales de chunk.
- **mActiveMin.x** = mCenterChunk.x - 8, **mActiveMax.x** = mCenterChunk.x + 8.
- La ventana abarca **16 chunks en X**.

### 2.2 Identidad de los chunks

- **mCoord** (slot): (x, y, z) con x,y,z ∈ [0, 15]. **Nunca cambia**.
- **mIndex** = ChunkIndex(mCoord) = x + 16*(y + 16*z). **Nunca cambia**.
- **mGlobalCoord**: posición mundial del chunk. **Cambia al reciclar**.
- Relación inicial: `mGlobalCoord = mCenterChunk + (mCoord - mHalfSize)`.
- Chunk en slot (0, y, z) → mGlobalCoord.x = mCenterChunk.x - 8 = mActiveMin.x.
- Chunk en slot (15, y, z) → mGlobalCoord.x = mCenterChunk.x + 7 = mActiveMax.x.

### 2.3 Ciclo de reciclado (movimiento +X)

1. Jugador cruza límite de chunk → `TryGetNewPlayerChunk` true → `UpdateStreamingX`.
2. **outgoingX** = mActiveMin.x (borde izquierdo).
3. **incomingX** = mActiveMax.x + 1 (nuevo borde derecho).
4. `RecycleLayerX`: recorre los 4096 chunks, recicla los que tienen `mGlobalCoord.x == outgoingX`.
5. Esos chunks son los de **slot x = 0** (los 256 del plano YZ izquierdo).
6. `ReassignChunk`: mGlobalCoord = (incomingX, oldY, oldZ), ClearMesh, transform.position = nueva posición, ForceEnqueue.
7. mActiveMin y mActiveMax se desplazan +1 en X.

### 2.4 Periodo del ciclo

- Cada cruce de chunk → se recicla **1 capa** (256 chunks).
- Tras **16 cruces** se han reciclado las 16 capas en X.
- Los mismos **256 objetos Chunk** (slot x=0) vuelven a reciclarse en el cruce 17.
- **Periodo espacial** = 16 chunks = 512 voxels en X.

---

## 3. DIAGNÓSTICO PREVIO RECHAZADO

### 3.1 Por qué no encaja con la fenomenología

El diagnóstico anterior (latencia del pipeline + ClearMesh + transform.position) **no explica** una línea a **distancia constante y precisa**:

- **Latencia variable**: si la franja fuera solo "tiempo hasta Apply", la distancia visible dependería de la velocidad del jugador y del pipeline. Sería **variable**, no constante.
- **Cada cruce de chunk**: si la franja apareciera en cada reciclado (cada 32 voxels), habría 16 franjas visibles simultáneamente en la ventana, no una sola línea a intervalo fijo.
- **Distancia constante y precisa**: la fenomenología indica que la línea aparece a **intervalos exactos** en espacio (p. ej. cada 512 voxels). Eso requiere un **fallo real** del pipeline en posiciones concretas, no solo retraso.

Las causas propuestas (reciclado activo, ClearMesh, transform.position, latencia del pipeline) producirían latencia visual variable o múltiples franjas, **no** una línea única a distancia constante. Hay que buscar un mecanismo que **falle de forma cíclica** en un periodo fijo (p. ej. cada 16 chunks).

---

## 4. HIPÓTESIS ALTERNATIVAS (CAUSA DE FALLO CÍCLICO)

### 4.1 TryAdd en RenderQueueAsync.Enqueue

- `Enqueue` usa `mInWait.TryAdd(pChunk, 0)`. Si el chunk **ya está en cola** (o en proceso), **TryAdd falla** y no se encola el nuevo trabajo.
- **Escenario**: al reciclar el **mismo chunk** por segunda vez (tras 16 cruces), si la petición anterior **sigue en cola** por backlog, TryAdd rechaza la nueva petición.
- **Efecto**: el chunk nunca recibe la malla nueva → franja persistente.
- **Periodicidad**: cada 16 chunks se reciclan los mismos 256 objetos Chunk (slot x=0). Si hay backlog, TryAdd falla en ese ciclo → línea cada 512 voxels.

### 4.2 Bug en GetChunkByGlobalCoord (mapeo slot ↔ global)

- `GetChunkByGlobalCoord` usa `lx = cx - mActiveMin.x`, asumiendo slot lineal: slot 0 = mActiveMin, slot 15 = mActiveMax.
- Tras reciclar, el chunk en **slot 0** pasa a estar en **mActiveMax.x** (borde derecho). El mapeo lineal es **incorrecto**.
- Para `cx = mActiveMax.x` se devuelve el chunk en slot 15 (segundo borde), no el reciclado en slot 0.
- **Impacto**: ModifyWorld y cualquier búsqueda por coordenada global en el borde derecho devolverían el chunk equivocado. No afecta directamente al generador de malla (que recibe el chunk por referencia), pero podría causar estados incoherentes.

### 4.3 Estado residual en el segundo reciclado

- Al reciclar un chunk por **segunda vez** (mismo objeto, 16 cruces después), podría quedar estado residual (mInWait, colas, flags) que interfiera con el nuevo pipeline.
- Requiere trazar el flujo completo para el mismo Chunk en ciclos consecutivos.

### 4.4 Datos validados por el usuario

- **Distancia exacta**: **16 chunks** (512 voxels) entre una línea y otra. ✓
- Confirma la hipótesis TryAdd: mismo periodo que el ciclo de reciclado de los mismos objetos Chunk.

---

## 5. CAUSA RAÍZ CONFIRMADA

Véase **CAUSA_RAIZ_FRANJA_SIN_GEOMETRIA.md** para el documento completo de refactorización.

**Resumen**: `RenderQueueAsync.Enqueue` usa `TryAdd`. Si el chunk ya está en `mInWait` (por backlog), rechaza silenciosamente. El chunk reciclado nunca recibe malla nueva → franja. Solución: `ForceEnqueue` análogo al de DensitySampler para el flujo de streaming.

**Logs de diagnóstico**: Añadidos en `RenderQueueAsync.Enqueue` (hasta 20 avisos cuando TryAdd falla). Reproducir la franja y comprobar si aparecen los logs para confirmar.
