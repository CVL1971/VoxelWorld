using UnityEngine;

public static class SDFGenerator
{
    private const float MACRO_SCALE = 0.008f;
    private const float DETAIL_SCALE = 0.025f;
    private const float ROUGH_SCALE = 0.1f;
    private const float MAX_HEIGHT = 55.0f;
    private const float GROUND_OFFSET = 10.0f;

    private const float ISO_SURFACE = 0.5f;

    // Al subir esto a 100f, eliminamos el degradado suave y se vuelve "duro"
    private const float SMOOTHNESS = 2.0f;

    public static void Sample(Chunk pChunk)
    {
        int size = pChunk.mSize;
        Vector3Int origin = pChunk.mWorldOrigin;

        for (int z = 0; z < size; z++)
        {
            float worldZ = origin.z + z;
            for (int x = 0; x < size; x++)
            {
                float worldX = origin.x + x;

                float macro = Mathf.PerlinNoise(worldX * MACRO_SCALE, worldZ * MACRO_SCALE);
                float detail = Mathf.PerlinNoise(worldX * DETAIL_SCALE, worldZ * DETAIL_SCALE);
                float rough = Mathf.PerlinNoise(worldX * ROUGH_SCALE, worldZ * ROUGH_SCALE);

                float combined = (macro * 0.6f) + (detail * 0.35f) + (rough * 0.05f);
                float height = (Mathf.Pow(combined, 1.2f) * MAX_HEIGHT) + GROUND_OFFSET;

                for (int y = 0; y < size; y++)
                {
                    // La fórmula ahora devolverá 0 o 1 casi instantáneamente
                    float density = Mathf.Clamp01((height - (origin.y + y)) * SMOOTHNESS + ISO_SURFACE);
                    pChunk.SetDensity(x, y, z, density);
                    pChunk.SetSolid(x, y, z, (byte)(density >= ISO_SURFACE ? 1 : 0));
                }
            }
        }
    }

    public static float Sample(Vector3 worldPos)
    {
        float m = Mathf.PerlinNoise(worldPos.x * MACRO_SCALE, worldPos.z * MACRO_SCALE);
        float d = Mathf.PerlinNoise(worldPos.x * DETAIL_SCALE, worldPos.z * DETAIL_SCALE);
        float r = Mathf.PerlinNoise(worldPos.x * ROUGH_SCALE, worldPos.z * ROUGH_SCALE);
        float h = (Mathf.Pow((m * 0.6f) + (d * 0.35f) + (r * 0.05f), 1.2f) * MAX_HEIGHT) + GROUND_OFFSET;
        return Mathf.Clamp01((h - worldPos.y) * SMOOTHNESS + ISO_SURFACE);
    }

    //public static Vector3 CalculateNormal(Vector3 worldPos)
    //{
    //    const float h = 0.05f;

    //    // Mantenemos tu ruido de superficie
    //    float noiseEntry = Mathf.PerlinNoise(worldPos.x * 0.8f, worldPos.z * 0.8f) * 0.2f;

    //    // --- LUZ CORREGIDA ---
    //    // Invertimos el orden (Restamos Posterior a Anterior) para que la normal apunte al aire
    //    float dX = Sample(new Vector3(worldPos.x - h, worldPos.y, worldPos.z)) - Sample(new Vector3(worldPos.x + h, worldPos.y, worldPos.z));
    //    float dY = Sample(new Vector3(worldPos.x, worldPos.y - h, worldPos.z)) - Sample(new Vector3(worldPos.x, worldPos.y + h, worldPos.z));
    //    float dZ = Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z - h)) - Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z + h));

    //    Vector3 normal = new Vector3(dX, dY, dZ);

    //    //---TU JITTER ORIGINAL ---
    //    //Mantenemos esta mezcla exacta que es la que te da el look que buscas
    //   Vector3 jitter = new Vector3(
    //       Mathf.PerlinNoise(worldPos.y, worldPos.z) - 0.5f,
    //       0,
    //       Mathf.PerlinNoise(worldPos.x, worldPos.y) - 0.5f
    //   ) * 0.1f;

    //    return (normal + jitter).normalized;
    //    //return normal.normalized;
    //}

    public static Vector3 CalculateNormal(Vector3 worldPos)
    {
        // 1. Aumentamos 'h' ligeramente (de 0.05 a 0.15). 
        // Esto actúa como un "filtro" natural: ignora el ruido pequeño (manchas)
        // pero mantiene las formas grandes (perfil).
        const float h = 0.15f;

        // 2. Gradiente Central (Diferencia de densidades)
        // No necesitamos añadir ruido extra aquí, porque 'Sample' ya contiene
        // el ruido con el que generaste el mundo. La normal lo seguirá fielmente.
        float dX = Sample(new Vector3(worldPos.x - h, worldPos.y, worldPos.z)) - Sample(new Vector3(worldPos.x + h, worldPos.y, worldPos.z));
        float dY = Sample(new Vector3(worldPos.x, worldPos.y - h, worldPos.z)) - Sample(new Vector3(worldPos.x, worldPos.y + h, worldPos.z));
        float dZ = Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z - h)) - Sample(new Vector3(worldPos.x, worldPos.y, worldPos.z + h));

        Vector3 normal = new Vector3(dX, dY, dZ);

        // 3. Normalización con protección de seguridad
        // Si la zona es totalmente plana (normal cero), devolvemos Up para no romper el shader
        if (normal.sqrMagnitude < 0.0001f)
            return Vector3.up;

        return normal.normalized;
    }


}