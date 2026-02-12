//using UnityEngine;

//public class DecimationManager
//{
//    private RenderQueue mRenderQueue;
//    private MeshGenerator mGenerator;

//    public void Setup(RenderQueue pQueue, MeshGenerator pGenerator)
//    {
//        mRenderQueue = pQueue;
//        mGenerator = pGenerator;
//    }

//    /// <summary>
//    /// Determina la resolución basándose en la distancia al observador,
//    /// consultando los valores maestros de VoxelUtils.
//    /// </summary>
//    public int DetermineResolutionTier(float pDistSq)
//    {
//        // 1. Obtenemos el bloque de datos (offset) según la distancia
//        int infoBlock = VoxelUtils.GetInfoDist(pDistSq);

//        // 2. Extraemos la resolución definida en el primer índice de dicho bloque
//        // [0] es Resolución, [4] es Resolución LOD1, [8] es Resolución LOD2
//        return (int)VoxelUtils.LOD_DATA[infoBlock];
//    }

//    public void DispatchToRender(Chunk pChunk)
//    {
//        // Filtros futuros (frustum culling, etc.) se implementarán aquí.
//        // Actualmente, envío directo a la cola de procesamiento.
//        mRenderQueue.Enqueue(pChunk, mGenerator);
//    }
//}


//*****************************************************************************

using UnityEngine;

public class DecimationManager
{
    private RenderQueueMonohilo mRenderQueue;
    private MeshGenerator mGenerator;

    public void Setup(RenderQueueMonohilo pQueue, MeshGenerator pGenerator)
    {
        mRenderQueue = pQueue;
        mGenerator = pGenerator;
    }

    //public int DetermineResolutionTier(float pDistSq)
    //{
    //    int infoBlock = VoxelUtils.GetInfoDist(pDistSq);
    //    return (int)VoxelUtils.LOD_DATA[infoBlock];
    //}

    /// <summary>
    /// COMPORTAMIENTO DE TEST: Suspendemos RenderQueue y Pool.
    /// Pintamos el chunk según el LOD detectado por el Vigilante.
    /// </summary>
    public void DispatchToRender(Chunk pChunk)
    {
        // 1. Identificamos el bloque de datos actual para saber el color
        // Usamos GetInfoRes para saber en qué escalón de la Autoridad estamos
        int vBase = VoxelUtils.GetInfoRes(pChunk.mTargetSize);
        int vLodIndex = (int)VoxelUtils.LOD_DATA[vBase + 3];

        Color vDebugColor;

        switch (vLodIndex)
        {
            case 0: // LOD Máximo (Res 32)
                vDebugColor = Color.white; // Sin cambios notables
                break;
            case 1: // LOD Medio (Res 16)
                vDebugColor = Color.blue;
                break;
            case 2: // LOD Bajo (Res 8)
                vDebugColor = Color.red;
                break;
            default:
                vDebugColor = Color.gray;
                break;
        }

        // 2. Dibujamos la caja en el mundo. 
        // Usamos una duración de 0.5s para que persista entre ciclos del Vigilante (200ms)
        pChunk.DrawDebug(vDebugColor, 0.5f);
        mRenderQueue.Enqueue(pChunk, mGenerator);
    }
}