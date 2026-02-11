using UnityEngine;

public class DecimationManager
{
    private RenderQueue mRenderQueue;
    private MeshGenerator mGenerator;

    public void Setup(RenderQueue pQueue, MeshGenerator pGenerator)
    {
        mRenderQueue = pQueue;
        mGenerator = pGenerator;
    }

    /// <summary>
    /// Determina la resolución basándose en la distancia al observador,
    /// consultando los valores maestros de VoxelUtils.
    /// </summary>
    public int DetermineResolutionTier(float pDistSq)
    {
        // 1. Obtenemos el bloque de datos (offset) según la distancia
        int infoBlock = VoxelUtils.GetInfoDist(pDistSq);

        // 2. Extraemos la resolución definida en el primer índice de dicho bloque
        // [0] es Resolución, [4] es Resolución LOD1, [8] es Resolución LOD2
        return (int)VoxelUtils.LOD_DATA[infoBlock];
    }

    public void DispatchToRender(Chunk pChunk)
    {
        // Filtros futuros (frustum culling, etc.) se implementarán aquí.
        // Actualmente, envío directo a la cola de procesamiento.
        mRenderQueue.Enqueue(pChunk, mGenerator);
    }
}