
//using System;
//using System.Collections.Concurrent;
//using System.Threading;
//using System.Threading.Tasks;
//using UnityEngine;

//public class RenderQueueAsyncEpoch2
//{
//    private readonly Grid mGrid;

//    public ConcurrentQueue<RenderJobPlus> mQueue = new();
//    public ConcurrentQueue<RenderJobPlus> mStructuralQueue = new();

//    // espera no estructural
//    public ConcurrentQueue<RenderJobPlus> mWaitQueue = new();
//    public ConcurrentQueue<(Chunk chunk, MeshData mesh)> mResultsLOD = new();

//    private readonly SemaphoreSlim mSlots;
//    private int mWorkerRunning = 0;

//    private bool mStructuralActive = false;
//    private RenderJobPlus mCurrentStructural;

//    // barreras generacionales
//    private readonly ConcurrentDictionary<Chunk, long> mBarriers = new();

//    public RenderQueueAsyncEpoch2(Grid pGrid, int maxParallel = 8)
//    {
//        mGrid = pGrid;
//        mSlots = new SemaphoreSlim(maxParallel);
//    }

//    // =========================================================
//    // ENQUEUE NORMAL
//    // =========================================================
//    public void Enqueue(Chunk chunk, MeshGenerator generator)
//    {
//        if (chunk == null || generator == null)
//            return;

//        long ts = Time.frameCount;

//        if (mBarriers.TryGetValue(chunk, out long barrier))
//        {
//            if (ts > barrier)
//            {
//                // generación nueva -> espera
//                SubscribeAndWait(chunk, generator, ts);
//                return;
//            }
//        }

//        if (chunk.mPending > 0)
//        {
//            SubscribeAndWait(chunk, generator, ts);
//            return;
//        }

//        chunk.AddPending();

//        mQueue.Enqueue(new RenderJobPlus(chunk, generator, false)
//        {
//            mTimestamp = ts
//        });

//        StartWorker();
//    }

//    // =========================================================
//    // ENQUEUE STRUCTURAL
//    // =========================================================
//    public void Enqueue(Chunk chunk, MeshGenerator generator, bool vReset)
//    {
//        if (!vReset)
//        {
//            Enqueue(chunk, generator);
//            return;
//        }

//        long t0 = Time.frameCount;

//        mBarriers[chunk] = t0;

//        mStructuralQueue.Enqueue(new RenderJobPlus(chunk, generator, true)
//        {
//            mTimestamp = t0
//        });

//        TryActivateStructural();
//    }

//    // =========================================================
//    // ESPERA NO ESTRUCTURAL
//    // =========================================================
//    private void SubscribeAndWait(Chunk chunk, MeshGenerator gen, long ts)
//    {
//        var job = new RenderJobPlus(chunk, gen, false)
//        {
//            mTimestamp = ts
//        };

//        mWaitQueue.Enqueue(job);

//        chunk.OnIdle += OnChunkIdle;
//    }

//    // =========================================================
//    // EVENTO IDLE
//    // =========================================================
//    private void OnChunkIdle(Chunk chunk)
//    {
//        chunk.OnIdle -= OnChunkIdle;

//        // regla del testigo
//        while (mWaitQueue.TryPeek(out var job))
//        {
//            if (job.mChunk != chunk)
//                break;

//            if (!mWaitQueue.TryDequeue(out job))
//                break;

//            if (job.mChunk.mPending > 0)
//                break;

//            job.mChunk.AddPending();
//            mQueue.Enqueue(job);
//        }

//        StartWorker();
//    }

//    // =========================================================
//    // ACTIVAR STRUCTURAL
//    // =========================================================
//    private void TryActivateStructural()
//    {
//        if (mStructuralActive)
//            return;

//        if (!mStructuralQueue.TryDequeue(out var job))
//            return;

//        mStructuralActive = true;
//        mCurrentStructural = job;

//        Chunk chunk = job.mChunk;

//        if (chunk.mPending == 0)
//        {
//            mQueue.Enqueue(job);
//            StartWorker();
//        }
//        else
//        {
//            chunk.OnIdle += OnStructuralIdle;
//        }
//    }

//    private void OnStructuralIdle(Chunk chunk)
//    {
//        chunk.OnIdle -= OnStructuralIdle;

//        mQueue.Enqueue(mCurrentStructural);
//        StartWorker();
//    }

//    // =========================================================
//    // WORKER
//    // =========================================================
//    private void StartWorker()
//    {
//        if (Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) != 0)
//            return;

//        Task.Run(ProcessLoop);
//    }

//    private async Task ProcessLoop()
//    {
//        while (true)
//        {
//            if (!mQueue.TryDequeue(out var job))
//            {
//                mWorkerRunning = 0;

//                if (!mQueue.IsEmpty &&
//                    Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) == 0)
//                    continue;

//                return;
//            }

//            await mSlots.WaitAsync();

//            _ = Task.Run(() =>
//            {
//                try { Execute(job); }
//                finally { mSlots.Release(); }
//            });
//        }
//    }

//    // =========================================================
//    // EJECUCIÓN
//    // =========================================================
//    private void Execute(RenderJobPlus job)
//    {
//        Chunk chunk = job.mChunk;

//        if (job.mStructural)
//        {
//            ResetChunkState(chunk);

//            mBarriers.TryRemove(chunk, out _);

//            mStructuralActive = false;

//            TryActivateStructural();
//            return;
//        }

//        MeshData data = job.mMeshGenerator.Generate(
//            chunk,
//            mGrid.mChunks,
//            mGrid.mSizeInChunks
//        );

//        if (data != null)
//            mResultsLOD.Enqueue((chunk, data));

//        chunk.ReleasePending();
//    }

//    // =========================================================
//    // RESET
//    // =========================================================
//    private void ResetChunkState(Chunk chunk)
//    {
//        int index = chunk.mIndex;

//        mGrid.mStatusGrid[index] = 0;
//        mGrid.Surface(index, false);
//        mGrid.SetProcessing(index, false);

//        chunk.mIsEdited = false;
//        chunk.ResetGenericBools();
//    }
//}
