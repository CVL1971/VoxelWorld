using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public struct DensitySamplerJob
{
    public Chunk mChunk;
  
    public DensitySamplerJob(Chunk pChunk)
    {
        mChunk = pChunk;
    }
}

public class DensitySamplerQueueAsync

{
  
    public ConcurrentQueue<DensitySamplerJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<Chunk> DensitySamplerResult = new();

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
            DensitySamplerResult.Enqueue(vChunk);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("Fallo en el Muestreador (Thread): " + ex.Message);
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