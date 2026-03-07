using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class DualContouringGenerator3caches : MeshGenerator
{
    /// <summary> Debe coincidir con SDFGenerator: d >= iso = sólido (debajo de superficie). </summary>
    private const float ISO_THRESHOLD = 0.5f;

    /// <summary> Desplazamiento mínimo hacia el interior en vértices de borde positivo para evitar z-fighting por precisión. </summary>
    private const float BOUNDARY_BIAS = 1e-5f;

    public override MeshData Generate(Chunk chunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = chunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : chunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData mesh = new MeshData();

        // 1. SELECCIÓN DE CACHÉ SEGÚN LOD
        float[] cache;
        ArrayPool.DCache mDCache = chunk.mDCache;
        Interlocked.Increment(ref mDCache.mRefs);

        try
        {

            if (lodIndex == 0)
            {
                cache = mDCache.mSample0;
            }
            else if (lodIndex == 4)
            {
                cache = mDCache.mSample1;
            }
            else
            {
                cache = mDCache.mSample2;
            }

            if (cache == null) return mesh;

            // Stride debe coincidir con Chunk: array (size+3)^3, posiciones -1..size+1
            int p = size + 3;

            // vmap incluye celdas -1..size para geometría en bordes (conexión entre chunks)
            int n = size + 2;
            int[,,] vmap = new int[n, n, n];
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    for (int z = 0; z < n; z++)
                        vmap[x, y, z] = -1;

            // 1) UN VÉRTICE POR CELDA QUE CRUZA EL ISO (celdas -1..size)
            for (int x = -1; x <= size; x++)
                for (int y = -1; y <= size; y++)
                    for (int z = -1; z <= size; z++)
                    {
                        if (!CellCrossesIso(cache, x, y, z, p))
                            continue;

                        Vector3 pos = SolveQEFStable(cache, x, y, z, p, vStep);

                        // Bias hacia el interior en bordes positivos para evitar z-fighting por precisión
                        if (x == size) pos.x -= BOUNDARY_BIAS;
                        if (y == size) pos.y -= BOUNDARY_BIAS;
                        if (z == size) pos.z -= BOUNDARY_BIAS;

                        int index = mesh.vertices.Count;
                        vmap[x + 1, y + 1, z + 1] = index;
                        mesh.vertices.Add(pos);

                        Vector3 smoothNormal = ComputeNormalFromCache(cache, x, y, z, p, size);
                        mesh.normals.Add(smoothNormal);
                    }

            // 2) CARAS POR ARISTA (excluir borde negativo: el vecino en -X/-Y/-Z ya emite esa cara)
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

    // ==================== EDGE EMISSION ======================

    /// <summary>
    /// Excluimos borde negativo (x=-1): el vecino en -X posee esa cara. Emitimos x=0..size-1.
    /// Solo datos locales (cache con padding).
    /// </summary>
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

    /// <summary>
    /// Orientación: d0 >= iso = sólido en lado "negativo" de la arista.
    /// Normal apunta de sólido hacia aire (exterior).
    /// </summary>
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

    // ====================== QEF SOLVER =======================

    Vector3 SolveQEFStable(float[] cache, int x, int y, int z, int p, float step)
    {
        int size = p - 3;
        List<Vector3> positions = new();
        List<Vector3> normals = new();

        void Check(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = GetD(cache, x0, y0, z0, p);
            float d1 = GetD(cache, x1, y1, z1, p);
            if (!SignChange(d0, d1)) return;

            float t = Mathf.Clamp01((ISO_THRESHOLD - d0) / (d1 - d0 + 1e-6f));
            Vector3 p0 = new Vector3(x0, y0, z0) * step;
            Vector3 p1 = new Vector3(x1, y1, z1) * step;
            Vector3 pos = Vector3.Lerp(p0, p1, t);

            Vector3 normal = ComputeNormalFromCache(cache, x0, y0, z0, p, size);
            positions.Add(pos);
            normals.Add(normal);
        }

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

        if (positions.Count == 0)
            return new Vector3((x + 0.5f) * step, (y + 0.5f) * step, (z + 0.5f) * step);

        // --- Hybrid SurfaceNets / DualContouring ---

        float maxAngle = ComputeMaxNormalAngle(normals);

        // umbral típico 25°–35°
        const float FEATURE_THRESHOLD = 40f;

        if (maxAngle < FEATURE_THRESHOLD)
        {
            // superficie suave → Surface Nets
            return Average(positions);
        }

        // feature real → Dual Contouring
        return SolveLeastSquaresClamped(positions, normals, x, y, z, step);
    }

    Vector3 SolveLeastSquaresClamped(List<Vector3> pts, List<Vector3> nms, int cx, int cy, int cz, float step)
    {
        float m00 = 0, m01 = 0, m02 = 0, m11 = 0, m12 = 0, m22 = 0;
        float b0 = 0, b1 = 0, b2 = 0;

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 n = nms[i];
            Vector3 p = pts[i];
            float d = Vector3.Dot(n, p);

            m00 += n.x * n.x; m01 += n.x * n.y; m02 += n.x * n.z;
            m11 += n.y * n.y; m12 += n.y * n.z;
            m22 += n.z * n.z;

            b0 += d * n.x;
            b1 += d * n.y;
            b2 += d * n.z;
        }

        float minX = cx * step, maxX = (cx + 1) * step;
        float minY = cy * step, maxY = (cy + 1) * step;
        float minZ = cz * step, maxZ = (cz + 1) * step;

        float det = m00 * (m11 * m22 - m12 * m12)
            - m01 * (m01 * m22 - m12 * m02)
            + m02 * (m01 * m12 - m11 * m02);

        Vector3 result;

        if (Mathf.Abs(det) < 1e-6f)
        {
            result = Average(pts);
        }
        else
        {
            float c00 = m11 * m22 - m12 * m12;
            float c01 = m02 * m12 - m01 * m22;
            float c02 = m01 * m12 - m02 * m11;
            float c11 = m00 * m22 - m02 * m02;
            float c12 = m01 * m02 - m00 * m12;
            float c22 = m00 * m11 - m01 * m01;

            float inv = 1f / det;
            float vx = (c00 * b0 + c01 * b1 + c02 * b2) * inv;
            float vy = (c01 * b0 + c11 * b1 + c12 * b2) * inv;
            float vz = (c02 * b0 + c12 * b1 + c22 * b2) * inv;
            result = new Vector3(vx, vy, vz);
        }

        bool needsClamp = result.x < minX || result.x > maxX
            || result.y < minY || result.y > maxY
            || result.z < minZ || result.z > maxZ;

        if (needsClamp)
            return Average(pts);

        return result;
    }

    Vector3 Average(List<Vector3> pts)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 p in pts) sum += p;
        return sum / pts.Count;
    }

    // ====================== HELPERS ==========================

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

    float GetD(float[] c, int x, int y, int z, int p)
    {
        // padding de 1 en cada eje
        return c[(x + 1) + p * ((y + 1) + p * (z + 1))];
    }

    /// <summary>
    /// Normal desde cache (mismo GetD que el mesher). Gradiente suavizado con kernel 5x5x5.
    /// </summary>
    Vector3 ComputeNormalFromCache(float[] cache, int cx, int cy, int cz, int p, int size)
    {
        int radius = 2;
        float gx = 0f, gy = 0f, gz = 0f;
        int count = 0;

        for (int dz = -radius; dz <= radius; dz++)
            for (int dy = -radius; dy <= radius; dy++)
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

        Vector3 grad = new Vector3(gx / count, gy / count, gz / count);
        return grad.sqrMagnitude < 0.0001f ? Vector3.up : grad.normalized;
    }

    float ComputeMaxNormalAngle(List<Vector3> normals)
    {
        float maxAngle = 0f;

        for (int i = 0; i < normals.Count; i++)
            for (int j = i + 1; j < normals.Count; j++)
            {
                float angle = Vector3.Angle(normals[i], normals[j]);
                if (angle > maxAngle)
                    maxAngle = angle;
            }

        return maxAngle;
    }


}
