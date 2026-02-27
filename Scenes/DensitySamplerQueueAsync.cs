using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

public struct DensitySamplerJob
{
    public Chunk mChunk;
    public int mGenerationIdAtEnqueue;

    public DensitySamplerJob(Chunk pChunk)
    {
        mChunk = pChunk;
        mGenerationIdAtEnqueue = pChunk.mGenerationId;
    }
}

internal class DensitySamplerQueueAsync
{
    public ConcurrentQueue<DensitySamplerJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<(Chunk chunk, int generationId)> DensitySamplerResult = new();

    // Estos son tus "detectores" para el Profiler
    private static readonly CustomSampler s_TaskSampler = CustomSampler.Create("Voxel_Density_Task");
    private static readonly CustomSampler s_MathSampler = CustomSampler.Create("SDF_Math_Calculation");

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

    public void ForceEnqueue(Chunk pChunk)
    {
        if (pChunk == null) return;
        mInWait.TryRemove(pChunk, out _);
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
        // Iniciamos la caja en el Profiler
        s_TaskSampler.Begin();
        try
        {
            Chunk vChunk = vJob.mChunk;
            if (vChunk == null) return;

            s_MathSampler.Begin();
            SDFGenerator.Sample(vChunk);
            s_MathSampler.End();

            if (vChunk.mGenerationId == vJob.mGenerationIdAtEnqueue)
                DensitySamplerResult.Enqueue((vChunk, vJob.mGenerationIdAtEnqueue));
            else
                ForceEnqueue(vChunk);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if (vJob.mChunk != null) mInWait.TryRemove(vJob.mChunk, out _);
            mSlots.Release();
            s_TaskSampler.End(); // Cerramos la caja
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