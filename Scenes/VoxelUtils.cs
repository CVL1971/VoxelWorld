using UnityEngine;
using System.Collections.Generic;

public static class VoxelUtils
{
    public const int UNIVERSAL_CHUNK_SIZE = 32;


    /// <summary>
    /// Estructura para transportar la información de ubicación de un voxel.
    /// </summary>
    public struct VoxelHit
    {
        public bool isValid;
        public int chunkIndex;
        public Vector3Int localPos;
        public Vector3Int globalVoxelPos;
    }

    // ==========================================================
    // MÉTODOS DE CONVERSIÓN CONVENCIONALES
    // ==========================================================

    public static int PosToVoxel(float position)
    {
        return Mathf.FloorToInt(position);
    }

    public static int VoxelToChunk(int voxelCoord, int chunkSize)
    {
        return voxelCoord / chunkSize;
    }

    public static int VoxelToLocal(int voxelCoord, int chunkSize, int chunkCoord)
    {
        return voxelCoord - (chunkCoord * chunkSize);
    }



    // ==========================================================
    // SOBRECARGAS PARA GENERADORES (ÚNICO PUNTO DE ACCESO)
    // ==========================================================
    // ==========================================================
    // SOBRECARGAS PARA GENERADORES (ÚNICO PUNTO DE ACCESO)
    // ==========================================================

    public static float GetDensityGlobal(Chunk currentChunk, Chunk[] allChunks, Vector3Int worldSize, float x, float y, float z)
    {
        float gx = currentChunk.mWorldOrigin.x + x;
        float gy = currentChunk.mWorldOrigin.y + y;
        float gz = currentChunk.mWorldOrigin.z + z;

        int cx = Mathf.FloorToInt(gx / UNIVERSAL_CHUNK_SIZE);
        int cy = Mathf.FloorToInt(gy / UNIVERSAL_CHUNK_SIZE);
        int cz = Mathf.FloorToInt(gz / UNIVERSAL_CHUNK_SIZE);

        if (!IsInBounds(cx, cy, cz, worldSize)) return 0.0f;

        Chunk target = allChunks[GetChunkIndex(cx, cy, cz, worldSize)];
        if (target == null) return 0.0f;

        // Usamos mSize para asegurar coherencia con el array de datos actual (mVoxels)
        int targetRes = target.mSize <= 0 ? UNIVERSAL_CHUNK_SIZE : target.mSize;

        int lodIdx = GetInfoRes(targetRes);
        float targetStep = LOD_DATA[lodIdx + 1];

        float localX = gx - target.mWorldOrigin.x;
        float localY = gy - target.mWorldOrigin.y;
        float localZ = gz - target.mWorldOrigin.z;

        int idxX = Mathf.Clamp(Mathf.RoundToInt(localX / targetStep), 0, targetRes - 1);
        int idxY = Mathf.Clamp(Mathf.RoundToInt(localY / targetStep), 0, targetRes - 1);
        int idxZ = Mathf.Clamp(Mathf.RoundToInt(localZ / targetStep), 0, targetRes - 1);

        // Llamamos a DensityAt que accede correctamente a mVoxels[index].density
        return target.DensityAt(idxX, idxY, idxZ);
    }

    public static int GetChunkIndex(int cx, int cy, int cz, Vector3Int worldChunkSize)
    {
        return cx + worldChunkSize.x * (cy + worldChunkSize.y * cz);
    }

    public static bool IsSolidGlobal(Chunk currentChunk, Chunk[] allChunks, Vector3Int worldSize, float x, float y, float z)
    {
        float gx = currentChunk.mWorldOrigin.x + x;
        float gy = currentChunk.mWorldOrigin.y + y;
        float gz = currentChunk.mWorldOrigin.z + z;

        int cx = Mathf.FloorToInt(gx / UNIVERSAL_CHUNK_SIZE);
        int cy = Mathf.FloorToInt(gy / UNIVERSAL_CHUNK_SIZE);
        int cz = Mathf.FloorToInt(gz / UNIVERSAL_CHUNK_SIZE);

        if (!IsInBounds(cx, cy, cz, worldSize)) return false;

        Chunk target = allChunks[GetChunkIndex(cx, cy, cz, worldSize)];
        if (target == null) return false;

        int targetRes = target.mSize <= 0 ? UNIVERSAL_CHUNK_SIZE : target.mSize;

        int lodIdx = GetInfoRes(targetRes);
        float targetStep = LOD_DATA[lodIdx + 1];

        float localX = gx - target.mWorldOrigin.x;
        float localY = gy - target.mWorldOrigin.y;
        float localZ = gz - target.mWorldOrigin.z;

        int idxX = Mathf.Clamp(Mathf.RoundToInt(localX / targetStep), 0, targetRes - 1);
        int idxY = Mathf.Clamp(Mathf.RoundToInt(localY / targetStep), 0, targetRes - 1);
        int idxZ = Mathf.Clamp(Mathf.RoundToInt(localZ / targetStep), 0, targetRes - 1);

        return target.SafeIsSolid(idxX, idxY, idxZ);
    }

    public static bool IsInBounds(int cx, int cy, int cz, Vector3Int worldChunkSize)
    {
        if (cx < 0 || cx >= worldChunkSize.x) return false;
        if (cy < 0 || cy >= worldChunkSize.y) return false;
        if (cz < 0 || cz >= worldChunkSize.z) return false;
        return true;
    }

    // ==========================================================
    // LÓGICA DE LOCALIZACIÓN
    // ==========================================================

    public static VoxelHit GetHitInfo(Vector3 worldPos, int chunkSize, Vector3Int worldChunkSize)
    {
        VoxelHit hit = new VoxelHit();

        hit.globalVoxelPos = new Vector3Int(
            PosToVoxel(worldPos.x),
            PosToVoxel(worldPos.y),
            PosToVoxel(worldPos.z)
        );

        int cx = VoxelToChunk(hit.globalVoxelPos.x, chunkSize);
        int cy = VoxelToChunk(hit.globalVoxelPos.y, chunkSize);
        int cz = VoxelToChunk(hit.globalVoxelPos.z, chunkSize);

        if (!IsInBounds(cx, cy, cz, worldChunkSize))
        {
            hit.isValid = false;
            return hit;
        }

        hit.isValid = true;
        hit.chunkIndex = GetChunkIndex(cx, cy, cz, worldChunkSize);
        hit.localPos = new Vector3Int(
            VoxelToLocal(hit.globalVoxelPos.x, chunkSize, cx),
            VoxelToLocal(hit.globalVoxelPos.y, chunkSize, cy),
            VoxelToLocal(hit.globalVoxelPos.z, chunkSize, cz)
        );

        return hit;
    }

    public static List<int> GetAffectedChunkIndices(Vector3Int globalV, int size, Vector3Int wSize)
    {
        HashSet<int> indices = new HashSet<int>();

        int cx = VoxelToChunk(globalV.x, size);
        int cy = VoxelToChunk(globalV.y, size);
        int cz = VoxelToChunk(globalV.z, size);

        if (IsInBounds(cx, cy, cz, wSize))
            indices.Add(GetChunkIndex(cx, cy, cz, wSize));

        int lx = VoxelToLocal(globalV.x, size, cx);
        int ly = VoxelToLocal(globalV.y, size, cy);
        int lz = VoxelToLocal(globalV.z, size, cz);

        if (lx == 0 && cx > 0) indices.Add(GetChunkIndex(cx - 1, cy, cz, wSize));
        if (lx == size - 1 && cx < wSize.x - 1) indices.Add(GetChunkIndex(cx + 1, cy, cz, wSize));

        if (ly == 0 && cy > 0) indices.Add(GetChunkIndex(cx, cy - 1, cz, wSize));
        if (ly == size - 1 && cy < wSize.y - 1) indices.Add(GetChunkIndex(cx, cy + 1, cz, wSize));

        if (lz == 0 && cz > 0) indices.Add(GetChunkIndex(cx, cy, cz - 1, wSize));
        if (lz == size - 1 && cz < wSize.z - 1) indices.Add(GetChunkIndex(cx, cy, cz + 1, wSize));

        return new List<int>(indices);
    }

    // [0] Resolución | [1] Paso | [2] DistanciaSq | [3] LOD_Index
    public static readonly float[] LOD_DATA =
    {
        32f, 1.0f, 9216f,   0f,
        16f, 2.0f, 65536f,  1f,
        8f,  4.0f, 1000000f, 2f
    };

    public static int GetInfoDist(float pDistSq)
    {
        if (pDistSq < LOD_DATA[2]) return 0;
        if (pDistSq < LOD_DATA[6]) return 4;
        return 8;
    }

    public static int GetInfoRes(int pRes)
    {
        if (pRes == (int)LOD_DATA[0]) return 0;
        if (pRes == (int)LOD_DATA[4]) return 4;
        return 8;
    }

    public static int GetInfoSize(int pSize)
    {
        if (pSize == (int)LOD_DATA[0]) return 0;
        if (pSize == (int)LOD_DATA[4]) return 4;
        return 8;
    }

    public static int GetInfoLod(int pLod) => pLod * 4;

    public static Vector3 GetChunkCenter(Vector3 pOriginOriginal, float pSizeOriginal)
    {
        float vHalf = pSizeOriginal * 0.5f;
        return new Vector3(pOriginOriginal.x + vHalf, pOriginOriginal.y + vHalf, pOriginOriginal.z + vHalf);
    }

    public static Vector3 GetGridCenter(Vector3Int pGridInChunks, int pChunkSize)
    {
        return new Vector3((pGridInChunks.x * pChunkSize) * 0.5f, (pGridInChunks.y * pChunkSize) * 0.5f, (pGridInChunks.z * pChunkSize) * 0.5f);
    }
}