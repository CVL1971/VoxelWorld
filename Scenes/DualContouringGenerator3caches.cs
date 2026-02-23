using UnityEngine;
using System.Collections.Generic;

public class DualContouringGenerator3caches : MeshGenerator
{
    private const float ISO_THRESHOLD = 0f; // usa 0 si tu SDF es signed

    public override MeshData Generate(Chunk chunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = chunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : chunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData mesh = new MeshData();

        float[] cache = (lodIndex == 0) ? chunk.mSample0 :
                        (lodIndex == 4) ? chunk.mSample1 : chunk.mSample2;

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

                    Vector3 pos = SolveQEFStable(chunk, cache, x, y, z, p, vStep);

                    int index = mesh.vertices.Count;
                    vmap[x + 1, y + 1, z + 1] = index;
                    mesh.vertices.Add(pos);

                    Vector3 world = (Vector3)chunk.WorldOrigin + pos;
                    mesh.normals.Add(SDFGenerator.CalculateNormal(world));
                }

        // 2) CARAS POR ARISTA
        EmitEdgesX(cache, vmap, mesh.triangles, size, p);
        EmitEdgesY(cache, vmap, mesh.triangles, size, p);
        EmitEdgesZ(cache, vmap, mesh.triangles, size, p);

        return mesh;
    }

    // ==================== EDGE EMISSION ======================

    // Eje X: aristas (x,y,z)-(x+1,y,z). Celdas (x,y-1,z-1)..(x,y,z). vmap[i,j,k] = celda (i-1,j-1,k-1)
    void EmitEdgesX(float[] cache, int[,,] vmap, List<int> tris, int size, int p)
    {
        for (int x = -1; x < size; x++)
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

    // Eje Y: aristas (x,y,z)-(x,y+1,z). Celdas (x-1,y,z-1)..(x,y,z)
    void EmitEdgesY(float[] cache, int[,,] vmap, List<int> tris, int size, int p)
    {
        for (int x = 0; x <= size; x++)
            for (int y = -1; y < size; y++)
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

    // Eje Z: aristas (x,y,z)-(x,y,z+1). Celdas (x-1,y-1,z)..(x,y,z)
    void EmitEdgesZ(float[] cache, int[,,] vmap, List<int> tris, int size, int p)
    {
        for (int x = 0; x <= size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = -1; z < size; z++)
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

    // Orientación consistente según el signo en el lado "negativo" de la arista
    void AddQuadConsistent(List<int> tris, int a, int b, int c, int d, float d0)
    {
        if (a < 0 || b < 0 || c < 0 || d < 0) return;

        // d0 < 0 => interior en el lado "negativo" de la arista, normal hacia exterior
        if (d0 < ISO_THRESHOLD)
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

    Vector3 SolveQEFStable(Chunk chunk, float[] cache,
        int x, int y, int z, int p, float step)
    {
        List<Vector3> pts = new();
        List<Vector3> nms = new();

        void Check(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = GetD(cache, x0, y0, z0, p);
            float d1 = GetD(cache, x1, y1, z1, p);
            if (!SignChange(d0, d1)) return;

            float t = d0 / (d0 - d1);
            Vector3 p0 = new Vector3(x0, y0, z0) * step;
            Vector3 p1 = new Vector3(x1, y1, z1) * step;
            Vector3 pos = Vector3.Lerp(p0, p1, t);

            Vector3 world = (Vector3)chunk.WorldOrigin + pos;
            Vector3 normal = SDFGenerator.CalculateNormal(world).normalized;

            pts.Add(pos);
            nms.Add(normal);
        }

        // 12 aristas de la celda
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

        if (pts.Count == 0)
            return new Vector3((x + 0.5f) * step, (y + 0.5f) * step, (z + 0.5f) * step);

        return SolveLeastSquaresClamped(pts, nms, x, y, z, step);
    }

    Vector3 SolveLeastSquaresClamped(
        List<Vector3> pts,
        List<Vector3> nms,
        int cx, int cy, int cz, float step)
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

        // matriz simétrica:
        // [m00 m01 m02]
        // [m01 m11 m12]
        // [m02 m12 m22]

        float det =
            m00 * (m11 * m22 - m12 * m12)
          - m01 * (m01 * m22 - m12 * m02)
          + m02 * (m01 * m12 - m11 * m02);

        Vector3 result;

        if (Mathf.Abs(det) < 1e-8f)
        {
            // mal condicionada: media simple
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

            float invDet = 1f / det;

            float x = (c00 * b0 + c01 * b1 + c02 * b2) * invDet;
            float y = (c01 * b0 + c11 * b1 + c12 * b2) * invDet;
            float z = (c02 * b0 + c12 * b1 + c22 * b2) * invDet;

            result = new Vector3(x, y, z);
        }

        // Clamp al AABB de la celda
        float minX = cx * step, maxX = (cx + 1) * step;
        float minY = cy * step, maxY = (cy + 1) * step;
        float minZ = cz * step, maxZ = (cz + 1) * step;

        result.x = Mathf.Clamp(result.x, minX, maxX);
        result.y = Mathf.Clamp(result.y, minY, maxY);
        result.z = Mathf.Clamp(result.z, minZ, maxZ);

        return result;
    }

    Vector3 Average(List<Vector3> pts)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p;
        return sum / pts.Count;
    }

    // ====================== HELPERS ==========================

    // Usa cambio de signo estricto para evitar duplicidades en valores exactamente 0
    bool SignChange(float a, float b)
        => (a < ISO_THRESHOLD && b > ISO_THRESHOLD)
        || (a > ISO_THRESHOLD && b < ISO_THRESHOLD);

    bool CellCrossesIso(float[] cache, int x, int y, int z, int p)
    {
        float d0 = GetD(cache, x, y, z, p);
        bool sign = d0 < ISO_THRESHOLD;

        for (int i = 1; i < 8; i++)
        {
            float d = GetD(cache,
                x + (i & 1),
                y + ((i >> 1) & 1),
                z + ((i >> 2) & 1),
                p);

            if ((d < ISO_THRESHOLD) != sign)
                return true;
        }
        return false;
    }

    float GetD(float[] c, int x, int y, int z, int p)
    {
        // padding de 1 en cada eje
        return c[(x + 1) + p * ((y + 1) + p * (z + 1))];
    }
}
