using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class SurfaceNetsNewGeneration1 : MeshGenerator
{
    const float ISO_THRESHOLD = 0.5f;
    const float BOUNDARY_BIAS = 1e-5f;

    public override MeshData Generate(Chunk chunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = chunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : chunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData mesh = new MeshData();

        float[] cache;
        ArrayPool.DCache mDCache = chunk.mDCache;
        Interlocked.Increment(ref mDCache.mRefs);

        try
        {
            if (lodIndex == 0)
                cache = mDCache.mSample0;
            else if (lodIndex == 4)
                cache = mDCache.mSample1;
            else
                cache = mDCache.mSample2;

            if (cache == null) return mesh;

            int p = size + 3;

            int n = size + 2;
            int[,,] vmap = new int[n, n, n];

            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    for (int z = 0; z < n; z++)
                        vmap[x, y, z] = -1;

            // ----------------------------------
            // VERTICES
            // ----------------------------------

            for (int x = -1; x <= size; x++)
                for (int y = -1; y <= size; y++)
                    for (int z = -1; z <= size; z++)
                    {
                        if (!CellCrossesIso(cache, x, y, z, p))
                            continue;

                        Vector3 pos = SolveWeightedSurfaceNet(cache, x, y, z, p, vStep);

                        if (x == size) pos.x -= BOUNDARY_BIAS;
                        if (y == size) pos.y -= BOUNDARY_BIAS;
                        if (z == size) pos.z -= BOUNDARY_BIAS;

                        int index = mesh.vertices.Count;
                        vmap[x + 1, y + 1, z + 1] = index;

                        mesh.vertices.Add(pos);
                        mesh.normals.Add(ComputeNormalFromCache(cache, x, y, z, p, size));
                    }

            // ----------------------------------
            // FACES
            // ----------------------------------

            EmitEdgesX(cache, vmap, mesh.triangles, size, p);
            EmitEdgesY(cache, vmap, mesh.triangles, size, p);
            EmitEdgesZ(cache, vmap, mesh.triangles, size, p);
        }
        finally
        {
            Interlocked.Decrement(ref mDCache.mRefs);
        }

        return mesh;
    }

    // ---------------------------------------------------
    // WEIGHTED SURFACE NETS
    // ---------------------------------------------------

    Vector3 SolveWeightedSurfaceNet(float[] cache, int x, int y, int z, int p, float step)
    {
        Vector3 sum = Vector3.zero;
        float weightSum = 0f;

        void Check(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = GetD(cache, x0, y0, z0, p);
            float d1 = GetD(cache, x1, y1, z1, p);

            if (!SignChange(d0, d1)) return;

            float t = (ISO_THRESHOLD - d0) / (d1 - d0 + 1e-6f);
            t = Mathf.Clamp01(t);

            Vector3 p0 = new Vector3(x0, y0, z0) * step;
            Vector3 p1 = new Vector3(x1, y1, z1) * step;

            Vector3 pos = Vector3.Lerp(p0, p1, t);

            float w = 1f - Mathf.Abs(d0 - d1); // peso iso proximity
            w = Mathf.Max(w, 0.001f);

            sum += pos * w;
            weightSum += w;
        }

        // 12 edges
        Check(x, y, z, x + 1, y, z);
        Check(x, y + 1, z, x + 1, y + 1, z);
        Check(x, y, z + 1, x + 1, y, z + 1);
        Check(x, y + 1, z + 1, x + 1, y + 1, z + 1);

        Check(x, y, z, x, y + 1, z);
        Check(x + 1, y, z, x + 1, y + 1, z);
        Check(x, y, z + 1, x, y + 1, z + 1);
        Check(x + 1, y, z + 1, x + 1, y + 1, z + 1);

        Check(x, y, z, x, y, z + 1);
        Check(x + 1, y, z, x + 1, y, z + 1);
        Check(x, y + 1, z, x, y + 1, z + 1);
        Check(x + 1, y + 1, z, x + 1, y + 1, z + 1);

        if (weightSum == 0)
            return new Vector3(
                (x + 0.5f) * step,
                (y + 0.5f) * step,
                (z + 0.5f) * step);

        return sum / weightSum;
    }

    // ---------------------------------------------------
    // EDGE EMISSION
    // ---------------------------------------------------

    void EmitEdgesX(float[] cache, int[,,] vmap, List<int> tris, int size, int p)
    {
        for (int x = 0; x < size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = 0; z <= size; z++)
                {
                    float d0 = GetD(cache, x, y, z, p);
                    float d1 = GetD(cache, x + 1, y, z, p);

                    if (!SignChange(d0, d1)) continue;

                    int v00 = vmap[x + 1, y, z];
                    int v10 = vmap[x + 1, y + 1, z];
                    int v11 = vmap[x + 1, y + 1, z + 1];
                    int v01 = vmap[x + 1, y, z + 1];

                    AddQuadConsistent(tris, v00, v10, v11, v01, d0);
                }
    }

    void EmitEdgesY(float[] cache, int[,,] vmap, List<int> tris, int size, int p)
    {
        for (int x = 0; x <= size; x++)
            for (int y = 0; y < size; y++)
                for (int z = 0; z <= size; z++)
                {
                    float d0 = GetD(cache, x, y, z, p);
                    float d1 = GetD(cache, x, y + 1, z, p);

                    if (!SignChange(d0, d1)) continue;

                    int v00 = vmap[x, y + 1, z];
                    int v10 = vmap[x + 1, y + 1, z];
                    int v11 = vmap[x + 1, y + 1, z + 1];
                    int v01 = vmap[x, y + 1, z + 1];

                    AddQuadConsistent(tris, v00, v01, v11, v10, d0);
                }
    }

    void EmitEdgesZ(float[] cache, int[,,] vmap, List<int> tris, int size, int p)
    {
        for (int x = 0; x <= size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = 0; z < size; z++)
                {
                    float d0 = GetD(cache, x, y, z, p);
                    float d1 = GetD(cache, x, y, z + 1, p);

                    if (!SignChange(d0, d1)) continue;

                    int v00 = vmap[x, y, z + 1];
                    int v10 = vmap[x + 1, y, z + 1];
                    int v11 = vmap[x + 1, y + 1, z + 1];
                    int v01 = vmap[x, y + 1, z + 1];

                    AddQuadConsistent(tris, v00, v10, v11, v01, d0);
                }
    }

    void AddQuadConsistent(List<int> tris, int a, int b, int c, int d, float d0)
    {
        if (a < 0 || b < 0 || c < 0 || d < 0) return;

        if (d0 >= ISO_THRESHOLD)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }
        else
        {
            tris.Add(a); tris.Add(d); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(b);
        }
    }

    // ---------------------------------------------------
    // HELPERS
    // ---------------------------------------------------

    float GetD(float[] c, int x, int y, int z, int p)
    {
        return c[(x + 1) + p * ((y + 1) + p * (z + 1))];
    }

    bool SignChange(float a, float b)
    {
        return (a >= ISO_THRESHOLD && b < ISO_THRESHOLD)
            || (a < ISO_THRESHOLD && b >= ISO_THRESHOLD);
    }

    bool CellCrossesIso(float[] cache, int x, int y, int z, int p)
    {
        float d0 = GetD(cache, x, y, z, p);
        bool solid = d0 >= ISO_THRESHOLD;

        for (int i = 1; i < 8; i++)
        {
            float d = GetD(cache,
                x + (i & 1),
                y + ((i >> 1) & 1),
                z + ((i >> 2) & 1),
                p);

            if ((d >= ISO_THRESHOLD) != solid)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Normal desde cache con clamp para celdas de borde (-1, size).
    /// Evita acceso fuera de rango en GetD(x±1, y±1, z±1).
    /// </summary>
    Vector3 ComputeNormalFromCache(float[] cache, int cx, int cy, int cz, int p, int size)
    {
        int radius = 2;
        float gx = 0f, gy = 0f, gz = 0f;
        int count = 0;

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = Mathf.Clamp(cx + dx, 0, size);
                    int ny = Mathf.Clamp(cy + dy, 0, size);
                    int nz = Mathf.Clamp(cz + dz, 0, size);

                    gx += GetD(cache, nx - 1, ny, nz, p) - GetD(cache, nx + 1, ny, nz, p);
                    gy += GetD(cache, nx, ny - 1, nz, p) - GetD(cache, nx, ny + 1, nz, p);
                    gz += GetD(cache, nx, ny, nz - 1, p) - GetD(cache, nx, ny, nz + 1, p);
                    count++;
                }
            }
        }

        Vector3 grad = new Vector3(gx / count, gy / count, gz / count);
        return grad.sqrMagnitude < 0.0001f ? Vector3.up : grad.normalized;
    }
}
