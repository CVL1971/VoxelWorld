
using UnityEngine;

public sealed class Chunk
{
    // =========================
    // Identidad
    // =========================

    public readonly Vector3Int mCoord;
    public int mSize;
    public readonly Grid mGrid;
    public readonly Vector3Int mWorldOrigin;
    /// <summary> 0 = sin marcar. Si &gt; 0, resolución LOD deseada (32/16/8); marca la cascada Redim → Sample → Remesh. </summary>
    public int mTargetSize = 0;
    public bool mIsEdited = false;
    /// <summary> True mientras el chunk está en la cola de resample del DecimationManager.
    /// GetDensityGlobal/IsSolidGlobal usan SDF procedural en lugar del array para evitar grietas. </summary>
    public bool mAwaitingResample = false;
    public bool mBool1 = false;
    public bool mBool2 = false;
    public int mIndex; //Indice para localizar al chunk en el array del grid de datos.
    public VoxelData[] mVoxels;
    public GameObject mViewGO;
    // Caché de densidades (LODs con padding de 1)
    // Tamaños: (32+2)^3, (16+2)^3, (8+2)^3
    public float[] mSample0; // LOD 0 (Res 32)
    public float[] mSample1; // LOD 1 (Res 16)
    public float[] mSample2; // LOD 2 (Res 8)

    public Chunk(Vector3Int pCoord, int pSize, Grid pGrid)
    {
        mCoord = pCoord;
        mSize = pSize;
        mGrid = pGrid;

        // Usamos la fórmula oficial del padre para calcular nuestra posición
        mIndex = mGrid.ChunkIndex(pCoord.x, pCoord.y, pCoord.z);

        mWorldOrigin = new Vector3Int(
            pCoord.x * pSize,
            pCoord.y * pSize,
            pCoord.z * pSize
        );

        mVoxels = VoxelArrayPool.Get(mSize);
        DeclareSampleArray();

    }

    public void DeclareSampleArray()
    {
        // Usamos las constantes de VoxelUtils para las resoluciones
        int res0 = VoxelUtils.LOD_DATA[0] + 2;
        int res1 = VoxelUtils.LOD_DATA[4] + 2;
        int res2 = VoxelUtils.LOD_DATA[8] + 2;

        mSample0 = new float[res0 * res0 * res0];
        mSample1 = new float[res1 * res1 * res1];
        mSample2 = new float[res2 * res2 * res2];
    }

    // Helper para indexar con el nuevo tamaño (mSize + 2)
    public int IndexSample(int x, int y, int z, int resWithPadding)
    {
        return x + resWithPadding * (y + resWithPadding * z);
    }

    public void PrepareView(Transform worldRoot, Material surfaceMaterial)
    {
        if (mViewGO == null)
        {
            mViewGO = new GameObject("Chunk_" + mCoord, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            mViewGO.transform.parent = worldRoot;
            mViewGO.transform.position = (Vector3)mWorldOrigin;
            mViewGO.GetComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }
    }

    public void OnDestroy() // O cuando el chunk se desactiva
    {
        if (mVoxels != null)
        {
            VoxelArrayPool.Return(mVoxels);
            mVoxels = null;

            //destroy mViewGO Logic

            // 2. Limpiar los objetos de Unity (GPU/Escena)
            if (mViewGO != null)
            {
                // Importante: Extraemos la malla antes de destruir el GO
                MeshFilter filter = mViewGO.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    // DESTROZAR la malla de la memoria de vídeo (VRAM)
                    // Si no haces esto, cada vez que destruyas un chunk perderás megas de VRAM
                    Object.Destroy(filter.sharedMesh);
                }

                MeshCollider collider = mViewGO.GetComponent<MeshCollider>();
                if (collider != null && collider.sharedMesh != null)
                {
                    // A veces el collider comparte la malla, pero por seguridad
                    // nos aseguramos de que la referencia se limpie.
                    collider.sharedMesh = null;
                }

                // Finalmente destruimos el contenedor en la escena
                Object.Destroy(mViewGO);
                mViewGO = null;
            }
        }
    }

    public void Redim(int pSize)
    {
        VoxelArrayPool.Return(mVoxels);
        // Pedimos el nuevo
        mVoxels = VoxelArrayPool.Get(pSize);
        mSize = pSize;

        //inconsistency nViewGO logic
    }

    public void ResetGenericBools()
    {
        mBool1 = false;
        mBool2 = false;
    }

    public void ApplyBrush(VoxelBrush pBrush)
    {
        // mSize es el tamaño del array (ej. 128)
        for (int vz = 0; vz < mSize; vz++)
            for (int vy = 0; vy < mSize; vy++)
                for (int vx = 0; vx < mSize; vx++)
                {
                    // Suma vectorial directa: Origen del Chunk + coordenadas locales
                    Vector3 vWorldPos = new Vector3(
                        mWorldOrigin.x + vx,
                        mWorldOrigin.y + vy,
                        mWorldOrigin.z + vz
                    );

                    // El radio de influencia suele incluir un margen para el factor k
                    float vDistThreshold = pBrush.mRadius + pBrush.mK * 2f;

                    // Usamos sqrMagnitude si quisiéramos optimizar más, 
                    // pero para seguir tu lógica actual usamos Distance
                    if (Vector3.Distance(vWorldPos, pBrush.mCenter) <= vDistThreshold)
                    {
                        float vCurrentD = GetDensity(vx, vy, vz);
                        float vNewD = pBrush.CalculateDensity(vWorldPos, vCurrentD);

                        SetDensity(vx, vy, vz, Mathf.Clamp01(vNewD));
                        // Actualizamos el estado sólido basándonos en el umbral
                        SetSolid(vx, vy, vz, vNewD > 0.5f ? (byte)1 : (byte)0);
                    }
                }
    }

    public float DensityAt(int x, int y, int z)
    {
        // Usamos InBounds (que ya tienes definido) para verificar si el punto está dentro
        if (!InBounds(x, y, z))
        {
            return 0.0f; // Si está fuera del chunk, devolvemos aire
        }

        // Usamos Index(x, y, z) que ya tienes definido para obtener el voxel correcto
        return mVoxels[Index(x, y, z)].density;
    }

    // =========================
    // Indexación
    // =========================

    public int Index(int x, int y, int z)
    {
        return x + mSize * (y + mSize * z);
    }

    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < mSize &&
        y >= 0 && y < mSize &&
        z >= 0 && z < mSize;
    }

    // =========================
    // LECTURA (estricta)
    // =========================

    public bool IsSolid(int x, int y, int z)
    {
        return mVoxels[Index(x, y, z)].solid != 0;
    }

    public byte IsSolid(int index)
    {
        return mVoxels[index].solid; // 0 = aire, 1 = sólido
    }

    public float GetDensity(int x, int y, int z)
    {
        if (!InBounds(x, y, z))
        {
            throw new System.Exception("Chunk.getDensity(int,int,int) out of bonds");
            return 0.0f;
        }
      

       return mVoxels[Index(x, y, z)].density;
    }


    // =========================
    // LECTURA CON 3 ESTADOS
    // 0 = Aire
    // 1 = Sólido
    // 2 = Inexistente (fuera del dominio)
    // =========================

    //public byte SafeIsSolid(int x, int y, int z)
    //{
    //    if (x < 0 || y < 0 || z < 0 ||
    //        x >= mSize || y >= mSize || z >= mSize)
    //        return 2; // inexistente

    //    return mVoxels[Index(x, y, z)].solid != 0 ? (byte)1 : (byte)0;
    //}

    // =========================
    // LECTURA PARA MESHING
    // Política integrada:
    //  - dentro + sólido -> true
    //  - dentro + aire   -> false
    //  - fuera           -> false (tratado como aire)
    // =========================

    public bool SafeIsSolid(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 ||
            x >= mSize || y >= mSize || z >= mSize)
            return false;

        return mVoxels[Index(x, y, z)].solid != 0;
    }

    // =========================
    // ESCRITURA
    // =========================

    public void SetSolid(int x, int y, int z, byte pSolid)
    {
        if (!InBounds(x, y, z))
            return;

        mVoxels[Index(x, y, z)].solid = pSolid;
    }

    public void SetDensity(int x, int y, int z, float pDensity)
    {
        if (!InBounds(x, y, z))
            return;

        mVoxels[Index(x, y, z)].density = pDensity;
    }

    // =========================
    // UTILIDADES
    // =========================

    public void SetEmpty()
    {
        for (int i = 0; i < mVoxels.Length; i++)
            mVoxels[i].solid = 0;
    }

    public void SetFull()
    {
        for (int i = 0; i < mVoxels.Length; i++)
            mVoxels[i].solid = 1;
    }

    // =========================
    // DEBUG VISUAL
    // =========================

    public void DrawDebug(Color pColor, float pduration)
    {
        // Calculamos las esquinas basadas en el origen global y el tamaño
        Vector3 min = mWorldOrigin;
        Vector3 max = mWorldOrigin + new Vector3(mSize, mSize, mSize);

        // Base (Y inferior)
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), pColor, pduration);

        // Techo (Y superior)
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), pColor, pduration);

        // Columnas verticales (unión Base-Techo)
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), pColor, pduration);
    }


}



















