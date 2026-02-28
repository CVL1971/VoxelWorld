using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

public struct DensitySamplerJob
{
    public Chunk mChunk;

    public DensitySamplerJob(Chunk pChunk)
    {
        mChunk = pChunk;
    }
}

internal class DensitySamplerQueueAsync
{
    public ConcurrentQueue<DensitySamplerJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<Chunk> DensitySamplerResult = new();

    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    public DensitySamplerQueueAsync(int maxParallel = 10)
    {
        mSlots = new SemaphoreSlim(maxParallel);
    }

    public void Enqueue(Chunk pChunk)
    {
        if (pChunk == null) return;
        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new DensitySamplerJob(pChunk));
            StartWorker();
        }
    }

    private void StartWorker()
    {
        if (Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) != 0) return;
        _ = Task.Run(ProcessLoop);
    }

    private void ExecuteJob(DensitySamplerJob vJob)
    {
        
        try
        {
            Chunk vChunk = vJob.mChunk;
            if (vChunk == null) return;

            SDFGenerator.Sample(vChunk);
            DensitySamplerResult.Enqueue(vChunk);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if (vJob.mChunk != null) mInWait.TryRemove(vJob.mChunk, out _);
            mSlots.Release();
        }
    }

    private async Task ProcessLoop()
    {
        while (mQueue.TryDequeue(out DensitySamplerJob vJob))
        {
            await mSlots.WaitAsync();
            _ = Task.Run(() => ExecuteJob(vJob));
        }
        Interlocked.Exchange(ref mWorkerRunning, 0);
    }
}