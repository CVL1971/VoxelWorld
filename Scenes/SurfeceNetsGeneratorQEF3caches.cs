using UnityEngine;
using System.Collections.Generic;

public class SurfaceNetsGeneratorQEF3caches : MeshGenerator
{
    private const float ISO_THRESHOLD = 0.5f;

    public override MeshData Generate(Chunk pChunk, Chunk[] allChunks, Vector3Int worldSize)
    {
        int size = pChunk.mSize <= 0 ? VoxelUtils.UNIVERSAL_CHUNK_SIZE : pChunk.mSize;
        int lodIndex = VoxelUtils.GetInfoRes(size);
        float vStep = VoxelUtils.LOD_DATA[lodIndex + 1];

        MeshData meshData = new MeshData();

        // 1. SELECCIÓN DE CACHÉ SEGÚN LOD
        float[] cache = (lodIndex == 0) ? pChunk.mSample0 :
                        (lodIndex == 4) ? pChunk.mSample1 : pChunk.mSample2;


        


        if (cache == null) return meshData;

        // p = size+3: array cubre posiciones -1 a size+1 (geometría de fronteras entre chunks)
        int p = size + 3;

        // 2. FASE DE VÉRTICES
        // Celdas 0..size: incluye las del borde para generar vértices que conectan con vecinos
        int[,,] vmap = new int[size + 1, size + 1, size + 1];

        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    // Llamada corregida con 'p' y 'ISO_THRESHOLD'
                    if (CellCrossesIso(cache, x, y, z, p, ISO_THRESHOLD))
                    {
                        vmap[x, y, z] = meshData.vertices.Count;

                        // LLAMADA CORREGIDA: Ahora pasamos vStep al final
                        Vector3 localPos = ComputeCellVertexQEF(cache, x, y, z, p, ISO_THRESHOLD, vStep);

                        meshData.vertices.Add(localPos);

                        Vector3 normal = ComputeNormalFromCache(cache, localPos, vStep, p, size);
                        meshData.normals.Add(normal);
                    }
                    else vmap[x, y, z] = -1;
                }

        // 3. FASE DE CARAS (mismo rango de celdas: 0..size)
        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    EmitCorrectFaces(cache, x, y, z, p, ISO_THRESHOLD, vmap, meshData.triangles, size);
                }




        return meshData;
    }

    private float GetD(float[] c, int x, int y, int z, int p)
    {
        return c[(x + 1) + p * ((y + 1) + p * (z + 1))];
    }

    /// <summary>
    /// Calcula la normal desde la caché activa del chunk (gradiente por diferencias centrales).
    /// No usa SDFGenerator.Sample ni datos de vecinos; única fuente: mSample0/1/2.
    /// </summary>
    private Vector3 ComputeNormalFromCache(float[] cache, Vector3 localPos, float vStep, int p, int size)
    {
        // Cache cubre -1..size+1; diferencias centrales requieren vecinos, luego [0, size]
        int ix = Mathf.Clamp(Mathf.RoundToInt(localPos.x / vStep), 0, size);
        int iy = Mathf.Clamp(Mathf.RoundToInt(localPos.y / vStep), 0, size);
        int iz = Mathf.Clamp(Mathf.RoundToInt(localPos.z / vStep), 0, size);

        float dX = GetD(cache, ix + 1, iy, iz, p) - GetD(cache, ix - 1, iy, iz, p);
        float dY = GetD(cache, ix, iy + 1, iz, p) - GetD(cache, ix, iy - 1, iz, p);
        float dZ = GetD(cache, ix, iy, iz + 1, p) - GetD(cache, ix, iy, iz - 1, p);

        Vector3 grad = new Vector3(dX, dY, dZ);
        return grad.sqrMagnitude < 0.0001f ? Vector3.up : grad.normalized;
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
                float t = Mathf.Clamp01((iso - d0) / (d1 - d0 + 0.000001f));

                // Posición física: grid (i,j,k) → local (i*vStep, j*vStep, k*vStep)
                Vector3 p0 = new Vector3(x0, y0, z0) * vStep;
                Vector3 p1 = new Vector3(x1, y1, z1) * vStep;

                massPoint += Vector3.Lerp(p0, p1, t);
                count++;
            }
        }

        CheckEdge(x, y, z, x + 1, y, z); CheckEdge(x, y + 1, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x + 1, y, z + 1); CheckEdge(x, y + 1, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y + 1, z); CheckEdge(x + 1, y, z, x + 1, y + 1, z); CheckEdge(x, y, z + 1, x, y + 1, z + 1); CheckEdge(x + 1, y, z + 1, x + 1, y + 1, z + 1);
        CheckEdge(x, y, z, x, y, z + 1); CheckEdge(x + 1, y, z, x + 1, y, z + 1); CheckEdge(x, y + 1, z, x, y + 1, z + 1); CheckEdge(x + 1, y + 1, z, x + 1, y + 1, z + 1);

        return count > 0 ? massPoint / count : new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * vStep;
    }

    protected void EmitCorrectFaces(float[] cache, int x, int y, int z, int p, float iso, int[,,] vmap, List<int> tris, int size)
    {
        float d0 = GetD(cache, x, y, z, p);
        if (x < size)
        {
            float d1 = GetD(cache, x + 1, y, z, p);
            if ((d0 >= iso) != (d1 >= iso) && y > 0 && z > 0)
            {
                int v0 = vmap[x, y - 1, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x, y - 1, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 > d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (y < size)
        {
            float d1 = GetD(cache, x, y + 1, z, p);
            if ((d0 >= iso) != (d1 >= iso) && x > 0 && z > 0)
            {
                int v0 = vmap[x - 1, y, z - 1], v1 = vmap[x, y, z - 1], v2 = vmap[x, y, z], v3 = vmap[x - 1, y, z];
                if (v0 >= 0 && v1 >= 0 && v2 >= 0 && v3 >= 0) if (d0 < d1) AddQuad(tris, v0, v1, v2, v3); else AddQuad(tris, v0, v3, v2, v1);
            }
        }
        if (z < size)
        {
            float d1 = GetD(cache, x, y, z + 1, p);
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

