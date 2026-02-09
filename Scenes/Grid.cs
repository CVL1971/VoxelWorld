using UnityEngine;
using System.Collections.Generic;

public class Grid
{
    public readonly Chunk[] mChunks;
    public readonly Vector3Int mSizeInChunks;
    public readonly int mChunkSize;
    public GameObject mWorldRoot;

    public Grid(Vector3Int pSizeInChunks, int pChunkSize)
    {
        mSizeInChunks = pSizeInChunks;
        mChunkSize = pChunkSize;

        EmptyChunksInstances();

        int chunkCount =
           mSizeInChunks.x *
           mSizeInChunks.y *
           mSizeInChunks.z;

        mChunks = new Chunk[chunkCount];

        for (int z = 0; z < mSizeInChunks.z; z++)
            for (int y = 0; y < mSizeInChunks.y; y++)
                for (int x = 0; x < mSizeInChunks.x; x++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    int index = ChunkIndex(x, y, z);
                    mChunks[index] = new Chunk(coord, mChunkSize);
                }

    }

    public void ReadFromSDFGenerator() {

        // 1. Calcula el ruido Perlin 2D, densidades y solidez
        foreach (Chunk chunk in mChunks) SDFGenerator.Sample(chunk);
     
}

    int ChunkIndex(int x, int y, int z)
    {
        return x +
               mSizeInChunks.x *
               (y + mSizeInChunks.y * z);
    }

    public void EmptyChunksInstances()
    {

        if (mWorldRoot == null)
        {
            // Solo se crea si no existe
            mWorldRoot = new GameObject("WorldRoot");
            mWorldRoot.transform.position = Vector3.zero;
        }
        else
        {
            // Si ya existe, eliminamos a los hijos para dejarlo limpio
            foreach (Transform child in mWorldRoot.transform)
            {
                Object.Destroy(child.gameObject);
            }
        }

    }

    public HashSet<int> ModifyWorld(VoxelBrush pBrush)
    {
        HashSet<int> vAffectedChunks = new HashSet<int>();

        // 1. Calculamos el área de influencia en voxeles globales
        float vRadius = pBrush.mRadius + pBrush.mK;
        Vector3Int vMin = new Vector3Int(
            Mathf.FloorToInt(pBrush.mCenter.x - vRadius),
            Mathf.FloorToInt(pBrush.mCenter.y - vRadius),
            Mathf.FloorToInt(pBrush.mCenter.z - vRadius)
        );
        Vector3Int vMax = new Vector3Int(
            Mathf.CeilToInt(pBrush.mCenter.x + vRadius),
            Mathf.CeilToInt(pBrush.mCenter.y + vRadius),
            Mathf.CeilToInt(pBrush.mCenter.z + vRadius)
        );

        // 2. Iteramos solo sobre los voxeles del pincel (Rápido)
        for (int vz = vMin.z; vz <= vMax.z; vz++)
            for (int vy = vMin.y; vy <= vMax.y; vy++)
                for (int vx = vMin.x; vx <= vMax.x; vx++)
                {
                    // 3. Conversión de Global a Chunk usando vx, vy, vz (CORREGIDO)
                    int vCx = vx / mChunkSize;
                    int vCy = vy / mChunkSize;
                    int vCz = vz / mChunkSize; // Ahora usa vz correctamente

                    if (!VoxelUtils.IsInBounds(vCx, vCy, vCz, mSizeInChunks)) continue;

                    int vCIdx = VoxelUtils.GetChunkIndex(vCx, vCy, vCz, mSizeInChunks);
                    Chunk vChunk = mChunks[vCIdx];

                    // 4. Conversión a coordenadas locales del Chunk
                    int vLx = vx - (vCx * mChunkSize);
                    int vLy = vy - (vCy * mChunkSize);
                    int vLz = vz - (vCz * mChunkSize);

                    // 5. Aplicación del pincel
                    Vector3 vPos = new Vector3(vx, vy, vz);
                    float vCurrentD = vChunk.GetDensity(vLx, vLy, vLz);
                    float vNewD = pBrush.CalculateDensity(vPos, vCurrentD);

                    vChunk.SetDensity(vLx, vLy, vLz, Mathf.Clamp01(vNewD));
                    vChunk.SetSolid(vLx, vLy, vLz, vNewD > 0.5f ? (byte)1 : (byte)0);

                    vAffectedChunks.Add(vCIdx);
                }

        return vAffectedChunks;
    }


}