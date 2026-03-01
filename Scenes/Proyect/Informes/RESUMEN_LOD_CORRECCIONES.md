# Resumen completo de correcciones - VoxelWorld

## Índice
1. [Problemas del generador de mallas (Surface Nets)](#1-problemas-del-generador-de-mallas)
2. [Problemas del sistema LOD](#2-problemas-del-sistema-lod)
3. [Flujo temporal síncrono (revertido)](#3-flujo-temporal-síncrono)

---

## 1. Problemas del generador de mallas

### 1.1 Geometría vacía / IndexOutOfRangeException en celdas de borde

**Script:** `SurfeceNetsGeneratorQEF3caches.cs`  
**Procedimiento:** `Generate`, `CellCrossesIso`, `ComputeCellVertexQEF`, `EmitCorrectFaces`

**Problema:** El generador iteraba sobre celdas en el rango `0..size` cuando el array de densidades solo cubría un dominio menor. Al acceder a celdas en el borde (ej. `x+1`, `y+1`, `z+1` para la celda en `(size, size, size)`), se producía `IndexOutOfRangeException` porque faltaban muestras en las caras de frontera.

**Causa:** El array de cache tenía padding insuficiente. Para que Surface Nets genere vértices en las caras que conectan con chunks vecinos, necesita muestras en las posiciones `-1` y `size+1` por cada eje. Con `size+2` solo se cubría `0..size+1`, sin margen para las celdas de borde.

**Solución:** Aumentar el padding a `size+3` para cubrir posiciones lógicas `-1` a `size+1`. Los bucles de celdas se limitan a `0..size` (celdas válidas). La indexación usa `(x+1) + p*((y+1) + p*(z+1))` con `p = size+3`.

**Código reemplazado (concepto – array y bucles con padding insuficiente):**
```csharp
// ANTES: padding insuficiente, array size+2
int p = size + 2;
for (int z = 0; z <= size; z++)
    for (int y = 0; y <= size; y++)
        for (int x = 0; x <= size; x++)
            if (CellCrossesIso(cache, x, y, z, p, ...))  // Acceso a x+1,y+1,z+1 → IndexOutOfRange
```

**Código reemplazador:**
```csharp
// DESPUÉS: p = size+3, array cubre -1 a size+1
int p = size + 3;
for (int z = 0; z <= size; z++)
    for (int y = 0; y <= size; y++)
        for (int x = 0; x <= size; x++)
            if (CellCrossesIso(cache, x, y, z, p, ISO_THRESHOLD))
```

---

### 1.2 Gaps (huecos) entre chunks adyacentes

**Script:** `Chunk.cs`, `DSFDensityGenerator.cs`  
**Procedimientos:** `DeclareSampleArray`, `IndexSample`, `GetDensity`, `SetDensity` / `SDFGenerator.Sample`

**Problema:** Aparecían huecos visibles en las fronteras entre chunks. La geometría no era continua porque faltaban muestras de densidad en las caras compartidas.

**Causa:** El array de densidades no incluía una capa de padding en las caras de frontera. El generador necesita leer densidades en `-1` y `size+1` para cerrar correctamente las celdas de borde.

**Solución:** Arrays con `size+3` por eje (posiciones lógicas `-1` a `size+1`). `IndexSample` mapea `(x,y,z)` a `(x+1) + p*((y+1) + p*(z+1))` con `p = size+3`.

**Script: Chunk.cs – Procedimiento: DeclareSampleArray**

**Código reemplazado:**
```csharp
// ANTES: arrays sin padding suficiente para fronteras
int res0 = 32 + 2;  // o similar
mSample0 = new float[res0 * res0 * res0];
```

**Código reemplazador:**
```csharp
// DESPUÉS: padding +3 por eje (posiciones -1 a size+1)
int res0 = VoxelUtils.LOD_DATA[0] + 3;
int res1 = VoxelUtils.LOD_DATA[4] + 3;
int res2 = VoxelUtils.LOD_DATA[8] + 3;
mSample0 = new float[res0 * res0 * res0];
mSample1 = new float[res1 * res1 * res1];
mSample2 = new float[res2 * res2 * res2];
```

**Script: DSFDensityGenerator.cs – Procedimiento: Sample(Chunk)**

**Código reemplazado:**
```csharp
// ANTES: paddedRes = N+2 o menor, sin cubrir size+1
int paddedRes = N + 2;
```

**Código reemplazador:**
```csharp
// DESPUÉS: paddedRes = N+3, cubre -1 a N+1
int paddedRes = N + 3;
```

---

## 2. Problemas del sistema LOD

### 2.1 LOD no visibles: `mIsEdited` bloqueaba todos los chunks

**Script:** `Chunk.cs`  
**Procedimientos:** `SetDensity`, `ApplyBrush`

**Problema:** El Vigilante detectaba cambios de LOD (logs `[LOD] Vigilante detecta`) pero no se aplicaban cambios visuales. `RequestLODChange` salía sin encolar nada.

**Causa:** `SetDensity` asignaba `mIsEdited = true` en cada llamada. `SDFGenerator.Sample` usa `SetDensity` para rellenar las densidades iniciales, por lo que todos los chunks quedaban con `mIsEdited = true`. La condición `if (pChunk.mIsEdited) return;` en `ChunkPipeline.RequestLODChange` rechazaba todos los chunks.

**Solución:** Marcar `mIsEdited` solo en edición manual (ModifyWorld, ApplyBrush), no cuando el generador SDF escribe datos.

**Script: Chunk.cs – Procedimiento: SetDensity**

**Código reemplazado:**
```csharp
public void SetDensity(int x, int y, int z, float pDensity)
{
    float[] cache = GetActiveCache();
    int p = mSize + 3;
    cache[IndexSample(x, y, z, p)] = pDensity;
    mIsEdited = true;
}
```

**Código reemplazador:**
```csharp
public void SetDensity(int x, int y, int z, float pDensity)
{
    float[] cache = GetActiveCache();
    int p = mSize + 3;
    cache[IndexSample(x, y, z, p)] = pDensity;
    // mIsEdited solo se marca en ModifyWorld/ApplyBrush (edición usuario), no en SDFGenerator
}
```

**Script: Chunk.cs – Procedimiento: ApplyBrush**

**Código reemplazado:**
```csharp
public void ApplyBrush(VoxelBrush pBrush)
{
    int p = mSize + 3;
    float vStep = ...
```

**Código reemplazador:**
```csharp
public void ApplyBrush(VoxelBrush pBrush)
{
    mIsEdited = true;
    int p = mSize + 3;
    float vStep = ...
```

---

### 2.2 Chunks vacíos al cambiar LOD: arrays mSample1 y mSample2 sin datos

**Script:** `DSFDensityGenerator.cs`  
**Procedimiento:** `Sample(Chunk pChunk)`

**Problema:** Al cambiar a LOD 1 (res 16) o LOD 2 (res 8), los chunks aparecían vacíos (sin geometría).

**Causa:** `SDFGenerator.Sample` solo rellenaba el array activo según `mSize`. Al inicio todos los chunks tenían `mSize = 32`, así que solo se poblaba `mSample0`. `mSample1` y `mSample2` nunca se rellenaban. Al hacer `Redim(16)`, el generador usaba `mSample1`, que contenía ceros.

**Solución:** Rellenar los tres caches durante la inicialización. `SetDensity` usa `GetActiveCache()` según `mSize`, así que se itera sobre las tres resoluciones (32, 16, 8) cambiando temporalmente `mSize` para escribir en cada array.

**Script: DSFDensityGenerator.cs – Procedimiento: Sample(Chunk pChunk)**

**Código reemplazado:**
```csharp
public static void Sample(Chunk pChunk)
{
    int N = pChunk.mSize;
    int paddedRes = N + 3;
    Vector3Int origin = pChunk.mWorldOrigin;
    float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / (float)N;
    pChunk.ResetGenericBools();

    for (int z = 0; z < paddedRes; z++)
    {
        float worldZ = origin.z + ((z - 1) * vStep);
        for (int x = 0; x < paddedRes; x++)
        {
            float worldX = origin.x + ((x - 1) * vStep);
            float height = GetGeneratedHeight(worldX, worldZ);
            for (int y = 0; y < paddedRes; y++)
            {
                float worldY = origin.y + ((y - 1) * vStep);
                float density = Mathf.Clamp01((height - worldY) * SMOOTHNESS + ISO_SURFACE);
                pChunk.SetDensity(x - 1, y - 1, z - 1, density);
                // ... mBool1, mBool2
            }
        }
    }
}
```

**Código reemplazador:**
```csharp
public static void Sample(Chunk pChunk)
{
    int savedSize = pChunk.mSize;
    pChunk.ResetGenericBools();

    int[] resolutions = { (int)VoxelUtils.LOD_DATA[0], (int)VoxelUtils.LOD_DATA[4], (int)VoxelUtils.LOD_DATA[8] };

    foreach (int N in resolutions)
    {
        pChunk.mSize = N;
        int paddedRes = N + 3;
        Vector3Int origin = pChunk.mWorldOrigin;
        float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / (float)N;

        for (int z = 0; z < paddedRes; z++)
        {
            float worldZ = origin.z + ((z - 1) * vStep);
            for (int x = 0; x < paddedRes; x++)
            {
                float worldX = origin.x + ((x - 1) * vStep);
                float height = GetGeneratedHeight(worldX, worldZ);
                for (int y = 0; y < paddedRes; y++)
                {
                    float worldY = origin.y + ((y - 1) * vStep);
                    float density = Mathf.Clamp01((height - worldY) * SMOOTHNESS + ISO_SURFACE);
                    pChunk.SetDensity(x - 1, y - 1, z - 1, density);
                    if (x > 0 && x <= N && y > 0 && y <= N && z > 0 && z <= N)
                    {
                        if (density >= ISO_SURFACE) pChunk.mBool1 = true;
                        else pChunk.mBool2 = true;
                    }
                }
            }
        }
    }

    pChunk.mSize = savedSize;
}
```

---

## 3. Flujo temporal síncrono (revertido)

**Scripts:** `ChunkPipeline.cs`, `RenderStackAsync.cs`  
**Procedimientos:** `ProcessPendingResamples`, `ForceEnqueue` / `ProcessSync` (eliminado)

**Contexto:** Para descartar problemas de concurrencia, se usó temporalmente un flujo síncrono (`ProcessSync`: Generate + Apply en el hilo principal). Tras confirmar que el LOD funcionaba, se revirtió al flujo asíncrono.

**Script: ChunkPipeline.cs – Procedimiento: ProcessPendingResamples**

**Código temporal (síncrono, ya revertido):**
```csharp
t.chunk.mTargetSize = 0;
mRenderQueue.ProcessSync(t.chunk, mGenerator);
```

**Código final (asíncrono):**
```csharp
t.chunk.mTargetSize = 0;
mRenderQueue.ForceEnqueue(t.chunk, mGenerator);
```

**Script: RenderStackAsync.cs:** Se eliminó el método `ProcessSync` añadido durante el diagnóstico.

---

## Resumen de scripts alterados

| Script | Procedimientos modificados |
|--------|----------------------------|
| **Chunk.cs** | `SetDensity`, `ApplyBrush`, `DeclareSampleArray` |
| **DSFDensityGenerator.cs** | `Sample(Chunk pChunk)` |
| **SurfeceNetsGeneratorQEF3caches.cs** | `Generate`, uso de `p = size + 3` |
| **ChunkPipeline.cs** | `ProcessPendingResamples` (ForceEnqueue) |
| **RenderStackAsync.cs** | Eliminado `ProcessSync` |

---

## Flujo LOD actual (asíncrono)

1. **Vigilante** (Task): detecta distancia, llama a `RequestLODChange`.
2. **ChunkPipeline.RequestLODChange**: añade chunk a `mPendingResamples` (si `!mIsEdited`).
3. **SDFWorld.Update** → **ProcessPendingResamples**: `Redim(targetRes)`, `ForceEnqueue`.
4. **RenderStackAsync.ProcessLoop**: `Execute` → `Generate` en workers.
5. **SDFWorld.Update** → `while (mResultsLOD.TryDequeue)` → `Apply` en main thread.
