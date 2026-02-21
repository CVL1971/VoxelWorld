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
//    /// Determina la resoluci?n bas?ndose en la distancia al observador,
//    /// consultando los valores maestros de VoxelUtils.
//    /// </summary>
//    public int DetermineResolutionTier(float pDistSq)
//    {
//        // 1. Obtenemos el bloque de datos (offset) seg?n la distancia
//        int infoBlock = VoxelUtils.GetInfoDist(pDistSq);

//        // 2. Extraemos la resoluci?n definida en el primer ?ndice de dicho bloque
//        // [0] es Resoluci?n, [4] es Resoluci?n LOD1, [8] es Resoluci?n LOD2
//        return (int)VoxelUtils.LOD_DATA[infoBlock];
//    }

//    public void DispatchToRender(Chunk pChunk)
//    {
//        // Filtros futuros (frustum culling, etc.) se implementar?n aqu?.
//        // Actualmente, env?o directo a la cola de procesamiento.
//        mRenderQueue.Enqueue(pChunk, mGenerator);
//    }
//}


//*****************************************************************************

using System.Collections.Concurrent;
using UnityEngine;

public class DecimationManager
{
    private RenderQueueAsync mRenderQueue;
    private MeshGenerator mGenerator;
    private DensitySamplerQueueAsync mDensitySampler;

    /// <summary>
    /// Chunks pendientes de resample. No se encolan para remesh hasta que sus
    /// densidades est?n muestreadas para el LOD deseado (evita grietas inter-chunk).
    /// </summary>
    private ConcurrentDictionary<Chunk, int> mPendingResamples = new ConcurrentDictionary<Chunk, int>();

    public void Setup(RenderQueueAsync pQueue, DensitySamplerQueueAsync pDensitySampler, MeshGenerator pGenerator)
    {
        mRenderQueue = pQueue;
        mDensitySampler = pDensitySampler;
        mGenerator = pGenerator;
    }

    /// <summary>
    /// Solicita cambio de LOD. Marca el chunk con mTargetSize = pTargetRes (0 = sin marcar).
    /// Esa marca dispara la cascada: Redim ? Sample ? Remesh. No se encola remesh hasta tener datos listos.
    /// </summary>
    public void RequestLODChange(Chunk pChunk, int pTargetRes)
    {
        if (pChunk == null || pChunk.mIsEdited) return;
        mPendingResamples[pChunk] = pTargetRes;
    }

   

    /// <summary>
    /// Procesa hasta maxPerFrame chunks pendientes: Redim, luego Enqueue a remesh.
    /// Solo despu?s de tener datos listos se encola (sin estado "dato sucio" en la cola).
    /// </summary>
    public int ProcessPendingResamples(int maxPerFrame)
    {
        if (mPendingResamples.Count == 0) return 0;

        int processed = 0;
        foreach (var kv in mPendingResamples)
        {
            if (processed >= maxPerFrame) break;
            if (!mPendingResamples.TryRemove(kv.Key, out int targetRes)) continue;

            processed++;
            Chunk chunk = kv.Key;

            chunk.Redim(targetRes);
            mRenderQueue.Enqueue(chunk, mGenerator);

            int vBase = VoxelUtils.GetInfoRes(targetRes);
            int vLodIndex = (int)VoxelUtils.LOD_DATA[vBase + 3];
            Color vDebugColor = vLodIndex == 0 ? Color.white : (vLodIndex == 1 ? Color.blue : Color.red);
            chunk.DrawDebug(vDebugColor, 0.5f);
        }

        return processed;
    }
}