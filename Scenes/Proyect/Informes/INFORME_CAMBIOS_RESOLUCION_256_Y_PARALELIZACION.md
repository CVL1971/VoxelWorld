# Informe: Cambios de Resolución 256 y Paralelización

**Fecha:** Sesión actual  
**Contexto:** Subida de resolución de buffers de chunks de 128 a 256, corrección de artefactos visuales y eliminación de cuellos de botella por cachés no paralelizables.

---

## 1. Resumen Ejecutivo

Se realizaron modificaciones en cinco áreas principales:

1. **Auditoría de índices** – Consistencia de la fórmula `x + (y * width) + (z * width * depth)` con la nueva resolución.
2. **Prevención de overflow** – Uso de `long` en cálculos de índices para buffers de 256³.
3. **Refactor de cachés** – Sustitución de variables estáticas/compartidas por `ThreadLocal` para permitir paralelización.
4. **Sincronización Mesh-Sampler** – Verificación del orden de ejecución.
5. **Cálculo de vStep** – Soporte de cualquier resolución mediante `GetLodStep()`.

---

## 2. Cambios por Archivo

### 2.1 `VoxelUtils.cs`

| Cambio | Descripción |
|--------|-------------|
| `LOD_RES_BASE = 256` | Constante para resolución base (LOD 0). |
| `LOD_DATA` derivado | `[256, 1, ...], [128, 2, ...], [64, 4, ...]` a partir de `LOD_RES_BASE`. |
| `GetLodBufferSize(int res)` | Calcula `(res+3)³` con `long` para evitar overflow. |
| `GetLodStep(int lodIndex)` | Devuelve `UNIVERSAL_CHUNK_SIZE / res` para el paso físico por voxel. |
| `GetDensityGlobal` / `IsSolidGlobal` | Sustitución de `LOD_DATA[lodIdx+1]` por `GetLodStep(lodIdx)`. |

### 2.2 `DSFDensityGenerator.cs` (SDFGenerator)

| Cambio | Descripción |
|--------|-------------|
| `ThreadLocal` para cachés | Sustitución de `static float[] mLod0, mLod1, mLod2` por `ThreadLocal<(float[], float[], float[])> sLodCaches`. |
| Índices con `long` | `idx = (int)(zOffset + (long)y * paddedRes + x)` y `slice` como `long`. |
| Eliminación de constructor estático | Inicialización de arrays en el factory de `ThreadLocal`. |

**Motivo:** Las cachés estáticas provocaban race conditions cuando varios jobs de `DensitySampler` se ejecutaban en paralelo.

### 2.3 `ArrayPool.cs`

| Cambio | Descripción |
|--------|-------------|
| `GetLodBufferSize` | Sustitución de `(int)Mathf.Pow(...)` por `VoxelUtils.GetLodBufferSize()`. |

**Motivo:** Evitar overflow y pérdida de precisión con `Mathf.Pow` en resoluciones altas.

### 2.4 `Chunk.cs`

| Cambio | Descripción |
|--------|-------------|
| `IndexSample` con `long` | Cálculo del índice con `long` y comprobación de rango antes del cast a `int`. |

```csharp
// Antes
return (x + 1) + resWithPadding * ((y + 1) + resWithPadding * (z + 1));

// Después
long p = resWithPadding;
long idx = (x + 1L) + p * ((y + 1L) + p * (z + 1L));
return idx <= int.MaxValue ? (int)idx : int.MaxValue;
```

### 2.5 `SurfeceNetsGeneratorQEF3caches.cs`

| Cambio | Descripción |
|--------|-------------|
| `ThreadLocal` para cachés | Sustitución de `mCache0, mCache1, mCache2` por `ThreadLocal<(float[], float[], float[])> sCaches`. |
| `GetD` con `long` | Cálculo del índice con `long` para evitar overflow. |
| `vStep` calculado | `vStep = (float)UNIVERSAL_CHUNK_SIZE / size` en lugar de `LOD_DATA[lodIndex+1]`. |

**Motivo:** Las cachés por instancia provocaban race conditions cuando varios jobs de `RenderQueue` ejecutaban `Generate()` en paralelo.

---

## 3. Fórmula de Índice

Se mantiene la convención:

```
index = x + (y * width) + (z * width * depth)
```

Con padding +1 para el rango lógico `[-1, size+1]`:

```
index = (x+1) + p*(y+1) + p²*(z+1)
```

donde `p = res + 3` (paddedRes).

---

## 4. Overflow en 256³

| Operación | Valor | Límite int | Estado |
|-----------|-------|------------|--------|
| 256³ | 16.777.216 | 2.147.483.647 | OK |
| 259³ | 17.373.979 | 2.147.483.647 | OK |
| 515³ | 136.590.875 | 2.147.483.647 | OK |

Se usan `long` en cálculos intermedios para evitar overflow en futuras resoluciones mayores.

---

## 5. Cachés Refactorizadas

| Componente | Antes | Después |
|------------|-------|---------|
| SDFGenerator | `static float[]` compartidos | `ThreadLocal` por hilo |
| SurfaceNetsGeneratorQEF3caches | `float[]` por instancia | `ThreadLocal` por hilo |

Cada hilo mantiene sus propias copias para evitar condiciones de carrera.

---

## 6. Sincronización Mesh-Sampler

El flujo garantiza que el Mesher no lee antes de que el Sampler haya terminado:

1. **Density job** termina → escribe en el chunk.
2. `DensitySamplerResult.Enqueue(chunk)`.
3. **ChunkPipeline.Update**: `TryDequeueDensityResult` → chunk con densidad lista.
4. `EnqueueRender(chunk)` solo **después** de desencolar.
5. El **Render job** lee el chunk cuando ya tiene datos.

---

## 7. Configuración de Resolución

Para cambiar la resolución base, modificar en `VoxelUtils.cs`:

```csharp
public const int LOD_RES_BASE = 256;  // 256 alta calidad, 32 rendimiento
```

`LOD_DATA` se deriva automáticamente: 256, 128, 64.

---

## 8. Archivos Modificados

| Archivo | Cambios |
|---------|---------|
| `VoxelUtils.cs` | LOD_RES_BASE, GetLodBufferSize, GetLodStep, LOD_DATA derivado |
| `DSFDensityGenerator.cs` | ThreadLocal, índices con long |
| `ArrayPool.cs` | GetLodBufferSize |
| `Chunk.cs` | IndexSample con long |
| `SurfeceNetsGeneratorQEF3caches.cs` | ThreadLocal, GetD con long, vStep calculado |

---

## 9. Archivos No Modificados (posibles mejoras futuras)

- `DualContouringGenerator3caches.cs` – mantiene cachés por instancia y `LOD_DATA[lodIndex+1]`. Actualizar si se usa.
- `SurfeceNetsGeneratorQEFOriginal.cs`, `SurfaceNetsGeneratorQEF.cs`, etc. – siguen usando `LOD_DATA` para vStep.
