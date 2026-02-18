using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGeneratorQEFOriginal2 : MeshGenerator
{
    private const float ISO_THRESHOLD = 0.5f;

    public override MeshData Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        System.Diagnostics.Stopwatch vWatch = new System.Diagnostics.Stopwatch();
        vWatch.Restart();
        // Se cambia pChunk.mTargetSize por pChunk.mSize
        int size = pChunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : pChunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData meshData = new MeshData();

        // 1. CACHÉ LOCAL (Mantiene la lógica original de size + 2)
        float[,,] localCache = new float[size + 2, size + 2, size + 2];
        for (int z = 0; z <= size + 1; z++)
            for (int y = 0; y <= size + 1; y++)
                for (int x = 0; x <= size + 1; x++)
                {
                    localCache[x, y, z] = VoxelUtils.GetDensityGlobalFinal(pChunk, allChunks, worldSize, x, y, z);
                }

        int[,,] vmap = new int[size + 1, size + 1, size + 1];

        // 2. FASE DE VÉRTICES
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    if (CellCrossesIso(localCache, x, y, z, ISO_THRESHOLD))
                    {
                        vmap[x, y, z] = meshData.vertices.Count;
                        Vector3 localPos = ComputeCellVertexQEF(localCache, x, y, z, ISO_THRESHOLD, vStep);
                        meshData.vertices.Add(localPos);

                        Vector3 worldPos = (Vector3)pChunk.mWorldOrigin + localPos;
                        meshData.normals.Add(SDFGenerator.CalculateNormal(worldPos));
                    }
                    else vmap[x, y, z] = -1;
                }

        // 3. FASE DE CARAS
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    EmitCorrectFaces(localCache, x, y, z, ISO_THRESHOLD, vmap, meshData.triangles, size);
                }

        vWatch.Stop();
        Debug.Log($"[Remesh] time: {vWatch.Elapsed.TotalMilliseconds:F4} ms");
        return meshData;
    }

    protected Vector3 ComputeCellVertexQEF(float[,,] cache, int x, int y, int z, float iso, float vStep)
    {
        Vector3 massPoint = Vector3.zero; int count = 0;
        void CheckEdge(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = cache[x0, y0, z0], d1 = cache[x1, y1, z1];
            if ((d0 < iso && d1 >= iso) || (d0 >= iso && d1 < iso))
            {
                float t = Mathf.Clamp01((iso - d0) / (d1 - d0 + 0.00001f));
                massPoint += Vector3.Lerp(new Vector3(x0, y0, z0) * vStep, new Vector3(x1, y1, z1) * vStep, t);
                count++;
            }
        }
        CheckEdge(x, y, z, x + 1, y, z); CheckEdge(x, y + 1, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x + 1, y, z + 1); CheckEdge(x, y + 1, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y + 1, z); CheckEdge(x + 1, y, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x, y + 1, z + 1); CheckEdge(x + 1, y, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y, z + 1); CheckEdge(x + 1, y, z, x + 1, y, z + 1); CheckEdge(x, y + 1, z, x, y + 1, z + 1); CheckEdge(x + 1, y + 1, z, x + 1, y + 1, z + 1);
        return count > 0 ? massPoint / count : new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * vStep;
    }

    protected bool CellCrossesIso(float[,,] cache, int x, int y, int z, float iso)
    {
        bool first = cache[x, y, z] >= iso;
        for (int i = 1; i < 8; i++)
        {
            float d = cache[x + (i & 1), y + ((i >> 1) & 1), z + ((i >> 2) & 1)];
            if ((d >= iso) != first) return true;
        }
        return false;
    }

    protected void EmitCorrectFaces(float[,,] cache, int x, int y, int z, float iso, int[,,] vmap, List<int> tris, int size)
    {
        float d0 = cache[x, y, z];
        if (x < size)
        {
            float d1 = cache[x + 1, y, z];
            if ((d0 >= iso) != (d1 >= iso) && y > 0 && z > 0)
            {
                int v0 = vmap[x, y - 1, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x, y - 1, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (y < size)
        {
            float d1 = cache[x, y + 1, z];
            if ((d0 >= iso) != (d1 >= iso) && x > 0 && z > 0)
            {
                int v0 = vmap[x - 1, y, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 < d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (z < size)
        {
            float d1 = cache[x, y, z + 1];
            if ((d0 >= iso) != (d1 >= iso) && x > 0 && y > 0)
            {
                int v0 = vmap[x - 1, y - 1, z], v1 = vmap[x, y - 1, z], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
    }

    protected void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
    {
        tris.Add(v0); tris.Add(v1); tris.Add(v2); tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }
}
