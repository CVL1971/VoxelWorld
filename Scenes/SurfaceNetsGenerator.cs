using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGenerator : MeshGenerator
{
    public Mesh Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        const float iso = 0.5f;
        int size = pChunk.mSize;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> tris = new List<int>();

        // vmap[x,y,z] guarda el índice del vértice generado para la CELDA que empieza en (x,y,z)
        int[,,] vmap = new int[size + 1, size + 1, size + 1];

        // 1. FASE DE VÉRTICES: Generamos un vértice por cada celda que cruza el ISO
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    if (CellCrossesIso(pChunk, allChunks, worldSize, x, y, z, iso))
                    {
                        vmap[x, y, z] = verts.Count;
                        Vector3 localPos = ComputeCellVertex(pChunk, allChunks, worldSize, x, y, z, iso);
                        verts.Add(localPos);

                        Vector3 worldPos = (Vector3)pChunk.mWorldOrigin + localPos;
                        normals.Add(SDFGenerator.CalculateNormal(worldPos));
                    }
                    else vmap[x, y, z] = -1;
                }

        // 2. FASE DE CARAS: Revisamos las aristas de los voxeles
        // Cada arista que cruza el ISO genera un QUAD entre las 4 celdas que la comparten
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    EmitCorrectFaces(pChunk, allChunks, worldSize, x, y, z, iso, vmap, tris, size);
                }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    public void EmitCorrectFaces(Chunk p, Chunk[] a, Vector3Int w, int x, int y, int z, float iso, int[,,] vmap, List<int> tris, int size)
    {
        float d0 = VoxelUtils.GetDensityGlobal(p, a, w, x, y, z);

        // Arista en X: voxel(x,y,z) a voxel(x+1,y,z). Genera Quad en plano YZ.
        if (x < size)
        {
            float d1 = VoxelUtils.GetDensityGlobal(p, a, w, x + 1, y, z);
            if ((d0 >= iso) != (d1 >= iso))
            {
                // Las 4 celdas que comparten esta arista están en el mismo plano X
                if (y > 0 && z > 0)
                {
                    int v0 = vmap[x, y - 1, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x, y - 1, z];
                    if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                        if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
                }
            }
        }

        // Arista en Y: voxel(x,y,z) a voxel(x,y+1,z). Genera Quad en plano XZ.
        if (y < size)
        {
            float d1 = VoxelUtils.GetDensityGlobal(p, a, w, x, y + 1, z);
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

        // Arista en Z: voxel(x,y,z) a voxel(x,y,z+1). Genera Quad en plano XY.
        if (z < size)
        {
            float d1 = VoxelUtils.GetDensityGlobal(p, a, w, x, y, z + 1);
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

    // --- MÉTODOS AUXILIARES (Sin cambios lógicos, solo paso de parámetros) ---
    public bool CellCrossesIso(Chunk p, Chunk[] a, Vector3Int w, int x, int y, int z, float iso)
    {
        bool first = VoxelUtils.GetDensityGlobal(p, a, w, x, y, z) >= iso;
        for (int i = 1; i < 8; i++)
        {
            float d = VoxelUtils.GetDensityGlobal(p, a, w, x + (i & 1), y + ((i >> 1) & 1), z + ((i >> 2) & 1));
            if ((d >= iso) != first) return true;
        }
        return false;
    }

    protected virtual Vector3 ComputeCellVertex(Chunk p, Chunk[] a, Vector3Int w, int x, int y, int z, float iso)
    {
        Vector3 sum = Vector3.zero; int count = 0;
        void CheckEdge(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = VoxelUtils.GetDensityGlobal(p, a, w, x0, y0, z0), d1 = VoxelUtils.GetDensityGlobal(p, a, w, x1, y1, z1);
            if ((d0 < iso && d1 >= iso) || (d0 >= iso && d1 < iso))
            {
                float t = Mathf.Clamp01((iso - d0) / (d1 - d0 + 0.00001f));
                sum += Vector3.Lerp(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), t);
                count++;
            }
        }
        CheckEdge(x, y, z, x + 1, y, z); CheckEdge(x, y + 1, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x + 1, y, z + 1); CheckEdge(x, y + 1, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y + 1, z); CheckEdge(x + 1, y, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x, y + 1, z + 1); CheckEdge(x + 1, y, z + 1, x, y + 1, z + 1); // Fix index error here
        CheckEdge(x, y, z, x, y, z + 1); CheckEdge(x + 1, y, z, x + 1, y, z + 1); CheckEdge(x, y + 1, z, x, y + 1, z + 1); CheckEdge(x + 1, y + 1, z, x + 1, y + 1, z + 1);
        return (count > 0) ? (sum / count) : new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
    }

    public void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
    {
        tris.Add(v0); tris.Add(v1); tris.Add(v2); tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }
    public Mesh Generate(Chunk pChunk) => null;
    public Mesh Generate(Chunk[] pChunks) => null;
}