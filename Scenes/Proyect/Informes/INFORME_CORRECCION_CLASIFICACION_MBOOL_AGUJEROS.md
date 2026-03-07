# Informe: Corrección clasificación mBool1/mBool2 — causa raíz de los agujeros

**Fecha:** Marzo 2025  
**Estado:** Corregido — agujeros desaparecidos tras el cambio.

---

## Ubicación en código fuente

| Archivo | Líneas | Función |
|---------|--------|---------|
| **DSFDensityGenerator.cs** | 72-73 | Early exit: asigna `mBool1`/`mBool2` según `ChunkEarlyExitResult` |
| **DSFDensityGenerator.cs** | 131-133 | Bucle de muestreo: `pChunk.mBool1 = true` / `pChunk.mBool2 = true` según densidad |
| **DSFDensityGenerator.cs** | 139 | `bool isSurface = (pChunk.mBool1 && pChunk.mBool2)` |
| **DSFDensityGenerator.cs** | 312, 317 | Ruta alternativa (SetDensity): asigna bools |
| **ChunkPipeline.cs** | 61 | `if (densityChunk.mBool1 && densityChunk.mBool2) EnqueueRender(...)` |
| **ChunkPipeline.cs** | 148 | `if (vChunk.mBool1 && vChunk.mBool2)` — otra ruta de encolado |
| **Chunk.cs** | 33-34 | Declaración: `public bool mBool1`, `public bool mBool2` |
| **Chunk.cs** | 223-224 | `ResetGenericBools()`: pone ambos a `false` |
| **Grid.cs** | 474 | `Surface(pChunk, pChunk.mBool1 && pChunk.mBool2)` — actualiza `BIT_SURFACE` |

La corrección se aplicó en **DSFDensityGenerator.cs** (líneas 131-133): se eliminó la condición restrictiva del rango interior.

---

## 1. Naturaleza del error

### 1.1 Descripción

La clasificación de chunks como *superficie* (`mBool1 && mBool2`) se realizaba **solo en un subconjunto** de las muestras de densidad. La condición restrictiva era:

```csharp
if (x > 0 && x <= N && y > 0 && y <= N && z > 0 && z <= N)
{
    if (density >= ISO_SURFACE) pChunk.mBool1 = true;
    else pChunk.mBool2 = true;
}
```

En espacio de cache (índices del bucle), esto equivale a **excluir**:
- `x = 0`, `y = 0`, `z = 0` → capa de padding en la cara negativa (grid -1)
- `x > N`, `y > N`, `z > N` → capas de padding en las caras positivas (grid N, N+1)

Solo se clasificaban las muestras en el rango interior `(1..N)` en cada eje, es decir, el dominio lógico del chunk sin las capas de padding.

### 1.2 Tipo de error

**Error lógico de diseño:** La restricción del rango de clasificación era incorrecta. La intención probable era evitar que el padding “contaminara” la decisión, pero el efecto fue el contrario: se ignoraron precisamente las muestras donde la superficie suele cruzar en las fronteras entre chunks.

---

## 2. Impacto

### 2.1 Efecto directo

Chunks que tenían la superficie **solo en las capas de padding** (bordes del chunk) quedaban con `mBool1 && mBool2 == false` porque ninguna de las muestras que cruzaban el umbral estaba dentro del rango `(1..N)`.

### 2.2 Cadena de consecuencias

1. **`isSurface = false`** → El chunk no se considera en la superficie.
2. **`AssignDCache` no se llama** → El chunk no recibe datos de densidad para el mesher.
3. **`EnqueueRender` no se encola** → El chunk nunca entra en la cola de render.
4. **Malla vacía o inexistente** → Aparece un agujero en la geometría.

### 2.3 Dónde se manifiesta

- **ChunkPipeline:** `if (densityChunk.mBool1 && densityChunk.mBool2) EnqueueRender(...)` — los chunks mal clasificados nunca se encolan.
- **Grid.MarkSurface:** `Surface(pChunk, pChunk.mBool1 && pChunk.mBool2)` — el bit `BIT_SURFACE` queda a 0.
- **Vigilante LOD:** Solo considera chunks con `BIT_SURFACE`; los afectados quedan fuera del flujo de LOD y render.

### 2.4 Severidad

**Alta.** Los agujeros eran visibles y persistentes. La geometría era discontinua en las fronteras entre chunks cuando la superficie cruzaba únicamente en el padding.

---

## 3. Explicación del fenómeno desde un punto de vista lógico

### 3.1 Por qué el padding es esencial

Surface Nets (y Dual Contouring) necesitan muestras en las posiciones `-1` y `size+1` para cerrar correctamente las celdas de borde. El array de densidades tiene `paddedRes = N + 3` y cubre posiciones lógicas desde `-1` hasta `N+1` en cada eje.

Las celdas en el borde del chunk comparten caras con chunks vecinos. La geometría de esas caras se calcula usando muestras en el padding. Si la superficie cruza el umbral **solo** en esas muestras de padding, el chunk tiene superficie real que debe renderizarse.

### 3.2 Por qué la restricción era errónea

La condición `x > 0 && x <= N` excluía las muestras en `x = 0` (grid -1) y `x = N+1`, `N+2` (grid N, N+1). Esas muestras son precisamente las que definen las caras compartidas entre chunks.

**Analogía:** Es como decidir si una habitación tiene ventana mirando solo el centro de la habitación e ignorando las paredes. Si la ventana está en la pared, la conclusión sería errónea.

### 3.3 Caso típico que provocaba agujeros

- Chunk A y Chunk B son vecinos.
- La superficie cruza la frontera entre ellos.
- En el chunk A, todas las muestras en el interior `(1..N)` están por encima del umbral (sólido).
- La única muestra por debajo del umbral (aire) está en el padding `x = 0` (cara compartida con B).
- Con la condición restrictiva: `mBool2` nunca se pone a `true` → `isSurface = false` → no se renderiza → agujero.

### 3.4 Corrección aplicada

Se eliminó la restricción de rango. Ahora **todas** las muestras del bucle contribuyen a la clasificación:

```csharp
if (density >= ISO_SURFACE)
    pChunk.mBool1 = true;
else
    pChunk.mBool2 = true;
```

Así, cualquier cruce de superficie en el dominio muestreado (incluido el padding) se detecta correctamente.

---

## 4. Origen del cambio — referencia histórica y deducción

### 4.1 Referencia explícita

En **RESUMEN_LOD_CORRECCIONES.md**, sección **2.2** (“Chunks vacíos al cambiar LOD: arrays mSample1 y mSample2 sin datos”), el “Código reemplazador” incluye la condición:

```csharp
if (x > 0 && x <= N && y > 0 && y <= N && z > 0 && z <= N)
{
    if (density >= ISO_SURFACE) pChunk.mBool1 = true;
    else pChunk.mBool2 = true;
}
```

Ese cambio formaba parte de la corrección para rellenar los tres caches de LOD (32, 16, 8) en lugar de solo el activo. El código anterior tenía un comentario `// ... mBool1, mBool2` en el lugar de la lógica completa, lo que indica que la clasificación se añadió o se reescribió en ese momento.

### 4.2 Deducción en ausencia de más historial

Si no hubiera existido esa referencia, la deducción sería:

1. **Contexto:** La condición usa `N` y los índices del bucle sobre `paddedRes = N + 3`. Eso sugiere que se introdujo cuando ya existía el padding de `N+3` y el muestreo multi-resolución.

2. **Intención probable:** Evitar que las celdas de padding “contaminaran” la clasificación, asumiendo que el interior lógico `(1..N)` era suficiente para decidir si el chunk tenía superficie.

3. **Por qué falla:** El padding no es ruido; es la extensión necesaria para que Surface Nets cierre las celdas de borde. La superficie puede cruzar solo ahí.

4. **Momento más plausible:** Coincide con la introducción del muestreo multi-resolución y la lógica explícita de `mBool1`/`mBool2` en el bucle de `Sample(Chunk)`.

### 4.3 OldVersion

La OldVersion usa la misma condición en `DSFDensityGenerator.cs`. Si en OldVersion no aparecían agujeros, las causas probables son:

- Terreno sinusoidal más suave, con menos cruces de superficie en los bordes.
- Diferencias en el flujo de encolado (OldVersion encolaba siempre, sin filtrar por `mBool1 && mBool2` en ChunkPipeline).
- Uso de otro generador de malla o de otra ruta de render.

La corrección aplicada es válida en ambos contextos: la clasificación debe basarse en **todo** el dominio muestreado.

---

## 5. Resumen

| Aspecto | Detalle |
|---------|---------|
| **Causa** | Restricción `x > 0 && x <= N && y > 0 && y <= N && z > 0 && z <= N` en la clasificación `mBool1`/`mBool2` |
| **Efecto** | Chunks con superficie solo en el padding quedaban sin `isSurface` |
| **Consecuencia** | No se encolaban para render → agujeros en la geometría |
| **Corrección** | Clasificar usando todas las muestras (incluido el padding) |
| **Referencia** | RESUMEN_LOD_CORRECCIONES.md, sección 2.2 |
| **Estado** | Corregido — agujeros desaparecidos |
