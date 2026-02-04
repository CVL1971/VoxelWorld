
using UnityEngine;

public sealed class Chunk
{
    // =========================
    // Identidad
    // =========================

    public readonly Vector3Int mCoord;
    public readonly int mSize;
    public readonly Vector3Int mWorldOrigin;

    // =========================
    // Datos voxel
    // =========================

    public readonly VoxelData[] mVoxels;

    // =========================
    // Vista (opcional, externa)
    // =========================

    public GameObject mViewGO;

    // =========================
    // Constructor
    // =========================

    public Chunk(Vector3Int pCoord, int pSize)
    {
        mCoord = pCoord;
        mSize = pSize;

        mWorldOrigin = new Vector3Int(
            pCoord.x * pSize,
            pCoord.y * pSize,
            pCoord.z * pSize
        );

        mVoxels = new VoxelData[pSize * pSize * pSize];
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

    int Index(int x, int y, int z)
    {
        return x + mSize * (y + mSize * z);
    }

    bool InBounds(int x, int y, int z)
    {
        return !(x < 0 || y < 0 || z < 0 ||
                 x >= mSize || y >= mSize || z >= mSize);
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
}



















