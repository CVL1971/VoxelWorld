using UnityEngine;
using System.Collections.Generic;

public static class VoxelUtils
{
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

    public static int GetChunkIndex(int cx, int cy, int cz, Vector3Int worldChunkSize)
    {
        return cx + worldChunkSize.x * (cy + worldChunkSize.y * cz);
    }

    public static bool IsInBounds(int cx, int cy, int cz, Vector3Int worldChunkSize)
    {
        if (cx < 0 || cx >= worldChunkSize.x) return false;
        if (cy < 0 || cy >= worldChunkSize.y) return false;
        if (cz < 0 || cz >= worldChunkSize.z) return false;
        return true;
    }

    // ==========================================================
    // SOBRECARGAS PARA GENERADORES (ÚNICO PUNTO DE ACCESO)
    // ==========================================================

    public static float GetDensityGlobal(Chunk currentChunk, Chunk[] allChunks, Vector3Int worldSize, int x, int y, int z)
    {
        int size = currentChunk.mSize;
        int gx = currentChunk.mWorldOrigin.x + x;
        int gy = currentChunk.mWorldOrigin.y + y;
        int gz = currentChunk.mWorldOrigin.z + z;

        int cx = gx / size;
        int cy = gy / size;
        int cz = gz / size;

        if (!IsInBounds(cx, cy, cz, worldSize)) return 0.0f;

        Chunk target = allChunks[GetChunkIndex(cx, cy, cz, worldSize)];
        return target.DensityAt(gx - target.mWorldOrigin.x, gy - target.mWorldOrigin.y, gz - target.mWorldOrigin.z);
    }

    public static bool IsSolidGlobal(Chunk currentChunk, Chunk[] allChunks, Vector3Int worldSize, int x, int y, int z)
    {
        int size = currentChunk.mSize;
        int gx = currentChunk.mWorldOrigin.x + x;
        int gy = currentChunk.mWorldOrigin.y + y;
        int gz = currentChunk.mWorldOrigin.z + z;

        int cx = gx / size;
        int cy = gy / size;
        int cz = gz / size;

        if (!IsInBounds(cx, cy, cz, worldSize)) return false;

        Chunk target = allChunks[GetChunkIndex(cx, cy, cz, worldSize)];
        return target.SafeIsSolid(gx - target.mWorldOrigin.x, gy - target.mWorldOrigin.y, gz - target.mWorldOrigin.z);
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
}


