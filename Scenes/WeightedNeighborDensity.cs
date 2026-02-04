using UnityEngine;

public static class WeightedNeighborDensity
{
    // Pesos fijos según tu configuración anterior
    private const float FACE_WEIGHT = 1.0f;
    private const float EDGE_WEIGHT = 0.7f;
    private const float CORNER_WEIGHT = 0.5f;

    /// <summary>
    /// Método estático para muestrear la densidad basado en vecinos.
    /// Invocación: WeightedNeighborDensity.Sample(...)
    /// </summary>
    public static float Sample(
        Chunk pChunk,
        Chunk[] allChunks,
        Vector3Int worldSize,
        int pX,
        int pY,
        int pZ
    )
    {
        float sum = 0.0f;
        float max = 0.0f;

        // Bucle 3x3x3 sobre los vecinos
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    float weight = GetWeight(dx, dy, dz);
                    if (weight <= 0.0f) continue;

                    max += weight;

                    // Consulta global del estado del voxel (sólido/aire)
                    if (VoxelUtils.IsSolidGlobal(pChunk, allChunks, worldSize, pX + dx, pY + dy, pZ + dz))
                    {
                        sum += weight;
                    }
                }
            }
        }

        return (max > 0) ? (sum / max) : 0.0f;
    }

    private static float GetWeight(int dx, int dy, int dz)
    {
        // La suma de los valores absolutos nos dice si es cara, arista o esquina
        int absSum = Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz);

        return absSum switch
        {
            1 => FACE_WEIGHT,   // Solo un eje distinto de 0: Cara
            2 => EDGE_WEIGHT,   // Dos ejes distintos de 0: Arista
            3 => CORNER_WEIGHT, // Tres ejes distintos de 0: Esquina
            _ => 0.0f           // El propio voxel (0,0,0) o fuera de rango
        };
    }

    
}



