using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RenderStackAsync
{
    private readonly Grid mGrid;

    // Cambiado de ConcurrentQueue a ConcurrentStack para prioridad LIFO
    public ConcurrentStack<RenderJob> mStack = new();
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

    // ---- ENQUEUE (Ahora PUSH) ----
    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;

        if (mInWait.TryAdd(pChunk, 0))
        {
            // Usamos Push para que sea el primero en salir
            mStack.Push(new RenderJob(pChunk, pGenerator));
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
            // Cambiado TryDequeue por TryPop para obtener el último elemento añadido
            if (!mStack.TryPop(out var job))
            {
                mWorkerRunning = 0;

                // si alguien encoló mientras salíamos
                if (!mStack.IsEmpty && Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) == 0)
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

        MeshRenderer vMr = pChunk.mViewGO.GetComponent<MeshRenderer>();
        int index = pChunk.mIndex;
        int targetLod = (mGrid.mStatusGrid[index] & Grid.MASK_LOD_TARGET) >> 4;
        mGrid.SetLod(index, targetLod);
        mGrid.SetProcessing(index, false);
    }
}