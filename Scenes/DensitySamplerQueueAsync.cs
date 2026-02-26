using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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

public class DensitySamplerQueueAsync

{
  
    public ConcurrentQueue<DensitySamplerJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<(Chunk chunk, int generationId)> DensitySamplerResult = new();

    // ---- CONFIGURACIÓN ----
    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    public DensitySamplerQueueAsync(int maxParallel = 10)
    {
        mSlots = new SemaphoreSlim(maxParallel);
    }

    // ---- ENQUEUE ----
    public void Enqueue(Chunk pChunk)
    {
        if (pChunk == null) return;

        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new DensitySamplerJob(pChunk));
            StartWorker();
        }
    }

    /// <summary>
    /// Fuerza re-encolado aunque el chunk esté en mInWait (p. ej. tras ReassignChunk por streaming).
    /// Evita la franja sin geometría cuando se recicla una capa antes de que termine el sampleo anterior.
    /// </summary>
    public void ForceEnqueue(Chunk pChunk)
    {
        if (pChunk == null) return;

        mInWait.TryRemove(pChunk, out _);
        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new DensitySamplerJob(pChunk));
        }
        StartWorker();
    }

    // ---- WORKER LOOP ----
    private void StartWorker()
    {
        if (Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) != 0)
            return;

        Task.Run(ProcessLoop);
    }

    // Un método con nombre, con su firma clara
    private void ThreadEntry(object pState)
    {
        // 1. Control de entrada: Si el sistema nos manda basura, ignoramos.
        if (pState == null) return;

        // Declaramos fuera lo que necesitamos usar en el finally
        DensitySamplerJob vJob = default;
        bool vLoaded = false;

        try
        {
            // 2. EL CASTING: Dentro del try. 
            // Si el "desempaquetado" falla, el catch lo captura y el finally libera el slot.
            vJob = (DensitySamplerJob)pState;
            vLoaded = true;

            // 3. EJECUCIÓN: Ya tenemos el tipo real, procedemos.
            Chunk vChunk = vJob.mChunk;
            SDFGenerator.Sample(vChunk);
            // No encolar si el chunk fue reciclado durante el sampleo
            if (vChunk.mGenerationId == vJob.mGenerationIdAtEnqueue)
                DensitySamplerResult.Enqueue((vChunk, vJob.mGenerationIdAtEnqueue));
           
        }
        catch (Exception ex)
        {
            //UnityEngine.Debug.LogError("Fallo en el Muestreador (Thread): " + ex.Message);
        }
        finally
        {
            // 4. LIMPIEZA: Solo si logramos cargar el Job con éxito.
            if (vLoaded && vJob.mChunk != null)
            {
                byte vRemovedValue;
                mInWait.TryRemove(vJob.mChunk, out vRemovedValue);
            }

            // 5. EL SEMÁFORO: Es la única forma de que el Vigilante siga vivo.
            // Pase lo que pase arriba, devolvemos el permiso.
            mSlots.Release();
        }
    }

    public static string DebugState(Chunk chunk)
    {
        if (chunk == null)
            return "[ChunkDebug] NULL chunk";

        if (chunk.mGrid == null)
            return "[ChunkDebug] Grid NULL";

        ushort status = chunk.mGrid.mStatusGrid[chunk.mIndex];

        bool surface = (status & Grid.BIT_SURFACE) != 0;
        bool processing = (status & Grid.MASK_PROCESSING) != 0;
        int lodCurrent = (status & Grid.MASK_LOD_CURRENT) >> 2;
        int lodTarget = (status & Grid.MASK_LOD_TARGET) >> 4;

        return
            $"[ChunkDebug] " +
            $"Slot={chunk.mCoord} | " +
            $"Global={chunk.mGlobalCoord} | " +
            $"Index={chunk.mIndex} | " +
            $"GenId={chunk.mGenerationId} | " +
            $"Size={chunk.mSize} | " +
            $"Edited={chunk.mIsEdited} | " +
            $"Bool1={chunk.mBool1} | " +
            $"Bool2={chunk.mBool2} | " +
            $"Surface={surface} | " +
            $"Processing={processing} | " +
            $"LOD_Current={lodCurrent} | " +
            $"LOD_Target={lodTarget} | " +
            $"StatusRaw=0x{status:X4} | " +
            $"WorldOrigin={chunk.WorldOrigin}";
    }

    private async Task ProcessLoop()
    {
        while (true)
        {
          
            if (!mQueue.TryDequeue(out DensitySamplerJob vJob)) break;

            // Espera síncrona o asíncrona, pero clara
            await mSlots.WaitAsync();

            // En lugar de una lambda oscura, pasamos el método y el estado
            // Esto es mucho más parecido al C# original
            Task vTask = new Task(ThreadEntry, vJob);
            vTask.Start();
        }

        Interlocked.Exchange(ref mWorkerRunning, 0);
    }

}