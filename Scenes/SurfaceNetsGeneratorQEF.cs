using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGeneratorQEF : MeshGenerator
{
    private float[,,] densityCache;
    private Chunk currentChunk;
    private Chunk[] allChunks;
    private Vector3Int worldSize;

    // Mantenemos el ISO de la versión que te funciona
    private const float ISO_THRESHOLD = 0.5f;
    // 0.0 = Suave (Surface Nets) | 1.0 = Afilado (QEF puro)
    private const float SHARPNESS_STRENGTH = 0.5f;

    public Mesh Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = pChunk.mSize;
        this.currentChunk = pChunk;
        this.allChunks = allChunks;
        this.worldSize = worldSize;

        // 1. INICIALIZACIÓN DE CACHÉ (Copia exacta de tu versión 2 funcional)
        densityCache = new float[size + 2, size + 2, size + 2];
        for (int z = 0; z <= size + 1; z++)
            for (int y = 0; y <= size + 1; y++)
                for (int x = 0; x <= size + 1; x++)
                {
                    densityCache[x, y, z] = VoxelUtils.GetDensityGlobal(pChunk, allChunks, worldSize, x, y, z);
                }

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> tris = new List<int>();

        // vmap[x,y,z] guarda el índice del vértice generado para la CELDA que empieza en (x,y,z)
        int[,,] vmap = new int[size + 1, size + 1, size + 1];

        // 2. FASE DE VÉRTICES (Estructura de bucle idéntica a v2)
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    if (CellCrossesIso(x, y, z, ISO_THRESHOLD))
                    {
                        vmap[x, y, z] = verts.Count;

                        // CAMBIO: Usamos el solucionador QEF para posicionar el vértice
                        Vector3 localPos = ComputeCellVertexQEF(x, y, z, ISO_THRESHOLD);
                        verts.Add(localPos);

                        // Normal suave para el renderizado (SDF directo es más preciso para la iluminación)
                        Vector3 worldPos = (Vector3)pChunk.mWorldOrigin + localPos;
                        normals.Add(SDFGenerator.CalculateNormal(worldPos));
                    }
                    else vmap[x, y, z] = -1;
                }

        // 3. FASE DE CARAS (Copia exacta de tu versión 2 funcional)
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    EmitCorrectFaces(x, y, z, ISO_THRESHOLD, vmap, tris, size);
                }

        densityCache = null;
        this.currentChunk = null;

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private float GetDensityCached(int x, int y, int z) => densityCache[x, y, z];

    private bool CellCrossesIso(int x, int y, int z, float iso)
    {
        bool first = GetDensityCached(x, y, z) >= iso;
        for (int i = 1; i < 8; i++)
        {
            float d = GetDensityCached(x + (i & 1), y + ((i >> 1) & 1), z + ((i >> 2) & 1));
            if ((d >= iso) != first) return true;
        }
        return false;
    }

    // --- EL CEREBRO QEF ---
    private Vector3 ComputeCellVertexQEF(int x, int y, int z, float iso)
    {
        List<Vector3> points = new List<Vector3>();
        List<Vector3> nrms = new List<Vector3>();
        Vector3 massPoint = Vector3.zero;

        // Buscamos intersecciones en las 12 aristas de la celda
        void CheckEdge(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = GetDensityCached(x0, y0, z0);
            float d1 = GetDensityCached(x1, y1, z1);
            if ((d0 < iso && d1 >= iso) || (d0 >= iso && d1 < iso))
            {
                float t = Mathf.Clamp01((iso - d0) / (d1 - d0 + 0.00001f));
                Vector3 pLocal = Vector3.Lerp(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), t);
                points.Add(pLocal);

                // Normal en el punto exacto de la arista para el QEF
                Vector3 worldP = (Vector3)currentChunk.mWorldOrigin + pLocal;
                nrms.Add(SDFGenerator.CalculateNormal(worldP));

                massPoint += pLocal;
            }
        }

        CheckEdge(x, y, z, x + 1, y, z); CheckEdge(x, y + 1, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x + 1, y, z + 1); CheckEdge(x, y + 1, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y + 1, z); CheckEdge(x + 1, y, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x, y + 1, z + 1); CheckEdge(x + 1, y, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y, z + 1); CheckEdge(x + 1, y, z, x + 1, y, z + 1); CheckEdge(x, y + 1, z, x, y + 1, z + 1); CheckEdge(x + 1, y + 1, z, x + 1, y + 1, z + 1);

        if (points.Count == 0) return new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
        massPoint /= points.Count;

        Vector3 qefPos = SolveQEF(points, nrms, massPoint);
        Vector3 finalPos = Vector3.Lerp(massPoint, qefPos, SHARPNESS_STRENGTH);

        return new Vector3(
            Mathf.Clamp(finalPos.x, x, x + 1),
            Mathf.Clamp(finalPos.y, y, y + 1),
            Mathf.Clamp(finalPos.z, z, z + 1)
        );
    }

    private Vector3 SolveQEF(List<Vector3> pts, List<Vector3> nrms, Vector3 mass)
    {
        float m00 = 0, m01 = 0, m02 = 0, m11 = 0, m12 = 0, m22 = 0; Vector3 vB = Vector3.zero;
        float stability = 1.2f;
        m00 += stability; m11 += stability; m22 += stability;
        vB += mass * stability;

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 n = nrms[i]; float d = Vector3.Dot(n, pts[i]);
            m00 += n.x * n.x; m01 += n.x * n.y; m02 += n.x * n.z; m11 += n.y * n.y; m12 += n.y * n.z; m22 += n.z * n.z;
            vB += n * d;
        }

        float det = m00 * (m11 * m22 - m12 * m12) - m01 * (m01 * m22 - m12 * m02) + m02 * (m01 * m12 - m11 * m02);
        if (Mathf.Abs(det) < 0.001f) return mass;
        float invDet = 1.0f / det;
        return new Vector3(
            invDet * ((m11 * m22 - m12 * m12) * vB.x + (m02 * m12 - m01 * m22) * vB.y + (m01 * m12 - m11 * m02) * vB.z),
            invDet * ((m02 * m12 - m01 * m22) * vB.x + (m00 * m22 - m02 * m02) * vB.y + (m01 * m02 - m00 * m12) * vB.z),
            invDet * ((m01 * m12 - m11 * m02) * vB.x + (m01 * m02 - m00 * m12) * vB.y + (m00 * m11 - m01 * m01) * vB.z)
        );
    }

    public void EmitCorrectFaces(int x, int y, int z, float iso, int[,,] vmap, List<int> tris, int size)
    {
        float d0 = GetDensityCached(x, y, z);
        if (x < size)
        {
            float d1 = GetDensityCached(x + 1, y, z);
            if ((d0 >= iso) != (d1 >= iso))
            {
                if (y > 0 && z > 0)
                {
                    int v0 = vmap[x, y - 1, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x, y - 1, z];
                    if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                        if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
                }
            }
        }
        if (y < size)
        {
            float d1 = GetDensityCached(x, y + 1, z);
            if ((d0 >= iso) != (d1 >= iso))
            {
                if (x > 0 && z > 0)
                {
                    int v0 = vmap[x - 1, y, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                    if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                        if (d0 < d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
                }
            }
        }
        if (z < size)
        {
            float d1 = GetDensityCached(x, y, z + 1);
            if ((d0 >= iso) != (d1 >= iso))
            {
                if (x > 0 && y > 0)
                {
                    int v0 = vmap[x - 1, y - 1, z], v1 = vmap[x, y - 1, z], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                    if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                        if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
                }
            }
        }
    }

    public void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
    {
        tris.Add(v0); tris.Add(v1); tris.Add(v2); tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }

    public Mesh Generate(Chunk pChunk) => null;
    public Mesh Generate(Chunk[] pChunks) => null;
}