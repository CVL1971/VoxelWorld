using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Centraliza toda la gestión de tareas relacionadas al encolado de trabajos asíncronos.
/// Incluye la lógica de LOD (resamples pendientes).
/// </summary>
public class ChunkPipeline
{
    // ==========================================
    // ESTADO Y DEPENDENCIAS
    // ==========================================
    private readonly Grid mGrid;
    private readonly RenderQueueAsyncEpoch mRenderQueueAsync;
    private readonly DensitySamplerQueueEpoch mDensitySampler;
    private MeshGenerator mMeshGenerator;

    /// <summary>
    /// Chunks pendientes de resample LOD. No se encolan para remesh hasta que sus
    /// densidades estén muestreadas para el LOD deseado (evita grietas inter-chunk).
    /// </summary>
    private ConcurrentDictionary<Chunk, int> mPendingResamples = new ConcurrentDictionary<Chunk, int>();

    // ==========================================
    // CONFIGURACIÓN E INICIALIZACIÓN
    // ==========================================
    public ChunkPipeline(Grid pGrid, int maxParallelRender = 8, int maxParallelDensity = 10)
    {
        mGrid = pGrid;
        mGrid.SetPipeline(this);
        mRenderQueueAsync = new RenderQueueAsyncEpoch(pGrid, maxParallelRender);
        mDensitySampler = new DensitySamplerQueueEpoch(maxParallelDensity);
    }

    public void Setup(MeshGenerator pGenerator)
    {
        mMeshGenerator = pGenerator;
    }

    // ==========================================
    // GESTIÓN DE MUESTREO (DENSITY SAMPLER)
    // ==========================================
    public void EnqueueDensity(Chunk pChunk)
    {
        mDensitySampler.Enqueue(pChunk);
    }

    //public bool TryDequeueDensityResult(out Chunk pChunk)
    //{
    //    return mDensitySampler.DensitySamplerResult.TryDequeue(out pChunk);
    //}

    public bool TryDequeueDensityResult(out Chunk pChunk, out bool pStructural)
    {
        if (mDensitySampler.DensitySamplerResult.TryDequeue(out DensitySamplerResultItem result))
        {
            pChunk = result.mChunk;
            pStructural = result.mStructural;
            return true;
        }


        pChunk = null;
        pStructural = false;
        return false;


    }




    // ==========================================
    // GESTIÓN DE MALLADO (RENDER QUEUE)
    // ==========================================
    public void EnqueueRender(Chunk pChunk, MeshGenerator pGenerator)
    {
        mRenderQueueAsync.Enqueue(pChunk, pGenerator);
    }

    public void EnqueueRender(Chunk pChunk, MeshGenerator pGenerator, bool pBool)
    {
        mRenderQueueAsync.Enqueue(pChunk, pGenerator, pBool);
    }

    public bool TryDequeueRenderResult(out (Chunk chunk, MeshData mesh) pResult)
    {
        return mRenderQueueAsync.mResultsLOD.TryDequeue(out pResult);
    }

    public void ApplyRenderResult(Chunk pChunk, MeshData pData)
    {
        mRenderQueueAsync.Apply(pChunk, pData);
    }

    // ==========================================
    // SISTEMA DE NIVEL DE DETALLE (LOD)
    // ==========================================
    /// <summary>
    /// Solicita cambio de LOD. Marca el chunk con mTargetSize = pTargetRes (0 = sin marcar).
    /// Esa marca dispara la cascada: Redim → Sample → Remesh. No se encola remesh hasta tener datos listos.
    /// </summary>
    public void RequestLODChange(Chunk pChunk, int pTargetRes)
    {
        if (pChunk == null || pChunk.mIsEdited) return;
        mPendingResamples[pChunk] = pTargetRes;
    }

    /// <summary>
    /// Procesa hasta maxPerFrame chunks pendientes: Redim, luego Enqueue a remesh.
    /// Solo después de tener datos listos se encola (sin estado "dato sucio" en la cola).
    /// </summary>
    public int ProcessPendingResamples(int maxPerFrame)
    {
        if (mPendingResamples.Count == 0 || mMeshGenerator == null) return 0;

        int processed = 0;
        foreach (var kv in mPendingResamples)
        {
            if (processed >= maxPerFrame) break;
            if (!mPendingResamples.TryRemove(kv.Key, out int targetRes)) continue;

            processed++;
            Chunk chunk = kv.Key;

            chunk.Redim(targetRes);
            EnqueueRender(chunk, mMeshGenerator);

            int vBase = VoxelUtils.GetInfoRes(targetRes);
            int vLodIndex = (int)VoxelUtils.LOD_DATA[vBase + 3];
            Color vDebugColor = vLodIndex == 0 ? Color.white : (vLodIndex == 1 ? Color.blue : Color.red);
            chunk.DrawDebug(vDebugColor, 0.5f);
        }

        return processed;
    }

    // ==========================================
    // BUCLE PRINCIPAL DE ACTUALIZACIÓN (UPDATE)
    // ==========================================
    /// <summary>
    /// Procesa streaming, LOD pendientes, resultados de density sampler y aplica resultados de render.
    /// </summary>
    public void Update(Vector3 pPlayerPosition)
    {
        if (mGrid.TryGetNewPlayerChunk(pPlayerPosition, out Vector3Int newChunk))
        {
            mGrid.UpdateStreamingX(newChunk);
            mGrid.UpdateStreamingY(newChunk);
            mGrid.UpdateStreamingZ(newChunk);
        }

        ProcessPendingResamples(20);

        while (TryDequeueDensityResult(out Chunk densityChunk, out bool pStructural))
        {
            mGrid.MarkSurface(densityChunk);
            EnqueueRender(densityChunk, mMeshGenerator, pStructural);
        }

        while (TryDequeueRenderResult(out var r))
        {
            ApplyRenderResult(r.chunk, r.mesh);
        }
    }
}