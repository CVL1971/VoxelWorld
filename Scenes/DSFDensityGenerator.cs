using UnityEngine;
using System.IO;

public static class SDFGenerator
{
    // Parámetros de configuración del mundo
    private const float BASE_SCALE = 0.006f;
    private const float MOUNTAIN_SCALE = 0.015f;
    private const float DETAIL_SCALE = 0.04f;

    private const float MAX_HEIGHT = 60.0f;
    private const float ISO_SURFACE = 0.5f;
    private const float SMOOTHNESS = 2.0f; // Mantenemos un valor bajo para suavidad natural

  

    public static float Sample(Vector3 worldPos)
    {
        float h = GetGeneratedHeight(worldPos.x, worldPos.z);
        // La densidad es la diferencia entre la altura generada y el Y actual
        return Mathf.Clamp01((h - worldPos.y) * SMOOTHNESS + ISO_SURFACE);
    }

    public static float GetRawDistance(Vector3 worldPos)
    {
        float h = GetGeneratedHeight(worldPos.x, worldPos.z);
        return worldPos.y - h;
    }


    public static float GetGeneratedHeight(float x, float z)
    {
        // --- CONTINENTES (SIN WARP) ---
        float C = Mathf.PerlinNoise(x * 0.0008f, z * 0.0008f);

        // --- WARP SOLO PARA DETALLE Y MONTAÑAS ---
        Vector2 p = Warp(x, z);

        // M: Montañas (ridged)
        float noiseM = Mathf.PerlinNoise(p.x * 0.01f, p.y * 0.01f);
        float M = 1.0f - Mathf.Abs((noiseM * 2.0f) - 1.0f);

        // D: detalle fino
        float D = (Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f) * 2.0f) - 1.0f;

        // --- MEZCLA ---
        float baseLayer = SmoothStep(-0.2f, 0.6f, (C * 2.0f) - 1.0f);

        float mountain = M * M;
        mountain *= baseLayer * baseLayer;

        float valley = baseLayer * (1.0f - mountain);

        float h =
             baseLayer * 40.0f +
             mountain * 120.0f +
             valley * 25.0f +
             D * 5.0f * baseLayer; // evita ruido en océanos

        return h;
    }

    public static void Sample(Chunk pChunk)
    {
        int savedSize = pChunk.mSize;
        pChunk.ResetGenericBools();

        // Rellenar los 3 caches (mSample0, mSample1, mSample2) para que LOD funcione.
        // SetDensity usa GetActiveCache() según mSize, así que temporalmente cambiamos mSize.
        int[] resolutions = { (int)VoxelUtils.LOD_DATA[0], (int)VoxelUtils.LOD_DATA[4], (int)VoxelUtils.LOD_DATA[8] };

        foreach (int N in resolutions)
        {
            pChunk.mSize = N;
            int paddedRes = N + 3;
            Vector3Int origin = pChunk.mWorldOrigin;
            float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / (float)N;

            for (int z = 0; z < paddedRes; z++)
            {
                float worldZ = origin.z + ((z - 1) * vStep);
                for (int x = 0; x < paddedRes; x++)
                {
                    float worldX = origin.x + ((x - 1) * vStep);
                    float height = GetGeneratedHeight(worldX, worldZ);

                    for (int y = 0; y < paddedRes; y++)
                    {
                        float worldY = origin.y + ((y - 1) * vStep);
                        float density = Mathf.Clamp01((height - worldY) * SMOOTHNESS + ISO_SURFACE);

                        pChunk.SetDensity(x - 1, y - 1, z - 1, density);

                        if (x > 0 && x <= N && y > 0 && y <= N && z > 0 && z <= N)
                        {
                            if (density >= ISO_SURFACE) pChunk.mBool1 = true;
                            else pChunk.mBool2 = true;
                        }
                    }
                }
            }
        }

        pChunk.mSize = savedSize;
    }


    /// <summary>
    /// Implementación de SmoothStep estándar de HLSL/GLSL para C#
    /// </summary>
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        // Clampeamos el valor entre 0 y 1 basándonos en los bordes
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        // Fórmula polinómica: 3t^2 - 2t^3
        return t * t * (3.0f - 2.0f * t);
    }

    static Vector2 Warp(float x, float z)
    {
        float wx = Mathf.PerlinNoise(x * 0.002f, z * 0.002f);
        float wz = Mathf.PerlinNoise(x * 0.002f + 53.1f, z * 0.002f + 17.7f);

        wx = (wx * 2f - 1f);
        wz = (wz * 2f - 1f);

        return new Vector2(x + wx * 80f, z + wz * 80f);
    }

    public static Vector3 CalculateNormal(Vector3 worldPos)
    {
        const float h = 0.1f;
        float dX = Sample(new Vector3(worldPos.x - h, worldPos.y, worldPos.z)) - Sample(new Vector3(worldPos.x + h, worldPos.y, worldPos.z));
        float dY = Sample(new Vector3(worldPos.x, worldPos.y - h, worldPos.z)) - Sample(new Vector3(worldPos.x, worldPos.y + h, worldPos.z));
        float dZ = Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z - h)) - Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z + h));

        Vector3 n = new Vector3(dX, dY, dZ);
        return n.sqrMagnitude < 0.0001f ? Vector3.up : n.normalized;
    }
    public static Vector3 CalculateNormalRaw(Vector3 worldPos)
    {
        // Subimos h a 0.25 (un cuarto de voxel). 
        // Esto promedia el ruido y estabiliza la normal radicalmente.
        const float h = 0.25f;
        float dX = GetRawDistance(new Vector3(worldPos.x + h, worldPos.y, worldPos.z)) - GetRawDistance(new Vector3(worldPos.x - h, worldPos.y, worldPos.z));
        float dY = GetRawDistance(new Vector3(worldPos.x, worldPos.y + h, worldPos.z)) - GetRawDistance(new Vector3(worldPos.x, worldPos.y - h, worldPos.z));
        float dZ = GetRawDistance(new Vector3(worldPos.x, worldPos.y, worldPos.z + h)) - GetRawDistance(new Vector3(worldPos.x, worldPos.y, worldPos.z - h));
        return new Vector3(dX, dY, dZ).normalized;
    }

    /// <summary>
    /// Carga un Heightmap (EXR/PNG/JPG) y reconstruye densidades con interpolación.
    /// </summary>
    public static void LoadHeightmapToGrid(Grid pGrid, string filePath = @"E:\maps\1.exr")
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("No existe el archivo en " + filePath);
            return;
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RFloat, false, true);
        tex.LoadImage(bytes);
        tex.filterMode = FilterMode.Bilinear; // Suavizado entre píxeles

        int res = pGrid.mChunks[0].mSize;
        float maxHeight = pGrid.mSizeInChunks.y * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / res;

        int texW = tex.width;
        int texH = tex.height;

        for (int cz = 0; cz < pGrid.mSizeInChunks.z; cz++)
        {
            for (int cx = 0; cx < pGrid.mSizeInChunks.x; cx++)
            {
                for (int cy = 0; cy < pGrid.mSizeInChunks.y; cy++)
                {
                    int chunkIdx = cx + (cy * pGrid.mSizeInChunks.x) + (cz * pGrid.mSizeInChunks.x * pGrid.mSizeInChunks.y);
                    Chunk chunk = pGrid.mChunks[chunkIdx];
                    chunk.ResetGenericBools();

                    for (int vz = 0; vz < res; vz++)
                    {
                        for (int vx = 0; vx < res; vx++)
                        {
                            int px = (cx * res) + vx;
                            int pz = (cz * res) + vz;

                            float u = (float)px / (pGrid.mSizeInChunks.x * res);
                            float v = (float)pz / (pGrid.mSizeInChunks.z * res);

                            float h = tex.GetPixelBilinear(u, v).r * maxHeight;

                            for (int vy = 0; vy < res; vy++)
                            {
                                float worldY = (cy * VoxelUtils.UNIVERSAL_CHUNK_SIZE) + (vy * vStep);

                                // Gradiente suave de 0.5f para que las normales se vean con profundidad
                                float density = Mathf.Clamp01((h - worldY) * 0.5f + 0.5f);

                                chunk.SetDensity(vx, vy, vz, density);

                                if (density >= 0.5f)
                                {
                                    
                                    chunk.mBool1 = true;
                                }
                                else
                                {
                                   
                                    chunk.mBool2 = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        Object.DestroyImmediate(tex);
        Debug.Log("<color=cyan>[Heightmap]</color> Carga 32-bit finalizada.");
    }


    //public static void ResampleData(Chunk pChunk)
    //{
    //    int res = pChunk.mSize; // Resolución actual (8, 16 o 32)
    //    Vector3Int origin = pChunk.mWorldOrigin;

    //    // Calculamos el paso (step) necesario para cubrir 32 unidades físicas
    //    // Si res es 32, step es 1.0. Si res es 8, step es 4.0.
    //    float step = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / res;

    //    for (int z = 0; z < res; z++)
    //    {
    //        for (int y = 0; y < res; y++)
    //        {
    //            for (int x = 0; x < res; x++)
    //            {
    //                // Calculamos la posición real en el mundo usando el paso
    //                Vector3 worldPos = new Vector3(
    //                    origin.x + (x * step),
    //                    origin.y + (y * step),
    //                    origin.z + (z * step)
    //                );

    //                // Usamos tu generador original para obtener la densidad
    //                float density = SDFGenerator.Sample(worldPos);

    //                // Inyectamos los datos en el nuevo array del chunk
    //                pChunk.SetDensity(x, y, z, density);
    //                pChunk.SetSolid(x, y, z, (byte)(density >= 0.5f ? 1 : 0));
    //            }
    //        }
    //    }
    //}


    //private static float GetGeneratedHeight(float x, float z)
    //{
    //    // 1. Ruido Base (Grandes masas de tierra)
    //    float baseLand = Mathf.PerlinNoise(x * BASE_SCALE, z * BASE_SCALE);

    //    // 2. Ridged Noise (Para crestas de montañas afiladas)
    //    // Invertimos el ruido para que los "valles" del Perlin sean "picos"
    //    float mountainNoise = Mathf.PerlinNoise(x * MOUNTAIN_SCALE, z * MOUNTAIN_SCALE);
    //    float ridges = 1.0f - Mathf.Abs(mountainNoise * 2.0f - 1.0f);
    //    ridges = ridges * ridges; // Exponencial para afilar aún más las crestas

    //    // 3. Ruido de detalle (Pequeñas irregularidades)
    //    float detail = (Mathf.PerlinNoise(x * DETAIL_SCALE, z * DETAIL_SCALE) * 2.0f - 1.0f) * 2.0f;

    //    // Combinación: Las montañas solo aparecen donde el baseLand es alto
    //    float height = (baseLand * 30.0f) + (ridges * baseLand * 40.0f) + detail;

    //    return height + 15.0f; // Offset de elevación mínima
    //}
}