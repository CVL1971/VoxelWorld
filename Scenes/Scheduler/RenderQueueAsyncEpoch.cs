using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RenderQueueAsyncEpoch
{
    private readonly Grid mGrid;


    public ConcurrentQueue<RenderJobPlus> mQueue = new();
    public ConcurrentQueue<RenderJobPlus> mStructuralQueue = new();
    public ConcurrentQueue<(Chunk chunk, MeshData mesh)> mResultsLOD = new();

    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    private bool mStructuralActive = false;
    private RenderJobPlus mCurrentStructural;

    private readonly ConcurrentDictionary<Chunk, byte> mTranslationLocks = new();

    public RenderQueueAsyncEpoch(Grid pGrid, int maxParallel = 8)
    {
        mGrid = pGrid;
        mSlots = new SemaphoreSlim(maxParallel);
    }

    // =========================================================
    // ENQUEUE NORMAL
    // =========================================================
    public void Enqueue(Chunk chunk, MeshGenerator generator)
    {
        if (chunk == null || generator == null)
            return;

        if (mTranslationLocks.ContainsKey(chunk))
            return;

        chunk.AddPending();

        mQueue.Enqueue(new RenderJobPlus(chunk, generator, false));
        StartWorker();
    }

    // =========================================================
    // ENQUEUE STRUCTURAL
    // =========================================================
    public void Enqueue(Chunk chunk, MeshGenerator generator, bool vReset)
    {
        if (chunk == null) return;
        if (!vReset) { Enqueue(chunk, generator); return; }

        mStructuralQueue.Enqueue(new RenderJobPlus(chunk, generator, true));

        TryActivateStructural();
    }

    // =========================================================
    // ACTIVAR STRUCTURAL (mecha)
    // =========================================================
    private void TryActivateStructural()
    {
        if (mStructuralActive)
            return;

        if (!mStructuralQueue.TryDequeue(out var job))
            return;

        mStructuralActive = true;
        mCurrentStructural = job;

        Chunk chunk = job.mChunk;

        mTranslationLocks.TryAdd(chunk, 0);

        if (chunk.mPending == 0)
        {
            // ejecución inmediata
            mQueue.Enqueue(job);
            StartWorker();
        }
        else
        {
            // esperar evento
            chunk.OnIdle += OnChunkIdle;
        }
    }

    // =========================================================
    // EVENTO IDLE
    // =========================================================
    private void OnChunkIdle(Chunk chunk)
    {
        chunk.OnIdle -= OnChunkIdle;

        mQueue.Enqueue(mCurrentStructural);
        StartWorker();
    }

    // =========================================================
    // WORKER
    // =========================================================
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

                if (!mQueue.IsEmpty &&
                    Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) == 0)
                    continue;

                return;
            }

            await mSlots.WaitAsync();

            _ = Task.Run(() =>
            {
                try { Execute(job); }
                finally { mSlots.Release(); }
            });
        }
    }

    // =========================================================
    // EJECUCIÓN
    // =========================================================
    private void Execute(RenderJobPlus job)
    {
        Chunk chunk = job.mChunk;

        if (job.mStructural)
        {
            ResetChunkState(chunk);

            mTranslationLocks.TryRemove(chunk, out _);

            mStructuralActive = false;

            // paso de testigo (propaga la mecha)
            TryActivateStructural();

            return;
        }

        MeshData data = job.mMeshGenerator.Generate(
            chunk,
            mGrid.mChunks,
            mGrid.mSizeInChunks
        );

        if (data != null)
            mResultsLOD.Enqueue((chunk, data));

        chunk.ReleasePending();
    }

    // =========================================================
    // RESET
    // =========================================================
    private void ResetChunkState(Chunk chunk)
    {
        int index = chunk.mIndex;

        mGrid.mStatusGrid[index] = 0;
        mGrid.Surface(index, false);
        mGrid.SetProcessing(index, false);

        chunk.mIsEdited = false;
        chunk.ResetGenericBools();
    }

    // =========================================================
    // APPLY (MAIN THREAD) - aplica malla al chunk
    // =========================================================
    public void Apply(Chunk pChunk, MeshData pData)
    {
        if (pChunk?.mViewGO == null) return;

        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();
        Mesh vMesh = vMf.sharedMesh;
        if (vMesh == null)
        {
            vMesh = new Mesh();
            vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        else
        {
            vMesh.Clear();
        }
        vMesh.SetVertices(pData.vertices);
        vMesh.SetNormals(pData.normals);
        vMesh.SetTriangles(pData.triangles, 0);
        vMesh.RecalculateBounds();
        vMf.sharedMesh = vMesh;
        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        pChunk.mViewGO.GetComponent<MeshRenderer>().enabled = true;

        int index = pChunk.mIndex;
        int lodApplied = Grid.ResolutionToLodIndex(pChunk.mSize);
        mGrid.SetLod(index, lodApplied);
        mGrid.SetProcessing(index, false);
    }
}
