using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGeneratorQEF : MeshGenerator
{
    private const float ISO_THRESHOLD = 0.5f;

    public override MeshData Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = pChunk.mSize > 0 ? pChunk.mSize : VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData meshData = new MeshData();

        // 1. CACH? LOCAL con padding -1 (una capa en el vecino) para cerrar geometr?a en bordes
        const int PAD = 1;
        int cacheSize = size + 2 + PAD;
        float[,,] localCache = new float[cacheSize, cacheSize, cacheSize];
        for (int zi = 0; zi < cacheSize; zi++)
            for (int yi = 0; yi < cacheSize; yi++)
                for (int xi = 0; xi < cacheSize; xi++)
                {
                    float lx = (xi - PAD) * vStep, ly = (yi - PAD) * vStep, lz = (zi - PAD) * vStep;
                    localCache[xi, yi, zi] = VoxelUtils.GetDensityGlobal(pChunk, allChunks, worldSize, lx, ly, lz);
                }

        int vmapSize = size + 1 + PAD;
        int[,,] vmap = new int[vmapSize, vmapSize, vmapSize];
        for (int zi = 0; zi < vmapSize; zi++)
            for (int yi = 0; yi < vmapSize; yi++)
                for (int xi = 0; xi < vmapSize; xi++)
                    vmap[xi, yi, zi] = -1;

        // 2. V?RTICES (incluye celda -1 para bordes)
        for (int zi = 0; zi < vmapSize; zi++)
            for (int yi = 0; yi < vmapSize; yi++)
                for (int xi = 0; xi < vmapSize; xi++)
                {
                    if (CellCrossesIso(localCache, xi, yi, zi, ISO_THRESHOLD))
                    {
                        vmap[xi, yi, zi] = meshData.vertices.Count;
                        Vector3 localPos = ComputeCellVertexQEF(localCache, xi, yi, zi, ISO_THRESHOLD, vStep);
                        localPos -= new Vector3(PAD, PAD, PAD) * vStep;
                        meshData.vertices.Add(localPos);
                        Vector3 worldPos = (Vector3)pChunk.mWorldOrigin + localPos;
                        meshData.normals.Add(SDFGenerator.CalculateNormal(worldPos));
                    }
                }

        // 3. CARAS (emitir tambi?n en bordes xi=1, yi=1, zi=1 ? l?gico 0)
        for (int zi = 1; zi <= size + 1; zi++)
            for (int yi = 1; yi <= size + 1; yi++)
                for (int xi = 1; xi <= size + 1; xi++)
                    EmitCorrectFaces(localCache, xi, yi, zi, ISO_THRESHOLD, vmap, meshData.triangles, vmapSize);

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

    protected void EmitCorrectFaces(float[,,] cache, int xi, int yi, int zi, float iso, int[,,] vmap, List<int> tris, int vmapSize)
    {
        float d0 = cache[xi, yi, zi];
        if (xi < vmapSize - 1)
        {
            float d1 = cache[xi + 1, yi, zi];
            if ((d0 >= iso) != (d1 >= iso))
            {
                int v0 = vmap[xi, yi - 1, zi - 1], v1 = vmap[xi, yi, zi - 1], v2 = vmap[xi, yi, zi], v3 = vmap[xi, yi - 1, zi];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (yi < vmapSize - 1)
        {
            float d1 = cache[xi, yi + 1, zi];
            if ((d0 >= iso) != (d1 >= iso))
            {
                int v0 = vmap[xi - 1, yi, zi - 1], v1 = vmap[xi, yi, zi - 1], v2 = vmap[xi, yi, zi], v3 = vmap[xi - 1, yi, zi];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 < d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (zi < vmapSize - 1)
        {
            float d1 = cache[xi, yi, zi + 1];
            if ((d0 >= iso) != (d1 >= iso))
            {
                int v0 = vmap[xi - 1, yi - 1, zi], v1 = vmap[xi, yi - 1, zi], v2 = vmap[xi, yi, zi], v3 = vmap[xi - 1, yi, zi];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
    }

    protected void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
    {
        tris.Add(v0); tris.Add(v1); tris.Add(v2); tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }
}