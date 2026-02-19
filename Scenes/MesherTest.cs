using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGeneratorTest
{
    private const float ISO_THRESHOLD = 0.5f;

    public MeshData Generate(Chunk pChunk)
    {
        int size = pChunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : pChunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData meshData = new MeshData();

        // Selección de caché
        float[] cache = (lodIndex == 0) ? pChunk.mSample0 :
                        (lodIndex == 4) ? pChunk.mSample1 : pChunk.mSample2;

        if (cache == null) return meshData;

        int p = size + 2;

        int[,,] vmap = new int[size, size, size];

        // ============================
        // VERTICES
        // ============================
        for (int z = 0; z < size; z++)
for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                    if (CellCrossesIso(cache, x, y, z, p, ISO_THRESHOLD))
                    {
                        vmap[x, y, z] = meshData.vertices.Count;

                        Vector3 localPos = ComputeCellVertexQEF(cache, x, y, z, p, ISO_THRESHOLD, vStep);
                        meshData.vertices.Add(localPos);

                        Vector3 worldPos = (Vector3)pChunk.mWorldOrigin + localPos;
                        meshData.normals.Add(SDFGenerator.CalculateNormal(worldPos));
                    }
                    else vmap[x, y, z] = -1;
                }

        // ============================
        // CARAS
        // ============================
        for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    EmitCorrectFaces(cache, x, y, z, p, ISO_THRESHOLD, vmap, meshData.triangles, size);
                }

        return meshData;
    }

    private float GetD(float[] c, int x, int y, int z, int p)
    {
        return c[(x + 1) + p * ((y + 1) + p * (z + 1))];
    }

    protected bool CellCrossesIso(float[] cache, int x, int y, int z, int p, float iso)
    {
        bool first = GetD(cache, x, y, z, p) >= iso;
        for (int i = 1; i < 8; i++)
        {
            float d = GetD(cache, x + (i & 1), y + ((i >> 1) & 1), z + ((i >> 2) & 1), p);
            if ((d >= iso) != first) return true;
        }
        return false;
    }

    protected Vector3 ComputeCellVertexQEF(float[] cache, int x, int y, int z, int p, float iso, float vStep)
    {
        Vector3 massPoint = Vector3.zero;
        int count = 0;

        void CheckEdge(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            float d0 = GetD(cache, x0, y0, z0, p);
            float d1 = GetD(cache, x1, y1, z1, p);

            if ((d0 < iso && d1 >= iso) || (d0 >= iso && d1 < iso))
            {
                float diff = d1 - d0;
                if (Mathf.Abs(diff) < 1e-6f) return;

                float t = (iso - d0) / diff;
                if (t < 0f || t > 1f) return;

                Vector3 p0 = new Vector3(x0 - 1, y0 - 1, z0 - 1) * vStep;
                Vector3 p1 = new Vector3(x1 - 1, y1 - 1, z1 - 1) * vStep;

                massPoint += Vector3.Lerp(p0, p1, t);
                count++;
            }
        }

        // 12 edges
        CheckEdge(x, y, z, x + 1, y, z);
        CheckEdge(x, y + 1, z, x + 1, y + 1, z);
        CheckEdge(x, y, z + 1, x + 1, y, z + 1);
        CheckEdge(x, y + 1, z + 1, x + 1, y + 1, z + 1);

        CheckEdge(x, y, z, x, y + 1, z);
        CheckEdge(x + 1, y, z, x + 1, y + 1, z);
        CheckEdge(x, y, z + 1, x, y + 1, z + 1);
        CheckEdge(x + 1, y, z + 1, x + 1, y + 1, z + 1);

        CheckEdge(x, y, z, x, y, z + 1);
        CheckEdge(x + 1, y, z, x + 1, y, z + 1);
        CheckEdge(x, y + 1, z, x, y + 1, z + 1);
        CheckEdge(x + 1, y + 1, z, x + 1, y + 1, z + 1);

        if (count > 0)
            return massPoint / count;

        return new Vector3(x - 0.5f, y - 0.5f, z - 0.5f) * vStep;
    }

    protected void EmitCorrectFaces(float[] cache, int x, int y, int z, int p, float iso, int[,,] vmap, List<int> tris, int size)
    {
        float d0 = GetD(cache, x, y, z, p);

        // +X face only
        if (x < size - 1)
        {
            float d1 = GetD(cache, x + 1, y, z, p);
            if ((d0 >= iso) != (d1 >= iso))
                AddFace(vmap, tris, x, y, z, 0);
        }

        // +Y face only
        if (y < size - 1)
        {
            float d1 = GetD(cache, x, y + 1, z, p);
            if ((d0 >= iso) != (d1 >= iso))
                AddFace(vmap, tris, x, y, z, 1);
        }

        // +Z face only
        if (z < size - 1)
        {
            float d1 = GetD(cache, x, y, z + 1, p);
            if ((d0 >= iso) != (d1 >= iso))
                AddFace(vmap, tris, x, y, z, 2);
        }
    }

    void AddFace(int[,,] vmap, List<int> tris, int x, int y, int z, int axis)
    {
        int v0, v1, v2, v3;

        if (axis == 0) // X
        {
            v0 = vmap[x, y, z];
            v1 = vmap[x, y + 1, z];
            v2 = vmap[x, y + 1, z + 1];
            v3 = vmap[x, y, z + 1];
        }
        else if (axis == 1) // Y
        {
            v0 = vmap[x, y, z];
            v1 = vmap[x + 1, y, z];
            v2 = vmap[x + 1, y, z + 1];
            v3 = vmap[x, y, z + 1];
        }
        else // Z
        {
            v0 = vmap[x, y, z];
            v1 = vmap[x + 1, y, z];
            v2 = vmap[x + 1, y + 1, z];
            v3 = vmap[x, y + 1, z];
        }

        if (v0 < 0 || v1 < 0 || v2 < 0 || v3 < 0) return;

        tris.Add(v0); tris.Add(v1); tris.Add(v2);
        tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }
}

