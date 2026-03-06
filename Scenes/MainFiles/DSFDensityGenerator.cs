using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

public static class SDFGenerator
{
    public enum ChunkEarlyExitResult
    {
        Unknown,    // hay que samplear
        Solid,      // chunk sólido
        Empty       // chunk aire
    }


    // --- PARÁMETROS DE CONFIGURACIÓN ---
    private const float BASE_SCALE = 0.0008f;
    private const float WARP_SCALE = 0.002f;

    // El valor donde el Surface Nets coloca la malla.
    // En SDF puro suele ser 0, pero mantengo 0.5f por compatibilidad con tu sistema.
    private const float ISO_SURFACE = 0.5f;


    /// <summary>
    /// NUEVA LÓGICA: Devuelve la distancia vertical a la superficie + el offset de isosuperficie.
    /// Sin Clamps agresivos para que el Mesher tenga un gradiente real con el que trabajar.
    /// </summary>
    public static float Sample(Vector3 worldPos)
    {
        float h = GetGeneratedHeight(worldPos.x, worldPos.z);
        // Distancia real: Positivo (Sólido), Negativo (Aire).
        // Sumamos ISO_SURFACE para que el "Cero" del mundo esté en el 0.5 que espera tu Mesher.
        return (h - worldPos.y) + ISO_SURFACE;
    }

    /// <summary>
    /// Devuelve la distancia cruda (SDF puro). Útil para cálculos de normales.
    /// </summary>
    public static float GetRawDistance(Vector3 worldPos)
    {
        float h = GetGeneratedHeight(worldPos.x, worldPos.z);
        return h - worldPos.y;
    }

    public static float GetGeneratedHeight(float x, float z)
    {
        // 1. CONTINENTES (Estructura base)
        float C = Mathf.PerlinNoise(x * BASE_SCALE, z * BASE_SCALE);

        // 2. DOMAIN WARPING (Para que las montañas no parezcan nubes de Perlin)
        Vector2 p = Warp(x, z);

        // M: Montañas (Ridged Noise para crestas afiladas)
        float noiseM = Mathf.PerlinNoise(p.x * 0.01f, p.y * 0.01f);
        float M = 1.0f - Mathf.Abs((noiseM * 2.0f) - 1.0f);
        float mountain = M * M;

        // D: Detalle fino (Roca y suelo)
        float D = (Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f) * 2.0f) - 1.0f;

        // 3. MEZCLA LÓGICA (La personalidad geológica)
        float baseLayer = SmoothStep(-0.2f, 0.6f, (C * 2.0f) - 1.0f);

        // Las montañas solo crecen en los continentes
        mountain *= (baseLayer * baseLayer);
        float valley = baseLayer * (1.0f - mountain);

        // Resultado final en metros (Y)
        float h = (baseLayer * 40.0f) + (mountain * 120.0f) + (valley * 25.0f) + (D * 5.0f * baseLayer);

        return h;
    }

    /// <summary>
    /// Rellena el Chunk usando el nuevo gradiente de distancia.
    /// Optimizado con detección de límites de altura para descartar aire/sólido rápidamente.
    /// </summary>
    /// 
    public static void Sample(Chunk pChunk)
    {
        Vector3Int origin = pChunk.WorldOrigin;
        float chunkSize = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        ChunkEarlyExitResult vEval = DisableQuickEvaluate(pChunk);

        if (vEval != ChunkEarlyExitResult.Unknown)
        {
            pChunk.mBool1 = vEval == ChunkEarlyExitResult.Solid;
            pChunk.mBool2 = vEval == ChunkEarlyExitResult.Empty;

            pChunk.mGrid.MarkSurface(pChunk);
            pChunk.ReturnDCache();
            return;
        }


        //PROCESO NORMAL DE LLENADO DE CHUNK CON SUPERFICIE

        ArrayPool.DCache arrays;
        arrays = ArrayPool.Get(); // Con devolucion final la creamos, no necesidad de incremento

        float[] lod0 = arrays.mSample0;
        float[] lod1 = arrays.mSample1;
        float[] lod2 = arrays.mSample2;

        int[] resolutions = {(int)VoxelUtils.LOD_DATA[0],(int)VoxelUtils.LOD_DATA[4],(int)VoxelUtils.LOD_DATA[8]
};
        float[][] caches = { lod0, lod1, lod2 };

        pChunk.ResetGenericBools();

        for (int r = 0; r < resolutions.Length; r++)
        {
            int N = resolutions[r];
            //pChunk.mSize = N;

            int paddedRes = N + 3;
            float vStep = chunkSize / (float)N;

            float[] cache = caches[r];

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

                        float density = (height - worldY) + ISO_SURFACE;

                        // índice correcto usando el mismo layout que Chunk.IndexSample
                        int idx =
                            x +
                            paddedRes * (y + paddedRes * z);

                        cache[idx] = density;

                        if (x > 0 && x <= N && y > 0 && y <= N && z > 0 && z <= N)
                        {
                            if (density >= ISO_SURFACE) pChunk.mBool1 = true;
                            else pChunk.mBool2 = true;
                        }
                    }
                }
            }
        }

        //bool isSurface = ( pChunk.mBool1 && pChunk.mBool2) ;
        bool isSurface = true;
        
        pChunk.mGrid.MarkSurface(pChunk);


        if (isSurface)
        {
            pChunk.AssignDCache(arrays);
        }
        else
        {
            ArrayPool.Return(arrays);
            Interlocked.Decrement(ref arrays.mRefs);
            pChunk.ReturnDCache();

            return;
        }

        Interlocked.Decrement(ref arrays.mRefs); //tanto si asignamos como si devolvemos
                                                 //este clase ya no referenciara ese array.
    }




    public static ChunkEarlyExitResult HeightFivePointStrategy(Chunk pChunk)
    {
        const float margin = 8f;
        Vector3Int origin = pChunk.WorldOrigin;
        float chunkSize = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE;

        float h00 = GetGeneratedHeight(origin.x, origin.z);
        float h10 = GetGeneratedHeight(origin.x + chunkSize, origin.z);
        float h01 = GetGeneratedHeight(origin.x, origin.z + chunkSize);
        float h11 = GetGeneratedHeight(origin.x + chunkSize, origin.z + chunkSize);
        float hMid = GetGeneratedHeight(origin.x + chunkSize * 0.5f, origin.z + chunkSize * 0.5f);

        float minH = Mathf.Min(h00, Mathf.Min(h10, Mathf.Min(h01, Mathf.Min(h11, hMid))));
        float maxH = Mathf.Max(h00, Mathf.Max(h10, Mathf.Max(h01, Mathf.Max(h11, hMid))));

        if (origin.y > maxH + margin)
            return ChunkEarlyExitResult.Empty;

        if (origin.y + chunkSize < minH - margin)
            return ChunkEarlyExitResult.Solid;

        return ChunkEarlyExitResult.Unknown;
    }

    public static ChunkEarlyExitResult DisableQuickEvaluate(Chunk pChunk)
    {
        return ChunkEarlyExitResult.Unknown;
    }


    //public static float GetGeneratedHeight(float x, float z)
    //{
    //    // --- CONTINENTES (SIN WARP) ---
    //    float C = Mathf.PerlinNoise(x * 0.0008f, z * 0.0008f);

    //    // --- WARP SOLO PARA DETALLE Y MONTAÑAS ---
    //    Vector2 p = Warp(x, z);

    //    // M: Montañas (ridged)
    //    float noiseM = Mathf.PerlinNoise(p.x * 0.01f, p.y * 0.01f);
    //    float M = 1.0f - Mathf.Abs((noiseM * 2.0f) - 1.0f);

    //    // D: detalle fino
    //    float D = (Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f) * 2.0f) - 1.0f;

    //    // --- MEZCLA ---
    //    float baseLayer = SmoothStep(-0.2f, 0.6f, (C * 2.0f) - 1.0f);

    //    float mountain = M * M;
    //    mountain *= baseLayer * baseLayer;

    //    float valley = baseLayer * (1.0f - mountain);

    //    float h =
    //         baseLayer * 40.0f +
    //         mountain * 120.0f +
    //         valley * 25.0f +
    //         D * 5.0f * baseLayer; // evita ruido en océanos

    //    return h;
    //}

    /// <summary>
    /// Inyecta un mapa de alturas 2D como un volumen SDF coherente.
    /// </summary>

    public static Vector3 CalculateNormal(Vector3 worldPos)
    {
        const float h = 0.1f;
        float dX = Sample(new Vector3(worldPos.x + h, worldPos.y, worldPos.z)) - Sample(new Vector3(worldPos.x - h, worldPos.y, worldPos.z));
        // CORRECCIÓN: Usar worldPos.y para dY
        float dY = Sample(new Vector3(worldPos.x, worldPos.y + h, worldPos.z)) - Sample(new Vector3(worldPos.x, worldPos.y - h, worldPos.z));
        float dZ = Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z + h)) - Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z - h));

        return new Vector3(dX, dY, dZ).normalized;
    }

    static Vector2 Warp(float x, float z)
    {
        float wx = Mathf.PerlinNoise(x * WARP_SCALE, z * WARP_SCALE);
        float wz = Mathf.PerlinNoise(x * WARP_SCALE + 53.1f, z * WARP_SCALE + 17.7f);
        return new Vector2(x + (wx * 2 - 1) * 80f, z + (wz * 2 - 1) * 80f);
    }

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3.0f - 2.0f * t);
    }

    /// <summary>
    /// Carga un Heightmap (EXR/PNG/JPG) y reconstruye densidades con interpolación.
    /// </summary>
    public static void LoadHeightmapToGrid(Grid pGrid, string filePath = @"E:\maps\1.exr")
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.Log("No existe el archivo en " + filePath);
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
    }
}








