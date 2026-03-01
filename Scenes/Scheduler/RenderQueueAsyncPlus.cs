using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public struct RenderJobPlus
{
    public Chunk mChunk;
    public MeshGenerator mMeshGenerator;
    public bool mStructural;

    public RenderJobPlus(Chunk c, MeshGenerator g, bool structural)
    {
        mChunk = c;
        mMeshGenerator = g;
        mStructural = structural;
    }
}

public class RenderQueueAsyncPlus
{
    private readonly Grid mGrid;

    public ConcurrentQueue<RenderJobPlus> mQueue = new();
    public ConcurrentQueue<(Chunk chunk, MeshData mesh)> mResultsLOD = new();

    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    public RenderQueueAsyncPlus(Grid pGrid, int maxParallel = 8)
    {
        mGrid = pGrid;
        mSlots = new SemaphoreSlim(maxParallel);
    }

    // =========================================================
    // ENQUEUE NORMAL (incrementa contador)
    // =========================================================
    public void Enqueue(Chunk chunk, MeshGenerator generator)
    {
        if (chunk == null || generator == null) return;

        Interlocked.Increment(ref chunk.mPending);

        mQueue.Enqueue(new RenderJobPlus(chunk, generator, false));
        StartWorker();
    }

    // =========================================================
    // ENQUEUE ESTRUCTURAL (NO incrementa)
    // =========================================================
    public void Enqueue(Chunk chunk, MeshGenerator generator, bool vReset)
    {
        if (chunk == null || generator == null) return;

        mQueue.Enqueue(new RenderJobPlus(chunk, generator, vReset));
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

        // --------- ESTRUCTURAL ----------
        if (job.mStructural)
        {
            // Solo entra si nadie está trabajando
            if (Interlocked.CompareExchange(ref chunk.mPending, 1, 0) != 0)
            {
                // No libre → reencolar sin tocar contador
                mQueue.Enqueue(job);
                return;
            }

            // Exclusividad real obtenida
            ResetChunkState(chunk);
        }
        // --------- NORMAL ----------
        // Ya incrementó al encolar

        MeshData data = job.mMeshGenerator.Generate(
            chunk,
            mGrid.mChunks,
            mGrid.mSizeInChunks
        );

        if (data != null)
            mResultsLOD.Enqueue((chunk, data));

        // Liberación universal
        Interlocked.Decrement(ref chunk.mPending);
    }

    // =========================================================
    // RESET ESTRUCTURAL
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
    // APPLY (MAIN THREAD)
    // =========================================================
    public void Apply(Chunk chunk, MeshData data)
    {
        if (chunk.mViewGO == null) return;

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(data.vertices);
        mesh.SetNormals(data.normals);
        mesh.SetTriangles(data.triangles, 0);
        mesh.RecalculateBounds();

        MeshFilter mf = chunk.mViewGO.GetComponent<MeshFilter>();
        if (mf.sharedMesh != null) GameObject.Destroy(mf.sharedMesh);
        mf.sharedMesh = mesh;

        int index = chunk.mIndex;
        int lodApplied = Grid.ResolutionToLodIndex(chunk.mSize);

        mGrid.SetLod(index, lodApplied);
        mGrid.SetProcessing(index, false);
    }
}