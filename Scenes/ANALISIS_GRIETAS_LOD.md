# Análisis: Grietas en forma de L durante transiciones de LOD

## Síntomas

- Grietas en forma de **L** durante cambios de LOD
- Patrones tipo escaleras y estructuras
- Desaparecen cuando el terreno se reemplaza por completo
- **Aparecen incluso en terrenos planos** (donde no debería haber falta de precisión)
- Antes de la refactorización y del padding, eran menos frecuentes y se concentraban en zonas intraLOD

---

## Causas identificadas

### 1. **Orden de procesamiento distribuido (principal)**

`ConcurrentDictionary<Chunk, int>` no garantiza orden de iteración. Al hacer `foreach` sobre `mPendingResamples`, los chunks se procesan en un orden **aleatorio** en lugar de espacial.

**Efecto:** La zona en transición se actualiza de forma dispersa. En un frame se actualizan, por ejemplo, (5,0,5), (12,0,3), (7,0,8), etc. La frontera entre chunks ya actualizados y aún no actualizados forma patrones irregulares en L, escaleras y esquinas.

**Por qué empeora con el tiempo:** A medida que más chunks se actualizan, la frontera se va moviendo y las L se desplazan hasta que toda la zona está actualizada.

---

### 2. **EmitCorrectFaces: exclusión de caras en bordes**

En `SurfeceNetsGeneratorQEF3caches.EmitCorrectFaces`:

```csharp
// Cara +x: requiere y > 0 && z > 0
// Cara +y: requiere x > 0 && z > 0  
// Cara +z: requiere x > 0 && y > 0
```

Se omiten caras que usan vértices con índices `-1` (fuera del chunk). Para emitir en `(x, 0, z)` haría falta `vmap[x, -1, z-1]`, que no existe porque `vmap` solo cubre `[0..size]`.

**Efecto:** En las aristas del chunk (x=0, y=0, z=0) no se emiten caras. En las esquinas donde confluyen dos aristas se generan huecos en forma de L.

**Por qué en terreno plano:** Esos huecos están en las aristas de cada chunk, no en la superficie. En terreno plano, si la geometría cruza esas aristas, las L se hacen visibles.

---

### 3. **SurfaceNetsGeneratorQEF3caches no usa vecinos**

`SurfaceNetsGeneratorQEF3caches` solo usa la caché local (`mSample0/1/2`). No llama a `GetDensityGlobal` ni usa `allChunks`/`worldSize`.

**Comparación:** `SurfaceNetsGeneratorQEF` usa `GetDensityGlobal` y un `PAD` que incluye celdas vecinas, y emite caras en `xi=1..size+1` (incluyendo borde lógico 0).

**Efecto:** La versión con 3 caches es más rápida pero no consulta vecinos en tiempo de mallado. Los datos de borde vienen solo del SDF inicial. Si hay diferencias de LOD o de estado entre vecinos, no se reconcilian.

---

### 4. **Transición asíncrona LOD 0 ↔ LOD 1**

Durante la transición conviven chunks con distintos LOD:

- LOD 0: vértices cada 1 unidad (0, 1, 2, …, 32)
- LOD 1: vértices cada 2 unidades (0, 2, 4, …, 32)

En la frontera entre LOD 0 y LOD 1 pueden aparecer **T-junctions**: un chunk tiene vértices en (32, 17, 0) y el vecino solo en (32, 16, 0) y (32, 18, 0). El vértice intermedio queda “colgando” y puede generar grietas.

---

### 5. **GetDensityGlobal y vecinos en transición**

`GetDensityGlobal` (usado por otras variantes del generador) hace:

- Si el vecino tiene `mAwaitingResample` o el chunk actual es más fino → usa SDF
- Si no → lee del array del vecino

Con 3 caches, el mallador no usa `GetDensityGlobal`, así que esta lógica no afecta a la versión actual. Pero indica que la consistencia en bordes depende de que vecinos y LOD estén alineados.

---

## Soluciones propuestas

### Solución A: Orden de procesamiento espacial (prioridad alta)

**Objetivo:** Procesar chunks en un orden que reduzca fronteras irregulares.

**Implementación:**

1. Sustituir `ConcurrentDictionary` por una estructura que permita ordenar (por ejemplo, cola ordenada por distancia a cámara o por coordenadas).
2. Procesar por “anillos” o “ondas” desde la cámara hacia fuera.
3. Alternativa: ordenar por `mCoord` antes de procesar (p. ej. por distancia al centro de la zona en transición).

**Efecto esperado:** La frontera entre chunks actualizados y no actualizados se vuelve más suave (por ejemplo, un frente de onda) y se reducen las L y escaleras.

---

### Solución B: Procesamiento por regiones/batches (prioridad alta)

**Objetivo:** Actualizar juntos los chunks de una misma zona.

**Implementación:**

1. Agrupar chunks pendientes por región espacial (p. ej. cuadrícula de N×N chunks).
2. Procesar todos los chunks de una región antes de pasar a la siguiente.
3. Definir regiones por distancia a la cámara (anillos) o por cuadrícula espacial.

**Efecto esperado:** Menos mezcla de chunks nuevos y viejos en la misma frontera, menos grietas en L.

---

### Solución C: Incluir caras en bordes (prioridad media)

**Objetivo:** Eliminar huecos en aristas y esquinas de chunk.

**Implementación:**

1. Ampliar `vmap` a `[-1..size+1]` para incluir celdas de borde.
2. Para celdas con índice -1, obtener densidad vía `GetDensityGlobal` (vecinos o SDF).
3. Generar vértices en esas celdas y permitir que `EmitCorrectFaces` emita caras en x=0, y=0, z=0.

**Referencia:** `SurfaceNetsGeneratorQEF` ya usa un esquema similar con `PAD` y `GetDensityGlobal`.

**Efecto esperado:** Desaparecen los huecos geométricos en L en las aristas de los chunks.

---

### Solución D: Sincronizar LOD con vecinos (prioridad media)

**Objetivo:** Evitar fronteras LOD 0 / LOD 1 durante la transición.

**Implementación:**

1. Antes de cambiar LOD de un chunk, comprobar que todos sus vecinos 26-adyacentes estén listos (mismo LOD o sin `mAwaitingResample`).
2. Si algún vecino no está listo, posponer el cambio de LOD.
3. Opcional: procesar en orden de distancia a la cámara para que la transición sea más coherente.

**Efecto esperado:** Menos T-junctions y grietas en fronteras entre LOD distintos.

---

### Solución E: Usar GetDensityGlobal en bordes (3caches) (prioridad media)

**Objetivo:** Hacer que la versión con 3 caches consulte vecinos solo donde hace falta.

**Implementación:**

1. En `SurfaceNetsGeneratorQEF3caches`, para celdas de borde (x=-1, y=-1, z=-1 o x=size+1, etc.) leer densidad con `GetDensityGlobal` en lugar de la caché local.
2. Mantener la caché local para el interior del chunk.
3. Asegurar que `GetDensityGlobal` maneje bien vecinos en transición (`mAwaitingResample`, LOD distintos).

**Efecto esperado:** Bordes más coherentes entre chunks, sobre todo durante transiciones.

---

### Solución F: Buffer de transición (prioridad baja)

**Objetivo:** Evitar que se vea la frontera entre mallas viejas y nuevas.

**Implementación:**

1. Mantener la malla antigua hasta que todos los vecinos hayan actualizado.
2. O aplicar un fade/alpha en la zona de transición (más coste visual y de implementación).

**Efecto esperado:** Las grietas quedan ocultas durante la transición, a costa de más complejidad.

---

## Resumen de prioridades

| Prioridad | Solución | Impacto | Esfuerzo |
|-----------|----------|---------|----------|
| **Alta** | A. Orden espacial | Reduce L por orden aleatorio | Medio |
| **Alta** | B. Batches por región | Reduce L por distribución | Medio |
| **Media** | C. Caras en bordes | Elimina huecos geométricos | Alto |
| **Media** | D. Sincronizar vecinos | Reduce T-junctions | Medio |
| **Media** | E. GetDensityGlobal en bordes | Mejora consistencia de bordes | Medio |
| **Baja** | F. Buffer de transición | Oculta grietas | Alto |

---

## Recomendación

Empezar por **A** y **B** (orden y batches), que atacan la causa principal (orden de procesamiento) con esfuerzo moderado. Si persisten grietas, aplicar **C** o **E** para corregir la geometría en los bordes.
