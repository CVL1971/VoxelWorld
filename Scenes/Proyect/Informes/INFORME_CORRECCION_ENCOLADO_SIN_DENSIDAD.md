# Informe: Corrección de encolado de terrenos sin densidades

**Fecha:** 5 de marzo de 2025  
**Objetivo:** Evitar que se encolen chunks para remallado cuando no tienen densidades válidas, sin enmascarar el error con comprobaciones de null.

---

## 0. Causa principal y código responsable de la corrección

### Causa principal

Los chunks sin densidad llegaban al remesh porque **`Chunk.Redim` provocaba `NullReferenceException`** cuando se invocaba sobre chunks que aún no tenían `mDCache` (no muestreados). Esto ocurría en `InitWorld`, que llama `Redim` **antes** de `EnqueueDensity`:

```csharp
// InitWorld (SDFWorld.cs, líneas 101-104)
vChunk.Redim(VoxelUtils.LOD_DATA[VoxelUtils.GetInfoLod(2)]);
mChunkPipeline.EnqueueDensity(vChunk);
```

El crash impedía que el pipeline de inicialización terminara correctamente. El flujo quedaba roto y el sistema en un estado inconsistente (síntomas: bordes generados, centro vacío, o crash al arrancar).

### Código responsable de la corrección

**`Chunk.cs` – método `Redim` (líneas 211-217):**

```csharp
public void Redim(int pNewSize)
{
    mSize = pNewSize;
    mActiveCache = (mDCache != null) ? GetActiveCache() : null;  // ← Corrección
}
```

**Antes:** `mActiveCache = GetActiveCache()` accedía a `mDCache` sin comprobar null → crash en InitWorld.

**Después:** Solo se actualiza `mActiveCache` cuando `mDCache` existe. Si no, se deja en null. Así `Redim` puede llamarse legítimamente antes del muestreo.

**Complemento en `AssignDCache` (Chunk.cs):**

```csharp
mDCache = pDcache;
Interlocked.Increment(ref mDCache.mRefs);
mActiveCache = GetActiveCache();  // ← Asegura mActiveCache cuando llega la densidad
```

Cuando el chunk recibe densidad vía `AssignDCache`, `mActiveCache` se actualiza correctamente.

**Flujo corregido:**

1. InitWorld: `Redim(8)` → `mSize = 8`, `mActiveCache = null` (sin crash).
2. `EnqueueDensity` → `SDFGenerator.Sample` → solo chunks con superficie reciben `AssignDCache`.
3. `AssignDCache` → `mActiveCache = GetActiveCache()`.
4. `TryDequeueDensityResult` → solo encola `EnqueueRender` si `mBool1 && mBool2`.

La corrección en `Redim` permite que InitWorld complete y que el pipeline funcione como se espera, evitando que chunks sin densidad lleguen al remesh.

---

## 1. Diagnóstico previo

Se identificó que existían **rutas alternativas** que encolaban chunks para remallado sin comprobar si tenían superficie válida (`mBool1 && mBool2`):

| Ruta | ¿Comprobaba mBool1 && mBool2? |
|------|------------------------------|
| `TryDequeueDensityResult` → `EnqueueRender` | Sí |
| `ProcessPendingResamples` → `EnqueueRender` | **No** |
| `ExecuteModification` → `EnqueueRender` | No (chunk editado tiene densidad in-place) |

Además, los generadores de malla (`SurfeceNetsGeneratorQEF3caches`, `DualContouringGenerator3caches`) contenían comprobaciones `if (cache == null) return meshData` que **enmascaraban** el problema devolviendo mallas vacías en lugar de fallar de forma visible.

---

## 2. Cambios aplicados

### 2.1. ChunkPipeline.cs – ProcessPendingResamples (revertido a EnqueueRender)

**Estado final:**
```csharp
chunk.Redim(targetRes);
EnqueueRender(chunk, mMeshGenerator);
```

**Motivo:** El Vigilante solo selecciona chunks con `BIT_SURFACE`, que ya tienen densidad en las tres resoluciones (32, 16, 8) porque `SDFGenerator.Sample` las rellena en una sola pasada. No requiere re-sampleo: `Redim` cambia el puntero activo; se encola render directamente.

### 2.2. SurfeceNetsGeneratorQEF3caches.cs – Eliminación de comprobación enmascarante

**Antes:**
```csharp
if (cache == null) return meshData;
```

**Después:** Línea eliminada.

**Motivo:** Evitar enmascarar el error. Si un chunk sin densidad llega al generador, debe producirse una excepción visible en lugar de devolver una malla vacía.

### 2.3. DualContouringGenerator3caches.cs – Eliminación de comprobación enmascarante

**Antes:**
```csharp
if (cache == null) return mesh;
```

**Después:** Línea eliminada.

**Motivo:** Idéntico al anterior; no ocultar fallos con retornos silenciosos.

### 2.4. Chunk.cs – Redim, AssignDCache, ReturnDCache (corrección NullReferenceException)

**Problema:** En `InitWorld` se llama `Redim` antes de que el chunk tenga `mDCache` (aún no se ha muestreado). `Redim` llamaba a `GetActiveCache()` que accede a `mDCache`, provocando `NullReferenceException`.

**Redim – Antes:**
```csharp
mSize = pNewSize;
mActiveCache = GetActiveCache();
```

**Redim – Después:**
```csharp
mSize = pNewSize;
mActiveCache = (mDCache != null) ? GetActiveCache() : null;
```

**AssignDCache:** Se añade `mActiveCache = GetActiveCache()` al final para que, cuando el chunk reciba densidad, `mActiveCache` quede correctamente actualizado.

**ReturnDCache:** Se añade `mActiveCache = null` para mantener coherencia cuando se libera la caché.

**Nota:** No se trata de enmascarar con null. `GetDensity`/`SetDensity` siguen sin comprobar null; si se usan en un chunk sin densidad, fallarán de forma explícita. La corrección permite que `Redim` se invoque legítimamente antes del muestreo (p. ej. en `InitWorld`).

### 2.5. VoxelUtils.cs – Eliminación de comprobaciones enmascarantes en GetDensityGlobal e IsSolidGlobal

**Antes:**
```csharp
Chunk target = allChunks[GetChunkIndex(cx, cy, cz, worldSize)];
if (target == null) return 0.0f;  // GetDensityGlobal
if (target == null) return false; // IsSolidGlobal
```

**Después:** Líneas eliminadas.

**Motivo:** Devolver 0.0f o false cuando el chunk vecino es null enmascaraba el error de que se está consultando un chunk inexistente. Si `target` es null en un grid correctamente inicializado, debe producirse una excepción visible.

---

## 3. Rutas no modificadas

### ExecuteModification (SDFWorld.cs)

Se mantiene `EnqueueRender` directo porque:

- `ModifyWorld` modifica la densidad **in-place** mediante `GetDensity`/`SetDensity`.
- Esos métodos requieren `mDCache`; si no existe, se produciría una excepción antes de llegar a `EnqueueRender`.
- Los chunks afectados por el brush ya tienen densidad válida tras la edición.

---

## 4. Garantías tras los cambios

1. **LOD:** Los chunks de cambio de LOD vienen del Vigilante (solo `BIT_SURFACE`). Ya tienen densidad; `ProcessPendingResamples` hace `Redim` + `EnqueueRender` directamente.

2. **Visibilidad de errores:** Si un chunk sin densidad llega al generador de malla, se producirá una excepción clara en lugar de una malla vacía silenciosa.

3. **Cumplimiento del diseño:** Se respeta el comentario existente en `mPendingResamples`: *"No se encolan para remesh hasta que sus densidades estén muestreadas para el LOD deseado"*.

4. **No se encolan chunks sin superficie:** El Vigilante filtra por `BIT_SURFACE`; `ProcessPendingResamples` solo recibe chunks que ya tienen densidad. La ruta `TryDequeueDensityResult` sigue comprobando `mBool1 && mBool2` antes de `EnqueueRender`.

---

## 5. Carga inicial en LOD 2

`InitWorld` carga el mundo en **LOD 2** por defecto:

```csharp
vChunk.Redim(VoxelUtils.LOD_DATA[VoxelUtils.GetInfoLod(2)]);
```

- `GetInfoLod(2)` → índice 8 en `LOD_DATA`
- `LOD_DATA[8]` = **8** (resolución 8 voxels por eje)
- `Redim(8)` establece `mSize = 8` antes de `EnqueueDensity`

`SDFGenerator.Sample` rellena las tres resoluciones (32, 16, 8) en `mDCache`. Tras `AssignDCache`, el chunk usa `mSample2` (resolución 8) para el mallado. El mundo carga en LOD 2 (8 voxels por eje).

---

## 6. Auditoría de comprobaciones null (no enmascarantes)

| Ubicación | Comprobación | ¿Enmascara? |
|-----------|--------------|-------------|
| `Chunk.Redim` | `mDCache != null` | No. Permite invocar `Redim` antes del muestreo; `GetDensity`/`SetDensity` siguen sin comprobar null. |
| `Chunk.AssignDCache` / `ReturnDCache` | `mDCache != null` | No. Gestión de referencias correcta. |
| `ChunkPipeline.AdaptChunk` | `mViewGO == null` | No. Lógica de creación/devolución de GameObject. |
| `ChunkPipeline.RequestLODChange` | `pChunk == null` | No. Validación de parámetros. |
| `RenderQueueAsyncEpoch.Apply` | `pChunk.mViewGO == null` | No. No se puede aplicar malla si no hay GameObject; el chunk no debería llegar aquí sin ViewGO si el flujo es correcto. |
| `Chunk.ClearMesh` | `mViewGO == null`, `mf != null` | No. Comprobaciones de componentes Unity. |

---

## 7. Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `ChunkPipeline.cs` | `ProcessPendingResamples`: `Redim` + `EnqueueRender` (Vigilante garantiza chunks con densidad) |
| `SurfeceNetsGeneratorQEF3caches.cs` | Eliminada comprobación `if (cache == null) return meshData` |
| `DualContouringGenerator3caches.cs` | Eliminada comprobación `if (cache == null) return mesh` |
| `Chunk.cs` | `Redim`: solo actualiza `mActiveCache` si `mDCache != null`; `AssignDCache`: asigna `mActiveCache`; `ReturnDCache`: pone `mActiveCache = null` |
| `VoxelUtils.cs` | Eliminadas comprobaciones `if (target == null) return 0.0f` y `if (target == null) return false` en `GetDensityGlobal` e `IsSolidGlobal` |
