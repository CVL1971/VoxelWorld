using UnityEngine;
using System.Collections.Generic;


public class SurfaceNetsGenerator2 : MeshGenerator
{
    private float[,,] densityCache;
    private Chunk currentChunk;
    private Chunk[] allChunks;
    private Vector3Int worldSize;

    public Mesh Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        const float iso = 0.5f;
        int size = pChunk.mSize;

        this.currentChunk = pChunk;
        this.allChunks = allChunks;
        this.worldSize = worldSize;

        densityCache = new float[size + 2, size + 2, size + 2];

        for (int z = 0; z <= size + 1; z++)
            for (int y = 0; y <= size + 1; y++)
                for (int x = 0; x <= size + 1; x++)
                    densityCache[x, y, z] = VoxelUtils.GetDensityGlobal(pChunk, allChunks, worldSize, x, y, z);

        int estimated = size * size * size / 3;
        List<Vector3> verts = new List<Vector3>(estimated);
        List<Vector3> normals = new List<Vector3>(estimated);
        List<int> tris = new List<int>(estimated * 6);

        int[,,] vmap = new int[size + 1, size + 1, size + 1];

        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    float d000 = GetDensityCached(x, y, z);
                    float d100 = GetDensityCached(x + 1, y, z);
                    float d010 = GetDensityCached(x, y + 1, z);
                    float d110 = GetDensityCached(x + 1, y + 1, z);
                    float d001 = GetDensityCached(x, y, z + 1);
                    float d101 = GetDensityCached(x + 1, y, z + 1);
                    float d011 = GetDensityCached(x, y + 1, z + 1);
                    float d111 = GetDensityCached(x + 1, y + 1, z + 1);

                    int mask = 0;
                    mask |= (d000 >= iso) ? 1 : 0;
                    mask |= (d100 >= iso) ? 2 : 0;
                    mask |= (d010 >= iso) ? 4 : 0;
                    mask |= (d110 >= iso) ? 8 : 0;
                    mask |= (d001 >= iso) ? 16 : 0;
                    mask |= (d101 >= iso) ? 32 : 0;
                    mask |= (d011 >= iso) ? 64 : 0;
                    mask |= (d111 >= iso) ? 128 : 0;

                    if (mask == 0 || mask == 255)
                    {
                        vmap[x, y, z] = -1;
                        continue;
                    }

                    vmap[x, y, z] = verts.Count;
                    Vector3 localPos = ComputeCellVertex(x, y, z, iso, d000, d100, d010, d110, d001, d101, d011, d111);
                    verts.Add(localPos);

                    normals.Add(ComputeNormalFromCache(x + 1, y + 1, z + 1));
                }

        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                    EmitCorrectFaces(x, y, z, iso, vmap, tris, size);

        densityCache = null;
        this.currentChunk = null;
        this.allChunks = null;

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private float GetDensityCached(int x, int y, int z) => densityCache[x, y, z];

    private Vector3 ComputeNormalFromCache(int x, int y, int z)
    {
        float dx = GetDensityCached(x + 1, y, z) - GetDensityCached(x - 1, y, z);
        float dy = GetDensityCached(x, y + 1, z) - GetDensityCached(x, y - 1, z);
        float dz = GetDensityCached(x, y, z + 1) - GetDensityCached(x, y, z - 1);
        return new Vector3(dx, dy, dz).normalized;
    }

    protected virtual Vector3 ComputeCellVertex(int x, int y, int z, float iso,
        float d000, float d100, float d010, float d110,
        float d001, float d101, float d011, float d111)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        void Check(float d0, float d1, Vector3 p0, Vector3 p1)
        {
            if ((d0 < iso && d1 >= iso) || (d0 >= iso && d1 < iso))
            {
                float t = Mathf.Clamp01((iso - d0) / (d1 - d0 + 0.00001f));
                sum += Vector3.Lerp(p0, p1, t);
                count++;
            }
        }

        Vector3 p000 = new Vector3(x, y, z);
        Vector3 p100 = new Vector3(x + 1, y, z);
        Vector3 p010 = new Vector3(x, y + 1, z);
        Vector3 p110 = new Vector3(x + 1, y + 1, z);
        Vector3 p001 = new Vector3(x, y, z + 1);
        Vector3 p101 = new Vector3(x + 1, y, z + 1);
        Vector3 p011 = new Vector3(x, y + 1, z + 1);
        Vector3 p111 = new Vector3(x + 1, y + 1, z + 1);

        Check(d000, d100, p000, p100);
        Check(d010, d110, p010, p110);
        Check(d001, d101, p001, p101);
        Check(d011, d111, p011, p111);

        Check(d000, d010, p000, p010);
        Check(d100, d110, p100, p110);
        Check(d001, d011, p001, p011);
        Check(d101, d111, p101, p111);

        Check(d000, d001, p000, p001);
        Check(d100, d101, p100, p101);
        Check(d010, d011, p010, p011);
        Check(d110, d111, p110, p111);

        return (count > 0) ? sum / count : new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
    }

    public void EmitCorrectFaces(int x, int y, int z, float iso, int[,,] vmap, List<int> tris, int size)
    {
        float d0 = GetDensityCached(x, y, z);

        if (x < size)
        {
            float d1 = GetDensityCached(x + 1, y, z);
            if ((d0 >= iso) != (d1 >= iso) && y > 0 && z > 0)
            {
                int v0 = vmap[x, y - 1, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x, y - 1, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                    if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }

        if (y < size)
        {
            float d1 = GetDensityCached(x, y + 1, z);
            if ((d0 >= iso) != (d1 >= iso) && x > 0 && z > 0)
            {
                int v0 = vmap[x - 1, y, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                    if (d0 < d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }

        if (z < size)
        {
            float d1 = GetDensityCached(x, y, z + 1);
            if ((d0 >= iso) != (d1 >= iso) && x > 0 && y > 0)
            {
                int v0 = vmap[x - 1, y - 1, z], v1 = vmap[x, y - 1, z], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0)
                    if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
    }

    public void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
    {
        tris.Add(v0); tris.Add(v1); tris.Add(v2);
        tris.Add(v0); tris.Add(v2); tris.Add(v3);
    }

    public Mesh Generate(Chunk pChunk) => null;
    public Mesh Generate(Chunk[] pChunks) => null;
}

