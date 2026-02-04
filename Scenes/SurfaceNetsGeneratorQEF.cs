using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGeneratorQEF : SurfaceNetsGenerator
{
    // Parámetros de "Relajación"
    // 0.0 = Surface Nets básico (Suave) | 1.0 = QEF Puro (Afilado)
    private const float SHARPNESS_STRENGTH = 0.5f;
    private const float ISO_THRESHOLD = 0.6f; // El valor que sugeriste para afilar crestas

    public new Mesh Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = pChunk.mSize;
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> tris = new List<int>();
        int[,,] vmap = new int[size + 1, size + 1, size + 1];

        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    if (CellCrossesIso(pChunk, allChunks, worldSize, x, y, z, ISO_THRESHOLD))
                    {
                        vmap[x, y, z] = verts.Count;
                        // Invocamos nuestra versión mejorada
                        Vector3 localPos = ComputeCellVertex(pChunk, allChunks, worldSize, x, y, z, ISO_THRESHOLD);
                        verts.Add(localPos);
                        normals.Add(SDFGenerator.CalculateNormal((Vector3)pChunk.mWorldOrigin + localPos));
                    }
                    else vmap[x, y, z] = -1;
                }

        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                    EmitCorrectFaces(pChunk, allChunks, worldSize, x, y, z, ISO_THRESHOLD, vmap, tris, size);

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Mantenemos el nombre original para ser coherentes con la clase base
    protected override Vector3 ComputeCellVertex(Chunk p, Chunk[] a, Vector3Int w, int x, int y, int z, float iso)
    {
        List<Vector3> points = new List<Vector3>();
        List<Vector3> nrms = new List<Vector3>();
        Vector3 massPoint = Vector3.zero;

        // Función de ayuda para resolver cada arista con la SDF pura
        void SolveEdge(Vector3 p1, Vector3 p2)
        {
            float d1 = SDFGenerator.GetRawDistance((Vector3)p.mWorldOrigin + p1);
            float d2 = SDFGenerator.GetRawDistance((Vector3)p.mWorldOrigin + p2);

            if (Mathf.Sign(d1) != Mathf.Sign(d2))
            {
                float t = d1 / (d1 - d2 + 0.00001f);
                Vector3 pLocal = Vector3.Lerp(p1, p2, t);
                points.Add(pLocal);
                // IMPORTANTE: CalculateNormalRaw debe usar un 'h' grande (0.25) en SDFGenerator
                nrms.Add(SDFGenerator.CalculateNormalRaw((Vector3)p.mWorldOrigin + pLocal));
                massPoint += pLocal;
            }
        }

        // Definición de las 12 aristas de la celda
        Vector3 p000 = new Vector3(x, y, z), p100 = new Vector3(x + 1, y, z), p010 = new Vector3(x, y + 1, z), p001 = new Vector3(x, y, z + 1);
        Vector3 p110 = new Vector3(x + 1, y + 1, z), p101 = new Vector3(x + 1, y, z + 1), p011 = new Vector3(x, y + 1, z + 1), p111 = new Vector3(x + 1, y + 1, z + 1);

        SolveEdge(p000, p100); SolveEdge(p010, p110); SolveEdge(p001, p101); SolveEdge(p011, p111);
        SolveEdge(p000, p010); SolveEdge(p100, p110); SolveEdge(p001, p011); SolveEdge(p101, p111);
        SolveEdge(p000, p001); SolveEdge(p100, p101); SolveEdge(p010, p011); SolveEdge(p110, p111);

        if (points.Count == 0) return new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
        massPoint /= points.Count;

        // --- FILTRO DE PLANICIDAD (Anti Piel de Lagarto) ---
        float minDot = 1.0f;
        for (int i = 0; i < nrms.Count; i++)
            for (int j = i + 1; j < nrms.Count; j++)
                minDot = Mathf.Min(minDot, Vector3.Dot(nrms[i], nrms[j]));

        // Si la celda es casi plana (variación < 15°), no usamos QEF
        if (minDot > 0.96f) return massPoint;

        // --- SOLVER QEF CON ANCLAJE ---
        Vector3 qefPos = SolveQEF(points, nrms, massPoint);

        // Mezclamos con el promedio para "relajar" la malla y evitar artefactos
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

        // Estabilidad: Fuerza centrípeta hacia el promedio de los puntos
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
        if (Mathf.Abs(det) < 0.01f) return mass;

        float invDet = 1.0f / det;
        return new Vector3(
            invDet * ((m11 * m22 - m12 * m12) * vB.x + (m02 * m12 - m01 * m22) * vB.y + (m01 * m12 - m11 * m02) * vB.z),
            invDet * ((m02 * m12 - m01 * m22) * vB.x + (m00 * m22 - m02 * m02) * vB.y + (m01 * m02 - m00 * m12) * vB.z),
            invDet * ((m01 * m12 - m11 * m02) * vB.x + (m01 * m02 - m00 * m12) * vB.y + (m00 * m11 - m01 * m01) * vB.z)
        );
    }
}