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
    private static int sSamplerDiscardCount = 0;

    private readonly Grid mGrid;
    private readonly RenderQueueAsync mRenderQueueAsync;
    private readonly DensitySamplerQueueAsync mDensitySampler;
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
        mRenderQueueAsync = new RenderQueueAsync(pGrid, maxParallelRender, ForceEnqueueDensity);
        mDensitySampler = new DensitySamplerQueueAsync(maxParallelDensity);
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

    public void ForceEnqueueDensity(Chunk pChunk)
    {
        mDensitySampler.ForceEnqueue(pChunk);
    }

    public bool TryDequeueDensityResult(out (Chunk chunk, int generationId) pResult)
    {
        return mDensitySampler.DensitySamplerResult.TryDequeue(out pResult);
    }

    // ==========================================
    // GESTIÓN DE MALLADO (RENDER QUEUE)
    // ==========================================
    public void EnqueueRender(Chunk pChunk, MeshGenerator pGenerator)
    {
        mRenderQueueAsync.Enqueue(pChunk, pGenerator);
    }

    public void ForceEnqueueRender(Chunk pChunk, MeshGenerator pGenerator)
    {
        mRenderQueueAsync.ForceEnqueue(pChunk, pGenerator);
    }

    public bool TryDequeueRenderResult(out (Chunk chunk, MeshData mesh, int generationId) pResult)
    {
        return mRenderQueueAsync.mResultsLOD.TryDequeue(out pResult);
    }

    public void ApplyRenderResult(Chunk pChunk, MeshData pData, int pExpectedGenerationId)
    {
        mRenderQueueAsync.Apply(pChunk, pData, pExpectedGenerationId);
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

        while (TryDequeueDensityResult(out var r))
        {
            if (r.chunk.mGenerationId != r.generationId)
            {
                if (++sSamplerDiscardCount <= 30)
                    UnityEngine.Debug.LogWarning($"[Sampler] DESCARTADO genId | Slot={r.chunk.mCoord} Global={r.chunk.mGlobalCoord} chunkGen={r.chunk.mGenerationId} resultGen={r.generationId}");
                ForceEnqueueDensity(r.chunk);
                continue;
            }
            mGrid.MarkSurface(r.chunk);
            EnqueueRender(r.chunk, mMeshGenerator);
        }

        while (TryDequeueRenderResult(out var r))
        {
            if (r.chunk.mGenerationId != r.generationId)
            {
                ForceEnqueueDensity(r.chunk);
                continue;
            }
            ApplyRenderResult(r.chunk, r.mesh, r.generationId);
        }
    }
}