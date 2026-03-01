using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;



public struct DensitySamplerResultItem
{
    public Chunk mChunk;
    public bool mStructural;


public DensitySamplerResultItem(Chunk chunk, bool structural)
    {
        mChunk = chunk;
        mStructural = structural;
    }


}

internal class DensitySamplerQueueEpoch
{
    public ConcurrentQueue<DensitySamplerJob> mQueue = new();
    public ConcurrentQueue<DensitySamplerJob> mStructuralQueue = new();
    public ConcurrentQueue<DensitySamplerResultItem> DensitySamplerResult = new();

    private readonly ConcurrentDictionary<Chunk, byte> mTranslationLocks = new();

    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    private bool mStructuralActive = false;
    private DensitySamplerJob mCurrentStructural;

    public DensitySamplerQueueEpoch(int maxParallel = 10)
    {
        mSlots = new SemaphoreSlim(maxParallel);
    }

    // =========================================================
    // ENQUEUE NORMAL
    // =========================================================
    public void Enqueue(Chunk pChunk)
    {
        if (pChunk == null)
            return;

        if (mTranslationLocks.ContainsKey(pChunk))
            return;

        pChunk.AddPending();

        mQueue.Enqueue(new DensitySamplerJob(pChunk));
        StartWorker();
    }

    // =========================================================
    // ENQUEUE STRUCTURAL
    // =========================================================
    public void EnqueueStructural(Chunk pChunk)
    {
        if (pChunk == null)
            return;

        mStructuralQueue.Enqueue(new DensitySamplerJob(pChunk));

        TryActivateStructural();
    }

    // =========================================================
    // ACTIVAR STRUCTURAL
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
            mQueue.Enqueue(job);
            StartWorker();
        }
        else
        {
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

        _ = Task.Run(ProcessLoop);
    }

    private async Task ProcessLoop()
    {
        while (true)
        {
            if (!mQueue.TryDequeue(out DensitySamplerJob job))
            {
                Interlocked.Exchange(ref mWorkerRunning, 0);

                if (!mQueue.IsEmpty &&
                    Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) == 0)
                    continue;

                return;
            }

            await mSlots.WaitAsync();

            _ = Task.Run(() => ExecuteJob(job));
        }
    }

    // =========================================================
    // EJECUCIÓN
    // =========================================================
    private void ExecuteJob(DensitySamplerJob job)
    {
        try
        {
            Chunk chunk = job.mChunk;
            if (chunk == null) return;

            bool structural = (job.mChunk == mCurrentStructural.mChunk && mStructuralActive);

            if (structural)
            {
                int index = chunk.mIndex;

                chunk.mGrid.mStatusGrid[index] = 0;
                chunk.mGrid.Surface(index, false);
                chunk.mGrid.SetProcessing(index, false);

                chunk.mIsEdited = false;
                chunk.ResetGenericBools();

                // emitir resultado estructural
                DensitySamplerResult.Enqueue(
                    new DensitySamplerResultItem(chunk, true)
                );

                mTranslationLocks.TryRemove(chunk, out _);

                mStructuralActive = false;

                // pasar testigo
                TryActivateStructural();

                return;
            }

            // trabajo normal
            SDFGenerator.Sample(chunk);

            DensitySamplerResult.Enqueue(
                new DensitySamplerResultItem(chunk, false)
            );
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            if (!mStructuralActive && job.mChunk != null)
                job.mChunk.ReleasePending();

            mSlots.Release();
        }
    }


}
