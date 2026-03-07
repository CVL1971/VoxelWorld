using System;
using System.Collections.Concurrent;
using System.Text;
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
    // BUCLE PRINCIPAL DE ACTUALIZACIÓN (UPDATE)
    // ==========================================

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

            if (densityChunk.mBool1 && densityChunk.mBool2)
                {
                EnqueueRender(densityChunk, mMeshGenerator, pStructural);
            }
        }

        while (TryDequeueRenderResult(out var r))
        {
            ApplyRenderResult(r.chunk, r.mesh);
        }
    }

    // ==========================================
    // PROCESAR CHUNKS DE LOD PENDIENTES: REDIM, ENCOLAR RENDER, DIBUJAR DEBUG.
    // El Vigilante solo selecciona chunks con BIT_SURFACE (ya tienen densidad en las 3 resoluciones).
    // No requiere re-sampleo: Redim cambia el puntero activo; EnqueueRender directamente.
    // ==========================================

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
    // OBTENER RESULTADOS DE DENSTITY COMPLETRADOS Y CONSUMIRLOS
    // ==========================================

    public bool TryDequeueDensityResult(out Chunk pChunk, out bool pStructural)
    {
        if (mDensitySampler.DensitySamplerResult.TryDequeue(out DensitySamplerResultItem result))
        {
            pChunk = result.mChunk;
            pStructural = result.mStructural;
            //AdaptChunk(pChunk);
            return true;

        }


        pChunk = null;
        pStructural = false;
        return false;


    }






    // ==========================================
    // OBTENER MALLAS GENERADAS
    // ==========================================

    public bool TryDequeueRenderResult(out (Chunk chunk, MeshData mesh) pResult)
    {
        return mRenderQueueAsync.mResultsLOD.TryDequeue(out pResult);
    }

    // ==========================================
    // ==========================================
    // ==========================================
    // ==========================================


    public bool AdaptChunk(Chunk vChunk)
    {

        if (vChunk.mBool1 && vChunk.mBool2)
        {
            if (vChunk.mViewGO == null) vChunk.mViewGO = GameObjectPool.Get(vChunk);
            return true;

        }

        else

        {
            if (vChunk.mViewGO != null)
            {
                GameObjectPool.Return(vChunk);
                vChunk.mViewGO = null;
            }

            return false;

        }

    }





    // ==========================================
    // GESTIÓN DE MUESTREO (DENSITY SAMPLER)
    // ==========================================
    public void EnqueueDensity(Chunk pChunk)
    {
        mDensitySampler.Enqueue(pChunk);
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





}