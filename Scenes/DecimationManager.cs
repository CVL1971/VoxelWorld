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
using System.Collections.Generic;
using UnityEngine;

public class DecimationManager
{
    private RenderStackAsync mRenderQueue;
    private MeshGenerator mGenerator;

    /// <summary>
    /// Chunks pendientes de resample. No se encolan para remesh hasta que sus
    /// densidades est?n muestreadas para el LOD deseado (evita grietas inter-chunk).
    /// </summary>
    private ConcurrentDictionary<Chunk, int> mPendingResamples = new ConcurrentDictionary<Chunk, int>();

    public void Setup(RenderStackAsync pQueue, MeshGenerator pGenerator)
    {
        mRenderQueue = pQueue;
        mGenerator = pGenerator;
    }

    /// <summary>
    /// Solicita cambio de LOD. Marca el chunk con mTargetSize = pTargetRes (0 = sin marcar).
    /// Esa marca dispara la cascada: Redim ? Sample ? Remesh. No se encola remesh hasta tener datos listos.
    /// </summary>
    public void RequestLODChange(Chunk pChunk, int pTargetRes)
    {
        if (pChunk == null || pChunk.mIsEdited) return;

        pChunk.mTargetSize = pTargetRes;
        pChunk.mAwaitingResample = true;
        mPendingResamples[pChunk] = pTargetRes;

        #if UNITY_EDITOR
        UnityEngine.Debug.Log($"[LOD] Vigilante ? Decimator: chunk {pChunk.mCoord} mSize {pChunk.mSize} ? targetRes {pTargetRes}");
        #endif
    }

   

    /// <summary>
    /// Procesa hasta maxPerFrame chunks pendientes: Redim + Sample, luego Enqueue a remesh.
    /// Solo despu?s de tener datos listos se encola (sin estado "dato sucio" en la cola).
    /// </summary>
    public int ProcessPendingResamples(int maxPerFrame)
    {
        if (mPendingResamples.Count == 0) return 0;

        var toProcess = new List<(Chunk chunk, int targetRes)>();
        int take = 0;
        foreach (var kv in mPendingResamples)
        {
            if (take >= maxPerFrame) break;
            toProcess.Add((kv.Key, kv.Value));
            take++;
        }

        foreach (var t in toProcess)
        {
            // 2. CORRECCIN DEL ERROR CS7036:
            // TryRemove requiere un segundo parmetro 'out' para devolver el valor eliminado.
            // Usamos '_' (discard) porque no necesitamos ese valor para nada.
            mPendingResamples.TryRemove(t.chunk, out _);
            // Con 3 caches por LOD: solo Redim (cambia mSize ? selecciona mSample0/1/2), sin resample
            t.chunk.Redim(t.targetRes);
            t.chunk.mAwaitingResample = false;
            t.chunk.mTargetSize = 0;
            // Asncrono: encola a la cola de mallado (Generate en worker, Apply en main thread)
            mRenderQueue.ForceEnqueue(t.chunk, mGenerator);
            //2 error, da por sentado de que los datos de un voxel son coherentes y estables por haberle actualizado
            //sus densidades de acuerdo a la nueva resolucion, pero para que el proceso de remesh se haba sobre datos
            //limpios, el voxel tiene que tener datos actualizados Y sus VECINOS NO PUEDEN ESTAR MARCADOS PARA CAMBIO DE LOD,
            //al ser este proceso asincrono, vecinos de este mismo voxel pueden estar justo despues de el en la cola, y se
            //remuestrearan en el siguiente frame, entramos en race condition, si el remesh calcula la mesh, antes que los vecinos
            //actualicen, grietas intraLOD. Estrategia, buscar vecinos, no puede ser una lista, si no hay se envia a remesh,
            //si mueven de posicion para actualizarse en el siguienmte frame,  la celda en surfacenet consta de 27-1 vecinos, 
            //eso hace que la atomizacion de este proceso no pueda ser menor de 27

            // Debug visual por LOD
            int vBase = VoxelUtils.GetInfoRes(t.targetRes);
            int vLodIndex = (int)VoxelUtils.LOD_DATA[vBase + 3];
            Color vDebugColor;

            if (vLodIndex == 0)
            {
                vDebugColor = Color.white;
            }
            else if (vLodIndex == 1)
                {
                    vDebugColor = Color.blue;
                }
                else
                {
                    vDebugColor = Color.red;
                }
         t.chunk.DrawDebug(vDebugColor, 0.5f);
        }

        #if UNITY_EDITOR
        if (toProcess.Count > 0)
            UnityEngine.Debug.Log($"[LOD] ProcessPendingResamples: {toProcess.Count} chunks -> Redim + ForceEnqueue (pendientes: {mPendingResamples.Count})");
        #endif

        return toProcess.Count;
    }
}