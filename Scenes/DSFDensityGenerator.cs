using UnityEngine;

public static class SDFGenerator
{
    // Parámetros de configuración del mundo
    private const float BASE_SCALE = 0.006f;
    private const float MOUNTAIN_SCALE = 0.015f;
    private const float DETAIL_SCALE = 0.04f;

    private const float MAX_HEIGHT = 60.0f;
    private const float ISO_SURFACE = 0.5f;
    private const float SMOOTHNESS = 2.0f; // Mantenemos un valor bajo para suavidad natural

    public static void Sample(Chunk pChunk)
    {
        int size = pChunk.mSize;
        Vector3Int origin = pChunk.mWorldOrigin;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector3 worldPos = new Vector3(origin.x + x, origin.y + y, origin.z + z);
                    float density = Sample(worldPos);

                    pChunk.SetDensity(x, y, z, density);
                    pChunk.SetSolid(x, y, z, (byte)(density >= ISO_SURFACE ? 1 : 0));
                }
            }
        }
    }

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

    private static float GetGeneratedHeight(float x, float z)
    {
        // 1. Ruido Base (Grandes masas de tierra)
        float baseLand = Mathf.PerlinNoise(x * BASE_SCALE, z * BASE_SCALE);

        // 2. Ridged Noise (Para crestas de montañas afiladas)
        // Invertimos el ruido para que los "valles" del Perlin sean "picos"
        float mountainNoise = Mathf.PerlinNoise(x * MOUNTAIN_SCALE, z * MOUNTAIN_SCALE);
        float ridges = 1.0f - Mathf.Abs(mountainNoise * 2.0f - 1.0f);
        ridges = ridges * ridges; // Exponencial para afilar aún más las crestas

        // 3. Ruido de detalle (Pequeñas irregularidades)
        float detail = (Mathf.PerlinNoise(x * DETAIL_SCALE, z * DETAIL_SCALE) * 2.0f - 1.0f) * 2.0f;

        // Combinación: Las montañas solo aparecen donde el baseLand es alto
        float height = (baseLand * 30.0f) + (ridges * baseLand * 40.0f) + detail;

        return height + 15.0f; // Offset de elevación mínima
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
}