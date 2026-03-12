using System.Collections.Generic;
using UnityEngine;

public class SuperChunkDebugRenderer : MonoBehaviour
{
    private static List<nChunkB> sChunks = new List<nChunkB>();

    public static void RegisterChunk(nChunkB pChunk)
    {
        if (!sChunks.Contains(pChunk))
            sChunks.Add(pChunk);
    }

   

    [Header("Density Debug Mode")]

    public bool drawSamples = false;
    public bool edgesOnly = true;

    [Header("Debug toggles")]

    public bool drawChunkBounds = true;
    public bool drawIsoCells = true;
    public bool drawNormals = false;
    public bool drawClipmapLevel = true;

    private void OnDrawGizmos()
    {
        foreach (var chunk in sChunks)
        {
            if (drawClipmapLevel)
                DrawClipmapLevel(chunk);

            if (drawChunkBounds)
                DrawChunkBounds(chunk);

            if (drawSamples)
                DrawSamples(chunk);

            if (drawIsoCells)
                DrawIsoCells(chunk);

            if (drawNormals)
                DrawNormals(chunk);
        }
    }

    // ---------------------------------------------------------
    // CLIPMAP COLOR
    // ---------------------------------------------------------

    void DrawClipmapLevel(nChunkB chunk)
    {
        Color c = GetLevelColor(chunk.mRingCoords.Rlvl);

        Gizmos.color = new Color(c.r, c.g, c.b, 0.08f);

        Vector3 center =
            (Vector3)chunk.WorldOrigin +
            Vector3.one * (chunk.ChunkSize * 0.5f);

        Gizmos.DrawCube(center, Vector3.one * chunk.ChunkSize);
    }

    Color GetLevelColor(int level)
    {
        switch (level)
        {
            case 0: return Color.green;
            case 1: return Color.yellow;
            case 2: return Color.red;
            case 3: return Color.blue;
            case 4: return new Color(1f, 0.5f, 0f);
            case 5: return Color.magenta;
            case 6: return Color.cyan;
            case 7: return new Color(0.5f, 0.2f, 1f);
            default: return Color.white;
        }
    }

    // ---------------------------------------------------------
    // BOUNDS
    // ---------------------------------------------------------

    void DrawChunkBounds(nChunkB chunk)
    {
        Gizmos.color = Color.yellow;

        Vector3 center =
            (Vector3)chunk.WorldOrigin +
            Vector3.one * (chunk.ChunkSize * 0.5f);

        Gizmos.DrawWireCube(center, Vector3.one * chunk.ChunkSize);
    }

    // ---------------------------------------------------------
    // SAMPLE GRID
    // ---------------------------------------------------------

    void DrawSamples(nChunkB chunk)
    {
        int min = -nChunkB.PADDING;
        int max = chunk.mSize + nChunkB.PADDING;

        int cubeMin = 0;
        int cubeMax = chunk.mSize;

        float step = chunk.ChunkSize / chunk.mSize;

        Vector3 origin = chunk.WorldOrigin;

        Gizmos.color = Color.cyan;

        for (int z = min; z <= max; z++)
            for (int y = min; y <= max; y++)
                for (int x = min; x <= max; x++)
                {
                    bool draw;

                    if (edgesOnly)
                        draw = IsCubeEdgeOrPadding(x, y, z, cubeMin, cubeMax, min, max);
                    else
                        draw = IsCubeFaceOrPadding(x, y, z, cubeMin, cubeMax, min, max);

                    if (!draw)
                        continue;

                    Vector3 pos =
                        origin +
                        new Vector3(x * step, y * step, z * step);

                    Gizmos.DrawSphere(pos, step * 0.06f);
                }
    }

    bool IsCubeFaceOrPadding(
    int x, int y, int z,
    int cubeMin, int cubeMax,
    int min, int max)
    {
        return
            x == min || x == max ||
            y == min || y == max ||
            z == min || z == max ||

            x == cubeMin || x == cubeMax ||
            y == cubeMin || y == cubeMax ||
            z == cubeMin || z == cubeMax;
    }

    bool IsCubeEdgeOrPadding(
    int x, int y, int z,
    int cubeMin, int cubeMax,
    int min, int max)
    {
        int cubeBorders = 0;

        if (x == cubeMin || x == cubeMax) cubeBorders++;
        if (y == cubeMin || y == cubeMax) cubeBorders++;
        if (z == cubeMin || z == cubeMax) cubeBorders++;

        if (cubeBorders >= 2)
            return true;

        int paddingBorders = 0;

        if (x == min || x == max) paddingBorders++;
        if (y == min || y == max) paddingBorders++;
        if (z == min || z == max) paddingBorders++;

        return paddingBorders >= 2;
    }

    bool IsFace(int x, int y, int z, int min, int max)
    {
        return
            x == min || x == max ||
            y == min || y == max ||
            z == min || z == max;
    }

    bool IsEdge(int x, int y, int z, int min, int max)
    {
        int borders = 0;

        if (x == min || x == max) borders++;
        if (y == min || y == max) borders++;
        if (z == min || z == max) borders++;

        return borders >= 2;
    }



    // ---------------------------------------------------------
    // ISO CELLS
    // ---------------------------------------------------------

    void DrawIsoCells(nChunkB chunk)
    {
        int min = -nChunkB.PADDING;
        int max = chunk.mSize + nChunkB.PADDING;

        float step = chunk.ChunkSize / chunk.mSize;

        Vector3 origin = chunk.WorldOrigin;

        for (int z = min; z < max; z++)
            for (int y = min; y < max; y++)
                for (int x = min; x < max; x++)
                {
                    if (CellCrossesIso(chunk, x, y, z))
                    {
                        Vector3 pos =
                            origin +
                            new Vector3(
                                (x + 0.5f) * step,
                                (y + 0.5f) * step,
                                (z + 0.5f) * step
                            );

                        Gizmos.color = Color.red;

                        Gizmos.DrawCube(pos, Vector3.one * step * 0.3f);
                    }
                }
    }

    // ---------------------------------------------------------
    // NORMALS
    // ---------------------------------------------------------

    void DrawNormals(nChunkB chunk)
    {
        int min = -nChunkB.PADDING + 1;
        int max = chunk.mSize + nChunkB.PADDING - 1;

        float step = chunk.ChunkSize / chunk.mSize;

        Vector3 origin = chunk.WorldOrigin;

        Gizmos.color = Color.green;

        for (int z = min; z < max; z++)
            for (int y = min; y < max; y++)
                for (int x = min; x < max; x++)
                {
                    if (!CellCrossesIso(chunk, x, y, z))
                        continue;

                    DrawNormalSample(chunk, origin, step, x, y + 1, z); // arriba
                    DrawNormalSample(chunk, origin, step, x, y, z); // centro
                    DrawNormalSample(chunk, origin, step, x, y - 1, z); // abajo
                }
    }

    void DrawNormalSample(nChunkB chunk, Vector3 origin, float step, int x, int y, int z)
    {
        Vector3 pos =
            origin +
            new Vector3(x * step, y * step, z * step);

        Vector3 normal = ComputeNormal(chunk, x, y, z);

        Gizmos.DrawLine(pos, pos + normal * step * 0.8f);
    }

    // ---------------------------------------------------------
    // ISO TEST
    // ---------------------------------------------------------

    bool CellCrossesIso(nChunkB chunk, int x, int y, int z)
    {
        float iso = nChunkB.ISO_LEVEL_CONST;

        bool first = chunk.GetDensity(x, y, z) >= iso;

        for (int i = 1; i < 8; i++)
        {
            int ox = (i & 1);
            int oy = (i >> 1) & 1;
            int oz = (i >> 2) & 1;

            float d = chunk.GetDensity(x + ox, y + oy, z + oz);

            if ((d >= iso) != first)
                return true;
        }

        return false;
    }

    // ---------------------------------------------------------
    // NORMAL
    // ---------------------------------------------------------

    Vector3 ComputeNormal(nChunkB chunk, int x, int y, int z)
    {
        float dx =
            chunk.GetDensity(x - 1, y, z) -
            chunk.GetDensity(x + 1, y, z);

        float dy =
            chunk.GetDensity(x, y - 1, z) -
            chunk.GetDensity(x, y + 1, z);

        float dz =
            chunk.GetDensity(x, y, z - 1) -
            chunk.GetDensity(x, y, z + 1);

        Vector3 n = new Vector3(dx, dy, dz);

        if (n.sqrMagnitude < 0.00001f)
            return Vector3.up;

        return n.normalized;
    }
}