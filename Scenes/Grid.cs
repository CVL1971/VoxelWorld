using UnityEngine;
using System.Collections.Generic;

public class Grid
{
    private readonly Chunk[] mChunks;
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

// Aquí vive la lógica que matará al adaptador
public float GetDensityGlobal(Chunk currentChunk, int x, int y, int z)
    {
        // ... (Lógica de gx, gy, gz usando baseChunkSize para cx, cy, cz)
        return 0f;
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
}