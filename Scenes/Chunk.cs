
using UnityEngine;

public sealed class Chunk
{
    // =========================================================
    // IDENTIDAD Y ESTADO
    // =========================================================
    public readonly Vector3Int mCoord;
    public readonly Grid mGrid;
    private Vector3Int mWorldOrigin;
    public readonly int mIndex;
    public Vector3Int mGlobalCoord;
    public int mGenerationId;

    /// <summary> Resolución actual del chunk (32, 16 u 8). </summary>
    public int mSize;
    /// <summary> Resolución objetivo para la próxima actualización de LOD. </summary>

    public bool mIsEdited = false;
 
    // Bools de estado para optimización de visibilidad
    public bool mBool1 = false; // Flag: Contiene geometría sólida
    public bool mBool2 = false; // Flag: Contiene aire/vacío

    // =========================================================
    // CACHÉ DE CAMPO DE DENSIDADES (Fuente única de verdad)
    // =========================================================
    // Cada array tiene un padding de +2 (1 unidad por cada lado de los 6 ejes)
    // Esto permite al Mesher ser 100% autónomo.
    public float[] mSample0; // LOD 0: (32+2)^3
    public float[] mSample1; // LOD 1: (16+2)^3
    public float[] mSample2; // LOD 2: (8+2)^3

    public GameObject mViewGO;

    // Umbral de superficie (Iso-surface)
    public const float ISO_LEVEL = 0.5f;

    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    public Chunk(Vector3Int pCoord, int pSize, Grid pGrid)
    {
        mCoord = pCoord;
        mSize = pSize;
        mGrid = pGrid;

        mIndex = mGrid.ChunkIndex(pCoord.x, pCoord.y, pCoord.z);

        // El origen mundial se basa siempre en el tamaño universal (32) 
        // para que los chunks no se muevan al cambiar de LOD.
        mWorldOrigin = Vector3Int.zero;


        DeclareSampleArray();
    }

    public Vector3Int WorldOrigin
    {
        get
        {
            return mGlobalCoord * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        }
    }


    public void DeclareSampleArray()
    {
        // Padding +2 por cara: posiciones -1 a size+1 (necesario para geometría de fronteras entre chunks)
        // Res0 = 35, Res1 = 19, Res2 = 11
        int res0 = VoxelUtils.LOD_DATA[0] + 3;
        int res1 = VoxelUtils.LOD_DATA[4] + 3;
        int res2 = VoxelUtils.LOD_DATA[8] + 3;

        mSample0 = new float[res0 * res0 * res0];
        mSample1 = new float[res1 * res1 * res1];
        mSample2 = new float[res2 * res2 * res2];
    }

    // =========================================================
    // INDEXACIÓN Y DIRECCIONAMIENTO
    // =========================================================

    /// <summary>
    /// Calcula el índice 1D aplicando el offset de padding (+1).
    /// </summary>
    public int IndexSample(int x, int y, int z, int resWithPadding)
    {
        // El +1 mapea el rango lógico [-1, Size] al rango del array [0, Size+1]
        return (x + 1) + resWithPadding * ((y + 1) + resWithPadding * (z + 1));
    }

    private float[] GetActiveCache()
    {
        if (mSize == VoxelUtils.LOD_DATA[0]) return mSample0;
        if (mSize == VoxelUtils.LOD_DATA[4]) return mSample1;
        return mSample2;
    }

    // =========================================================
    // INTERFACES DE DENSIDAD
    // =========================================================

    public float GetDensity(int x, int y, int z)
    {
        float[] cache = GetActiveCache();
        int p = mSize + 3;
        return cache[IndexSample(x, y, z, p)];
    }

    public void SetDensity(int x, int y, int z, float pDensity)
    {
        float[] cache = GetActiveCache();
        int p = mSize + 3;
        cache[IndexSample(x, y, z, p)] = pDensity;
        // mIsEdited solo se marca en ModifyWorld/ApplyBrush (edición usuario), no en SDFGenerator
    }

    public bool IsSolid(int x, int y, int z)
    {
        // El estado sólido ahora se deriva dinámicamente de la densidad
        return GetDensity(x, y, z) >= ISO_LEVEL;
    }

    public bool SafeIsSolid(int x, int y, int z)
    {
        // Fuera de los límites del chunk tratamos todo como aire (false)
        if (x < 0 || x >= mSize || y < 0 || y >= mSize || z < 0 || z >= mSize)
            return false;

        return IsSolid(x, y, z);
    }

    // =========================================================
    // EDICIÓN (BRUSH)
    // =========================================================
    public void ApplyBrush(VoxelBrush pBrush)
    {
        mIsEdited = true;
        int p = mSize + 3;
        float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / mSize;

        for (int z = 0; z < mSize; z++)
            for (int y = 0; y < mSize; y++)
                for (int x = 0; x < mSize; x++)
                {
                    Vector3 vWorldPos = (Vector3)WorldOrigin + new Vector3(x, y, z) * vStep;
                    float vDistThreshold = pBrush.mRadius + pBrush.mK * 2f;

                    if (Vector3.Distance(vWorldPos, pBrush.mCenter) <= vDistThreshold)
                    {
                        float vCurrentD = GetDensity(x, y, z);
                        float vNewD = pBrush.CalculateDensity(vWorldPos, vCurrentD);
                        SetDensity(x, y, z, Mathf.Clamp01(vNewD));
                    }
                }
    }

    // =========================================================
    // GESTIÓN DE VISTA Y MEMORIA
    // =========================================================
    public void PrepareView(Transform worldRoot, Material surfaceMaterial)
    {
        if (mViewGO == null)
        {
            mViewGO = new GameObject("Chunk_" + mCoord, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            mViewGO.transform.parent = worldRoot;
            mViewGO.transform.position = (Vector3)WorldOrigin;
            mViewGO.GetComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }
    }

    public void Redim(int pNewSize)
    {
        // Al no haber mVoxels, el redimensionado es un simple cambio de puntero de resolución.
        // El sistema de renderizado usará automáticamente el array de caché correspondiente.
        mSize = pNewSize;
    }

    public void ResetGenericBools()
    {
        mBool1 = false;
        mBool2 = false;
    }

    public void OnDestroy()
    {
        // Liberamos los arrays para el GC
        mSample0 = mSample1 = mSample2 = null;

        if (mViewGO != null)
        {
            MeshFilter filter = mViewGO.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                Object.Destroy(filter.sharedMesh);

            Object.Destroy(mViewGO);
            mViewGO = null;
        }
    }

    // =========================================================
    // DEBUG VISUAL
    // =========================================================
    public void DrawDebug(Color pColor, float pduration)
    {
        // Usamos siempre el tamaño universal para el dibujo del cubo debug
        Vector3 min = (Vector3)WorldOrigin;
        float s = VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        Vector3 max = min + new Vector3(s, s, s);

        // Dibujado de las 12 aristas del cubo
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), pColor, pduration);

        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), pColor, pduration);

        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), pColor, pduration);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), pColor, pduration);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), pColor, pduration);
    }
}

















