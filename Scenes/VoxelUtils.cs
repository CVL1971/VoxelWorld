using UnityEngine;
using System.Collections.Generic;

public static class VoxelUtils
{
    public const int UNIVERSAL_CHUNK_SIZE = 32;


    /// <summary>
    /// Estructura para transportar la informaci?n de ubicaci?n de un voxel.
    /// </summary>
    public struct VoxelHit
    {
        public bool isValid;
        public int chunkIndex;
        public Vector3Int localPos;
        public Vector3Int globalVoxelPos;
    }

    // ==========================================================
    // M?TODOS DE CONVERSI?N CONVENCIONALES
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
    // SOBRECARGAS PARA GENERADORES (?NICO PUNTO DE ACCESO)
    // ==========================================================
    // ==========================================================
    // SOBRECARGAS PARA GENERADORES (?NICO PUNTO DE ACCESO)
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

        int targetRes = target.mSize <= 0 ? UNIVERSAL_CHUNK_SIZE : target.mSize;
        int currentRes = currentChunk.mSize <= 0 ? UNIVERSAL_CHUNK_SIZE : currentChunk.mSize;

        // Resincronizaci?n en bordes (evita grietas al acercar Y al alejar):
        // - Vecino pendiente de resample: SDF (no tiene datos coherentes a?n).
        // - Nosotros M?s FINOS que el vecino (acercando/refinando): SDF para no depender de datos viejos del vecino.
        // - Nosotros M?s GRUESOS o igual que el vecino (alejando/decimando): LEER del array del vecino para que
        //   nuestro borde coincida con la malla que el vecino ya tiene (?l se gener? antes leyendo nuestros datos).
        if (target.mAwaitingResample || currentRes > targetRes)
            return SDFGenerator.Sample(new Vector3(gx, gy, gz));

        // Misma resoluci?n o nosotros m?s gruesos: leer del array del vecino

        int lodIdx = GetInfoRes(targetRes);
        float targetStep = LOD_DATA[lodIdx + 1];

        float localX = gx - target.mWorldOrigin.x;
        float localY = gy - target.mWorldOrigin.y;
        float localZ = gz - target.mWorldOrigin.z;

        // Convertimos la posici?n continua a ?ndices del grid del chunk vecino
        float fx = localX / targetStep;
        float fy = localY / targetStep;
        float fz = localZ / targetStep;

        // Si estamos EXACTAMENTE en un v?rtice del grid, lo devolvemos directamente
        int ix = Mathf.RoundToInt(fx);
        int iy = Mathf.RoundToInt(fy);
        int iz = Mathf.RoundToInt(fz);

        // Tolerancia para considerar que estamos "en" un v?rtice
        const float SNAP_THRESHOLD = 0.01f;

        if (Mathf.Abs(fx - ix) < SNAP_THRESHOLD &&
            Mathf.Abs(fy - iy) < SNAP_THRESHOLD &&
            Mathf.Abs(fz - iz) < SNAP_THRESHOLD)
        {
            ix = Mathf.Clamp(ix, 0, targetRes - 1);
            iy = Mathf.Clamp(iy, 0, targetRes - 1);
            iz = Mathf.Clamp(iz, 0, targetRes - 1);
            return target.DensityAt(ix, iy, iz);
        }

        // Si no, hacemos interpolaci?n trilineal para suavizar la transici?n
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, targetRes - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, targetRes - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, targetRes - 1);

        int x1 = Mathf.Clamp(x0 + 1, 0, targetRes - 1);
        int y1 = Mathf.Clamp(y0 + 1, 0, targetRes - 1);
        int z1 = Mathf.Clamp(z0 + 1, 0, targetRes - 1);

        float tx = fx - x0;
        float ty = fy - y0;
        float tz = fz - z0;

        // Interpolaci?n trilineal
        float c000 = target.DensityAt(x0, y0, z0);
        float c100 = target.DensityAt(x1, y0, z0);
        float c010 = target.DensityAt(x0, y1, z0);
        float c110 = target.DensityAt(x1, y1, z0);
        float c001 = target.DensityAt(x0, y0, z1);
        float c101 = target.DensityAt(x1, y0, z1);
        float c011 = target.DensityAt(x0, y1, z1);
        float c111 = target.DensityAt(x1, y1, z1);

        float c00 = Mathf.Lerp(c000, c100, tx);
        float c01 = Mathf.Lerp(c001, c101, tx);
        float c10 = Mathf.Lerp(c010, c110, tx);
        float c11 = Mathf.Lerp(c011, c111, tx);

        float c0 = Mathf.Lerp(c00, c10, ty);
        float c1 = Mathf.Lerp(c01, c11, ty);

        return Mathf.Lerp(c0, c1, tz);
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
        int currentRes = currentChunk.mSize <= 0 ? UNIVERSAL_CHUNK_SIZE : currentChunk.mSize;

        // Misma l?gica que GetDensityGlobal: SDF solo si vecino pendiente o nosotros m?s finos; si no, leer array.
        if (target.mAwaitingResample || currentRes > targetRes)
            return SDFGenerator.Sample(new Vector3(gx, gy, gz)) >= 0.5f;

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
    // L?GICA DE LOCALIZACI?N
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

    // [0] Resoluci?n | [1] Paso | [2] DistanciaSq | [3] LOD_Index
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