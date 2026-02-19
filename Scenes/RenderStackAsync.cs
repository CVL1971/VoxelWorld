using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RenderStackAsync
{
    private readonly Grid mGrid;

    // FIFO: Init primero, luego LOD changes (evita que LOD sea sobrescrito por Init)
    public ConcurrentQueue<RenderJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<KeyValuePair<Chunk, MeshData>> mResultsLOD = new();

    // ---- CONFIGURACIÓN ----
    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    public RenderStackAsync(Grid pGrid, int maxParallel = 10)
    {
        mGrid = pGrid;
        mSlots = new SemaphoreSlim(maxParallel);
    }

    // ---- ENQUEUE ----
    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;

        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new RenderJob(pChunk, pGenerator));
            StartWorker();
        }
    }

    /// <summary>
    /// Fuerza encolar aunque el chunk esté en la cola (para LOD changes).
    /// Evita que TryAdd bloquee cuando Init y LOD compiten por el mismo chunk.
    /// </summary>
    public void ForceEnqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;

        mQueue.Enqueue(new RenderJob(pChunk, pGenerator));
        mInWait.TryAdd(pChunk, 0); // no bloquea si ya está
        StartWorker();
    }

    // ---- WORKER LOOP ----
    private void StartWorker()
    {
        if (Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) != 0)
            return;

        Task.Run(ProcessLoop);
    }

    private async Task ProcessLoop()
    {
        while (true)
        {
            if (!mQueue.TryDequeue(out var job))
            {
                mWorkerRunning = 0;

                // si alguien encoló mientras salíamos
                if (!mQueue.IsEmpty && Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) == 0)
                    continue;

                return;
            }

            await mSlots.WaitAsync();

            _ = Task.Run(() =>
            {
                try { Execute(job); }
                finally
                {
                    mInWait.TryRemove(job.mChunk, out _);
                    mSlots.Release();
                }
            });
        }
    }

    // ---- EJECUCIÓN REAL (Sin cambios) ----
    private void Execute(RenderJob vRequest)
    {
        Chunk vChunk = vRequest.mChunk;

        if (vChunk.mTargetSize > 0)
            vChunk.Redim(vChunk.mTargetSize);

        MeshData vData = vRequest.mMeshGenerator.Generate(
            vChunk,
            mGrid.mChunks,
            mGrid.mSizeInChunks
        );

        vChunk.mTargetSize = 0;

        if (vData != null)
            mResultsLOD.Enqueue(new KeyValuePair<Chunk, MeshData>(vChunk, vData));
    }

    // Lógica de aplicación original íntegra (Sin cambios)
    public void Apply(Chunk pChunk, MeshData pData)
    {
        if (pChunk.mViewGO == null) return;

        Mesh vMesh = new Mesh();
        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        vMesh.SetVertices(pData.vertices);
        vMesh.SetNormals(pData.normals);
        vMesh.SetTriangles(pData.triangles, 0);
        vMesh.RecalculateBounds();

        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();
        if (vMf.sharedMesh != null) GameObject.Destroy(vMf.sharedMesh);
        vMf.sharedMesh = vMesh;

        int index = pChunk.mIndex;
        int lodApplied = Grid.ResolutionToLodIndex(pChunk.mSize);
        mGrid.SetLod(index, lodApplied);
        mGrid.SetProcessing(index, false);

        #if UNITY_EDITOR
        if (lodApplied > 0)
            UnityEngine.Debug.Log($"[LOD] Apply: chunk {pChunk.mCoord} mSize={pChunk.mSize} LOD={lodApplied} verts={pData.vertices.Count}");
        #endif
    }
}