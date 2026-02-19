using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dual Contouring sobre las 3 cachés del chunk. Geometría solo en dominio 0..size (nunca en padding).
/// Posiciona cada vértice minimizando el QEF respecto a las intersecciones de aristas y sus normales.
/// </summary>
public class DualContouringGenerator3caches : MeshGenerator
{
    private const float ISO_THRESHOLD = 0.5f;

    public override MeshData Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = pChunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : pChunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData meshData = new MeshData();

        float[] cache = (lodIndex == 0) ? pChunk.mSample0 :
                        (lodIndex == 4) ? pChunk.mSample1 : pChunk.mSample2;
        if (cache == null) return meshData;

        int p = size + 3;
        int vmapSize = size + 2;
        int[,,] vmap = new int[vmapSize, vmapSize, vmapSize];
        for (int i = 0; i < vmapSize; i++)
            for (int j = 0; j < vmapSize; j++)
                for (int k = 0; k < vmapSize; k++)
                    vmap[i, j, k] = -1;

        for (int z = -1; z <= size; z++)
            for (int y = -1; y <= size; y++)
                for (int x = -1; x <= size; x++)
                {
                    if (!CellCrossesIso(cache, x, y, z, p, ISO_THRESHOLD)) continue;
                    int vx = x + 1, vy = y + 1, vz = z + 1;
                    vmap[vx, vy, vz] = meshData.vertices.Count;
                    Vector3 localPos;
                    if (x == -1 || y == -1 || z == -1)
                        localPos = CanonicalPositionForCell(x, y, z, size, vStep);
                    else
                    {
                        localPos = SolveQEF(cache, x, y, z, p, ISO_THRESHOLD, vStep, size, lodIndex);
                        localPos = SnapVertexToBoundary(localPos, x, y, z, size, vStep);
                    }
                    meshData.vertices.Add(localPos);
                    Vector3 normal = ComputeNormalFromCache(cache, x, y, z, p, size, lodIndex);
                    meshData.normals.Add(normal);
                }

        for (int z = -1; z <= size; z++)
            for (int y = -1; y <= size; y++)
                for (int x = -1; x <= size; x++)
                    EmitFaces(cache, x, y, z, p, ISO_THRESHOLD, vmap, meshData.triangles, size);

        SmoothNormalsWeighted(meshData);
        ClampToChunkDomain(meshData.vertices, vStep, size);

        return meshData;
    }

    /// <summary>
    /// Posición canónica para celdas de borde (incl. capa -1). Mismo resultado en ambos lados del borde.
    /// </summary>
    private Vector3 CanonicalPositionForCell(int cx, int cy, int cz, int size, float vStep)
    {
        float maxC = size * vStep;
        float px = (cx == -1) ? 0f : (cx == size) ? maxC : (cx + 0.5f) * vStep;
        float py = (cy == -1) ? 0f : (cy == size) ? maxC : (cy + 0.5f) * vStep;
        float pz = (cz == -1) ? 0f : (cz == size) ? maxC : (cz + 0.5f) * vStep;
        return new Vector3(px, py, pz);
    }

    /// <summary>
    /// En celdas de borde (índice 0 o size-1), usa posición canónica para coincidencia con el vecino.
    /// </summary>
    private Vector3 SnapVertexToBoundary(Vector3 pos, int cx, int cy, int cz, int size, float vStep)
    {
        bool onBoundary = (cx == 0 || cx == size - 1 || cy == 0 || cy == size - 1 || cz == 0 || cz == size - 1);
        if (!onBoundary) return pos;
        return CanonicalPositionForCell(cx, cy, cz, size, vStep);
    }

    /// <summary>
    /// Suaviza normales promediando por área de cara (triángulos grandes pesan más) para reducir ruido de iluminación.
    /// </summary>
    private void SmoothNormalsWeighted(MeshData meshData)
    {
        List<Vector3> verts = meshData.vertices;
        List<int> tris = meshData.triangles;
        if (verts.Count == 0 || tris.Count < 3) return;

        Vector3[] smoothNormals = new Vector3[verts.Count];
        for (int i = 0; i < tris.Count; i += 3)
        {
            int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
            Vector3 a = verts[i0], b = verts[i1], c = verts[i2];
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            float area = faceNormal.magnitude;
            if (area < 1e-10f) continue;
            faceNormal /= area;
            smoothNormals[i0] += faceNormal * area;
            smoothNormals[i1] += faceNormal * area;
            smoothNormals[i2] += faceNormal * area;
        }
        meshData.normals.Clear();
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 n = smoothNormals[i];
            meshData.normals.Add(n.sqrMagnitude < 1e-10f ? Vector3.up : n.normalized);
        }
    }

    /// <summary>
    /// Limita todas las posiciones al dominio [0, size*vStep] del chunk.
    /// </summary>
    private void ClampToChunkDomain(List<Vector3> vertices, float vStep, int size)
    {
        float maxC = size * vStep;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i];
            v.x = Mathf.Clamp(v.x, 0f, maxC);
            v.y = Mathf.Clamp(v.y, 0f, maxC);
            v.z = Mathf.Clamp(v.z, 0f, maxC);
            vertices[i] = v;
        }
    }

    private float GetD(float[] c, int x, int y, int z, int p)
    {
        return c[(x + 1) + p * ((y + 1) + p * (z + 1))];
    }

    private Vector3 ComputeNormalFromCache(float[] cache, int cx, int cy, int cz, int p, int size, int lodIndex)
    {
        int radius = 2;
        float gx = 0f, gy = 0f, gz = 0f;
        int count = 0;
        int maxCoord = size + 1;
        for (int dz = -radius; dz <= radius; dz++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            int nx = Mathf.Clamp(cx + dx, -1, size);
            int ny = Mathf.Clamp(cy + dy, -1, size);
            int nz = Mathf.Clamp(cz + dz, -1, size);
            gx += GetD(cache, Mathf.Clamp(nx - 1, -1, maxCoord), ny, nz, p) - GetD(cache, Mathf.Clamp(nx + 1, -1, maxCoord), ny, nz, p);
            gy += GetD(cache, nx, Mathf.Clamp(ny - 1, -1, maxCoord), nz, p) - GetD(cache, nx, Mathf.Clamp(ny + 1, -1, maxCoord), nz, p);
            gz += GetD(cache, nx, ny, Mathf.Clamp(nz - 1, -1, maxCoord), p) - GetD(cache, nx, ny, Mathf.Clamp(nz + 1, -1, maxCoord), p);
            count++;
        }
        Vector3 grad = new Vector3(gx / count, gy / count, gz / count);
        return grad.sqrMagnitude < 0.0001f ? Vector3.up : grad.normalized;
    }

    private bool CellCrossesIso(float[] cache, int x, int y, int z, int p, float iso)
    {
        bool first = GetD(cache, x, y, z, p) >= iso;
        for (int i = 1; i < 8; i++)
        {
            float d = GetD(cache, x + (i & 1), y + ((i >> 1) & 1), z + ((i >> 2) & 1), p);
            if ((d >= iso) != first) return true;
        }
        return false;
    }

    /// <summary>
    /// Dual Contouring: minimiza QEF sobre (posición, normal) de cada arista que cruza el iso.
    /// Devuelve el vértice en espacio local (coords * vStep). Solo usa celdas en 0..size.
    /// </summary>
    private Vector3 SolveQEF(float[] cache, int x, int y, int z, int p, float iso, float vStep, int size, int lodIndex)
    {
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();

        void AddEdge(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = GetD(cache, x0, y0, z0, p);
            float d1 = GetD(cache, x1, y1, z1, p);
            if ((d0 < iso && d1 >= iso) || (d0 >= iso && d1 < iso))
            {
                float t = Mathf.Clamp01((iso - d0) / (d1 - d0 + 0.000001f));
                Vector3 p0 = new Vector3(x0, y0, z0) * vStep;
                Vector3 p1 = new Vector3(x1, y1, z1) * vStep;
                Vector3 pos = Vector3.Lerp(p0, p1, t);
                int mx = (int)Mathf.Round(x0 + t * (x1 - x0));
                int my = (int)Mathf.Round(y0 + t * (y1 - y0));
                int mz = (int)Mathf.Round(z0 + t * (z1 - z0));
                mx = Mathf.Clamp(mx, 0, size);
                my = Mathf.Clamp(my, 0, size);
                mz = Mathf.Clamp(mz, 0, size);
                Vector3 n = ComputeNormalFromCache(cache, mx, my, mz, p, size, lodIndex);
                positions.Add(pos);
                normals.Add(n);
            }
        }

        AddEdge(x, y, z, x + 1, y, z); AddEdge(x, y + 1, z, x + 1, y + 1, z);
        AddEdge(x, y, z + 1, x + 1, y, z + 1); AddEdge(x, y + 1, z + 1, x + 1, y + 1, z + 1);
        AddEdge(x, y, z, x, y + 1, z); AddEdge(x + 1, y, z, x + 1, y + 1, z);
        AddEdge(x, y, z + 1, x, y + 1, z + 1); AddEdge(x + 1, y, z + 1, x + 1, y + 1, z + 1);
        AddEdge(x, y, z, x, y, z + 1); AddEdge(x + 1, y, z, x + 1, y, z + 1);
        AddEdge(x, y + 1, z, x, y + 1, z + 1); AddEdge(x + 1, y + 1, z, x + 1, y + 1, z + 1);

        if (positions.Count == 0)
            return new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * vStep;

        Vector3 mass = Vector3.zero;
        for (int i = 0; i < positions.Count; i++) mass += positions[i];
        mass /= positions.Count;

        float coherency = 0f;
        if (normals.Count > 1)
        {
            for (int i = 1; i < normals.Count; i++)
                coherency += Mathf.Max(0f, Vector3.Dot(normals[0], normals[i]));
            coherency /= (normals.Count - 1);
        }
        bool useMassPoint = coherency < 0.4f;

        float m00 = 0, m11 = 0, m22 = 0, m01 = 0, m02 = 0, m12 = 0;
        float b0 = 0, b1 = 0, b2 = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 n = normals[i];
            Vector3 pos = positions[i];
            float r = Vector3.Dot(n, pos);
            m00 += n.x * n.x; m11 += n.y * n.y; m22 += n.z * n.z;
            m01 += n.x * n.y; m02 += n.x * n.z; m12 += n.y * n.z;
            b0 += r * n.x; b1 += r * n.y; b2 += r * n.z;
        }

        float det = m00 * (m11 * m22 - m12 * m12) - m01 * (m01 * m22 - m12 * m02) + m02 * (m01 * m12 - m11 * m02);
        if (Mathf.Abs(det) < 1e-9f)
            return mass;

        float inv = 1f / det;
        float a00 = (m11 * m22 - m12 * m12) * inv;
        float a01 = (m02 * m12 - m01 * m22) * inv;
        float a02 = (m01 * m12 - m02 * m11) * inv;
        float a11 = (m00 * m22 - m02 * m02) * inv;
        float a12 = (m01 * m02 - m00 * m12) * inv;
        float a22 = (m00 * m11 - m01 * m01) * inv;
        Vector3 v = new Vector3(
            a00 * b0 + a01 * b1 + a02 * b2,
            a01 * b0 + a11 * b1 + a12 * b2,
            a02 * b0 + a12 * b1 + a22 * b2
        );
        Vector3 cellMin = new Vector3(x, y, z) * vStep;
        Vector3 cellMax = new Vector3(x + 1, y + 1, z + 1) * vStep;
        v.x = Mathf.Clamp(v.x, cellMin.x, cellMax.x);
        v.y = Mathf.Clamp(v.y, cellMin.y, cellMax.y);
        v.z = Mathf.Clamp(v.z, cellMin.z, cellMax.z);

        float maxDisplacement = 0.45f * vStep;
        if (useMassPoint || (mass - v).magnitude > maxDisplacement)
            v = mass;
        return v;
    }

    /// <summary>
    /// Emite todas las caras (incl. x=0,y=0,z=0) usando vmap con offset +1; cierra bordes y esquinas.
    /// </summary>
    private void EmitFaces(float[] cache, int x, int y, int z, int p, float iso, int[,,] vmap, List<int> tris, int size)
    {
        int V(int lx, int ly, int lz) => vmap[lx + 1, ly + 1, lz + 1];
        float d0 = GetD(cache, x, y, z, p);

        if (x < size)
        {
            float d1 = GetD(cache, x + 1, y, z, p);
            if ((d0 >= iso) != (d1 >= iso))
            {
                int v0 = V(x, y - 1, z - 1), v1 = V(x, y, z - 1), v2 = V(x, y, z), v3 = V(x, y - 1, z);
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                    if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (y < size)
        {
            float d1 = GetD(cache, x, y + 1, z, p);
            if ((d0 >= iso) != (d1 >= iso))
            {
                int v0 = V(x - 1, y, z - 1), v1 = V(x, y, z - 1), v2 = V(x, y, z), v3 = V(x - 1, y, z);
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                    if (d0 < d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (z < size)
        {
            float d1 = GetD(cache, x, y, z + 1, p);
            if ((d0 >= iso) != (d1 >= iso))
            {
                int v0 = V(x - 1, y - 1, z), v1 = V(x, y - 1, z), v2 = V(x, y, z), v3 = V(x - 1, y, z);
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                    if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
    }

    private void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
    {
        tris.Add(v0); tris.Add(v1); tris.Add(v2); tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }
}
