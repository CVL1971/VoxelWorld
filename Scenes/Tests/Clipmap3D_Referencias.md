# Referencias: extensión analítica del clipmap a 3D

## Resumen

El paper original (Losasso & Hoppe 2004) trabaja con heightmaps (dominio 2D). La extensión a volúmenes 3D no está formalizada en una fórmula analítica explícita en la literatura pública, pero hay fuentes que describen el enfoque.

---

## 1. John Whigham – Voxelus (clipmap 3D para planetas)

**Fuente:** [Goodbye Octrees](https://johnwhigham.blogspot.com/2013/06/goodbye-octrees.html)

- Sustituye octrees por clipmaps para voxels planetarios.
- **Relación de volumen:** cada nivel encierra **1/8 del volumen** del nivel anterior.
- Implica que la escala lineal se reduce a la mitad entre niveles: `scale(l+1) = scale(l) / 2`.
- Cada nivel usa una malla fija (p. ej. 19³ bricks) que representa un volumen distinto.
- Niveles centrados en el observador.
- Menciona una extensión a **4D**: (x, y, z) espaciales + eje W para el nivel de detalle al moverse el observador.

---

## 2. Procedural World (Miguel Cepero) – Voxel Farm

**Fuente:** [Clipmaps](https://procworld.blogspot.com/2011/10/clipmaps.html)

- Clipmaps como anillos concéntricos alrededor del observador.
- Cada anillo tiene cantidad de información similar, pero el tamaño crece con la distancia.
- En 3D: usa octree; las celdas se organizan alrededor de un punto de forma que se subdividen más al acercarse.
- **Fórmula implícita:** nivel de detalle según distancia al observador.
- Los anillos pueden ser cuadrados, esféricos o piramidales; los esféricos optimizan información vs tamaño en pantalla.

---

## 3. 0 FPS – LOD para voxels tipo Minecraft

**Fuente:** [A level of detail method for blocky voxels](https://0fps.net/2018/03/03/a-level-of-detail-method-for-blocky-voxels/)

- Usa POP buffers (vertex clustering) en lugar de clipmaps.
- **Fórmula de LOD:** `lod = bias - log2(viewDist)` con `viewDist` = distancia al vértice.
- Nivel por distancia al observador.
- Geomorphing para transiciones suaves entre niveles.

---

## 4. Fórmula analítica sugerida para 3D

A partir de estas fuentes, una extensión analítica coherente sería:

```
d = max(|rx|, |ry|, |rz|)     // Distancia L∞ (Chebyshev) en 3D
level = floor(log2(d))       // Nivel por distancia
scale = 2^level
cell_size = base × scale
```

- **Centro:** observador en (0,0,0).
- **Niveles:** cubos concéntricos; cada nivel tiene 1/8 del volumen del siguiente (escala ×2 en cada eje).
- **Render region:** cáscara = `active(l) − active(l+1)` en 3D.

La diferencia con el apilamiento: no hay capas 2D repetidas en Y, sino una única estructura 3D donde el nivel se determina por la distancia 3D al centro.

---

## 5. Limitación

No aparece una fórmula cerrada y publicada que extienda el clipmap 2D a 3D de forma explícita. Los enfoques prácticos (Voxel Farm, Voxelus) parten del concepto de anillos/cubos concéntricos y distancia al observador, pero sin una derivación formal equivalente al paper original en 2D.
