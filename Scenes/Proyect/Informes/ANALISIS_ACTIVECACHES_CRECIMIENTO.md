# Análisis: Crecimiento de ActiveCaches hasta saturación

**Problema:** A pesar del código que devuelve arrays al pool cuando `isSurface` es false, las estadísticas muestran que ActiveCaches crece constantemente hasta estabilizarse cuando todos los chunks que alguna vez fueron sólidos tienen mDCache. Los flags se resetean correctamente; la rama `else` debería ejecutarse. ¿Se ejecuta?

---

## 1. Flujo de re-muestreo

Los chunks se re-muestrean en dos situaciones:

| Origen | Cuándo | Método |
|--------|--------|--------|
| InitWorld | Arranque | EnqueueDensity para todos los chunks |
| Grid.ReassignChunk | Streaming (jugador se mueve) | EnqueueDensity para chunks reciclados |

`ReassignChunk` se invoca cuando el jugador cruza un borde de chunk y una capa entera se recicla con nuevas coordenadas globales. El chunk conserva su objeto pero cambia de posición en el mundo.

---

## 2. Estructura de SDFGenerator.Sample (DSFDensityGenerator.cs)

```
Sample(pChunk)
├── ResetGenericBools()
├── [EARLY EXIT 1] origin.y > maxH + margin  → aire puro
│       mBool2=true, mBool1=false
│       return;   ← SIN ReturnDCache, SIN MarkSurface
│
├── [EARLY EXIT 2] origin.y + chunkSize < minH - margin  → sólido puro
│       mBool1=true, mBool2=false
│       return;   ← SIN ReturnDCache, SIN MarkSurface
│
└── [RUTA NORMAL] ArrayPool.Get()
    ├── Llenar arrays, setear mBool1/mBool2
    ├── MarkSurface(pChunk)
    ├── if (isSurface)
    │       AssignDCache(arrays)
    └── else
            ArrayPool.Return(arrays)
            pChunk.ReturnDCache()
            return
```

---

## 3. Causa raíz: early exits no liberan mDCache

### 3.1. El bug

En las rutas de **early exit** (aire puro y sólido puro), el código hace `return` sin:

1. Llamar a `pChunk.ReturnDCache()`
2. Llamar a `pChunk.mGrid.MarkSurface(pChunk)`

### 3.2. Consecuencia

Cuando un chunk se re-muestrea (p. ej. por streaming):

1. El chunk tenía `mDCache` (era superficie en su posición anterior).
2. Se recicla con `ReassignChunk` y nuevas coordenadas.
3. Se encola con `EnqueueDensity` y se ejecuta `SDFGenerator.Sample`.
4. En la nueva posición el chunk es aire puro o sólido puro → early exit.
5. Se hace `return` sin llamar a `ReturnDCache()`.
6. El chunk mantiene `mDCache`.
7. El chunk sigue contando como "ActiveCache" aunque ya no sea superficie.

### 3.3. Por qué ActiveCaches crece hasta saturar

- Cada chunk que en algún momento fue superficie obtiene `mDCache`.
- Cuando pasa a aire/sólido puro por streaming, el early exit no libera la caché.
- Con el tiempo, todos los chunks que alguna vez fueron superficie conservan `mDCache`.
- ActiveCaches crece hasta estabilizarse en el número de chunks que han sido superficie al menos una vez.

---

## 4. ¿Se ejecuta la rama `else`?

Sí, pero solo en la **ruta normal** (cuando el chunk no es ni aire puro ni sólido puro).

- En la ruta normal, si `isSurface` es false, se ejecuta `ReturnDCache()` y se devuelven los arrays al pool.
- El problema está en los early exits: ahí nunca se llega a la rama `else` porque se hace `return` antes de `ArrayPool.Get()` y del `if (isSurface)`.

Los flags se resetean correctamente; el fallo es que en los early exits no se llama a `ReturnDCache()` ni a `MarkSurface()`.

---

## 5. Lógica de referencias del ArrayPool

| Operación | mRefs | Efecto |
|-----------|-------|--------|
| ArrayPool.Get() | 1 | Nueva referencia al DCache |
| Chunk.AssignDCache() | Incrementa | Chunk toma referencia |
| Chunk.ReturnDCache() | Decrementa, Return al pool | Chunk suelta referencia |
| ArrayPool.Return() | — | DCache vuelve al pool |

En los early exits no se llama a `ReturnDCache()`, por lo que el chunk nunca suelta su referencia y el DCache no vuelve al pool.

---

## 6. Corrección propuesta

En cada early exit, antes del `return`, añadir:

```csharp
// Early exit aire puro (línea ~99)
if (origin.y > maxH + margin)
{
    pChunk.mBool2 = true;
    pChunk.mBool1 = false;
    pChunk.mGrid.MarkSurface(pChunk);
    pChunk.ReturnDCache();
    return;
}

// Early exit sólido puro (línea ~107)
if (origin.y + chunkSize < minH - margin)
{
    pChunk.mBool1 = true;
    pChunk.mBool2 = false;
    pChunk.mGrid.MarkSurface(pChunk);
    pChunk.ReturnDCache();
    return;
}
```

Con esto:

1. Se actualiza el estado de superficie en el grid (`MarkSurface`).
2. Se libera `mDCache` si el chunk lo tenía (`ReturnDCache`).
3. Los DCache vuelven al pool cuando el chunk deja de ser superficie.

---

## 7. Resumen

| Pregunta | Respuesta |
|----------|-----------|
| ¿Por qué crece ActiveCaches? | Los early exits no llaman a `ReturnDCache()`. |
| ¿Se ejecuta la rama `else`? | Sí, pero solo en la ruta normal; los early exits hacen `return` antes. |
| ¿Los flags se resetean? | Sí; el fallo está en la ausencia de `ReturnDCache()` en los early exits. |
| ¿Solución? | Llamar a `MarkSurface` y `ReturnDCache` en ambos early exits antes del `return`. |
