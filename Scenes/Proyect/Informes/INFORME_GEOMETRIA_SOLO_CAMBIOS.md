# Primera intervención – Geometría cubo central: solo código modificado o añadido

Listado explícito de lo que se **añadió** o **alteró**. El resto del archivo no se tocó.

---

## Archivo: `Chunk.cs`

### 1. AÑADIDO – Propiedad `WorldOrigin`

**Ubicación:** después del constructor, antes de `DeclareSampleArray()`.

**Código añadido:**

```csharp
    public Vector3Int WorldOrigin
    {
        get
        {
            mWorldOrigin.x = (mGrid.mXOffset + mCoord.x) * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
            mWorldOrigin.y = (mGrid.mYOffset + mCoord.y) * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
            mWorldOrigin.z = (mGrid.mZOffset + mCoord.z) * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
            return mWorldOrigin;
        }
    }
```

*(Si ya existía un getter `WorldOrigin` que no usaba los offsets del grid, entonces se **sustituyó** el cuerpo del get por el de arriba.)*

---

### 2. MODIFICADO – Constructor

**Línea afectada:** asignación de `mWorldOrigin` dentro del constructor.

- **Antes (ejemplo típico del bug):**  
  `mWorldOrigin` se calculaba solo con la coordenada del chunk, **sin** los offsets del grid, por ejemplo:
  ```csharp
  mWorldOrigin = new Vector3Int(mCoord.x * pSize, mCoord.y * pSize, mCoord.z * pSize);
  ```
  o similar, sin `mGrid.mXOffset`, `mYOffset`, `mZOffset`.

- **Ahora:**
  ```csharp
  mWorldOrigin = Vector3Int.zero;
  ```
  El valor real lo calcula el getter de `WorldOrigin` usando el grid.

---

### 3. MODIFICADO – `PrepareView`

**Línea afectada:** asignación de `mViewGO.transform.position`.

- **Antes:**
  ```csharp
  mViewGO.transform.position = (Vector3)mWorldOrigin;
  ```
  (o cualquier expresión que no usara el origen calculado con offsets del grid)

- **Ahora:**
  ```csharp
  mViewGO.transform.position = (Vector3)WorldOrigin;
  ```

---

### 4. MODIFICADO – `ApplyBrush`

**Línea afectada:** cálculo de `vWorldPos`.

- **Antes:**
  ```csharp
  Vector3 vWorldPos = (Vector3)mWorldOrigin + new Vector3(x, y, z) * vStep;
  ```

- **Ahora:**
  ```csharp
  Vector3 vWorldPos = (Vector3)WorldOrigin + new Vector3(x, y, z) * vStep;
  ```

---

### 5. MODIFICADO – `DrawDebug`

**Línea afectada:** cálculo de `min` del cubo de debug.

- **Antes:**
  ```csharp
  Vector3 min = (Vector3)mWorldOrigin;
  ```

- **Ahora:**
  ```csharp
  Vector3 min = (Vector3)WorldOrigin;
  ```

---

## Resumen en una línea por cambio

| Dónde | Tipo | Cambio |
|-------|------|--------|
| Chunk.cs | Añadido | Propiedad `WorldOrigin` (get) que calcula origen con `mGrid.mXOffset/mYOffset/mZOffset` y `mCoord`. |
| Chunk.cs – constructor | Modificado | `mWorldOrigin = Vector3Int.zero` (el valor lo da el getter). |
| Chunk.cs – PrepareView | Modificado | `mViewGO.transform.position = (Vector3)WorldOrigin`. |
| Chunk.cs – ApplyBrush | Modificado | `vWorldPos = (Vector3)WorldOrigin + ...`. |
| Chunk.cs – DrawDebug | Modificado | `Vector3 min = (Vector3)WorldOrigin`. |

**Ningún otro archivo se modificó en esta primera intervención.**  
*(Vigilante y otros ya usaban `WorldOrigin` o equivalentes; no se cambiaron en esa intervención.)*
