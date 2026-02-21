using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public struct RenderJob
{
    public Chunk mChunk;
    public MeshGenerator mMeshGenerator;
    public RenderJob(Chunk pChunk, MeshGenerator pMeshGenerator)
    {
        mChunk = pChunk;
        mMeshGenerator = pMeshGenerator;
    }
}

public class RenderQueueAsync
{
    private readonly Grid mGrid;

    public ConcurrentQueue<RenderJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<KeyValuePair<Chunk, MeshData>> mResultsLOD = new();

    // ---- CONFIGURACIÓN ----
    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    public RenderQueueAsync(Grid pGrid, int maxParallel = 10)
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

    // ---- EJECUCIÓN REAL ----
    private void Execute(RenderJob vRequest)
    {
        Chunk vChunk = vRequest.mChunk;

        //// LOD
        //if (vChunk.mTargetSize > 0)
        //    vChunk.Redim(vChunk.mTargetSize);

        // Generar malla
        MeshData vData = vRequest.mMeshGenerator.Generate(
            vChunk,
            mGrid.mChunks,
            mGrid.mSizeInChunks
        );


        if (vData != null)
            mResultsLOD.Enqueue(new KeyValuePair<Chunk, MeshData>(vChunk, vData));
    }

    // Lógica de aplicación original íntegra
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

        MeshCollider vMc = pChunk.mViewGO.GetComponent<MeshCollider>();
        if (vMc != null) vMc.sharedMesh = vMesh;

        int index = pChunk.mIndex;
        int lodApplied = Grid.ResolutionToLodIndex(pChunk.mSize);
        mGrid.SetLod(index, lodApplied);
        mGrid.SetProcessing(index, false);
        
    }

}