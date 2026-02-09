using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

/// <summary>
/// Multithreaded chunk generation queue.
/// Workers generate meshes off the main thread.
/// Main thread only applies meshes to Unity objects.
/// </summary>
public class ChunkGenerationQueue : MonoBehaviour
{
    public static ChunkGenerationQueue Instance { get; private set; }

    private readonly ConcurrentQueue<Chunk> pending = new();
    private readonly ConcurrentQueue<Result> finished = new();

    private struct Result
    {
        public Chunk chunk;
        public Mesh mesh;
    }

    void Awake()
    {
        Instance = this;

        int workers = Mathf.Max(1, Environment.ProcessorCount - 1);
        Debug.Log($"[ChunkQueue] Starting {workers} worker threads");

        for (int i = 0; i < workers; i++)
        {
            Thread thread = new Thread(WorkerLoop);
            thread.IsBackground = true;
            thread.Start();
        }
    }

    // Called from game code when a chunk needs generation
    public void Request(Chunk chunk)
    {
        pending.Enqueue(chunk);
    }

    private void WorkerLoop()
    {
        var generator = new SurfaceNetsGenerator2();

        while (true)
        {
            if (!pending.TryDequeue(out Chunk chunk))
            {
                Thread.Sleep(1);
                continue;
            }

            try
            {
                Mesh mesh = generator.Generate(chunk, World.AllChunks, World.WorldSize);

                finished.Enqueue(new Result
                {
                    chunk = chunk,
                    mesh = mesh
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChunkQueue] Worker exception: {ex}");
            }
        }
    }

    // Main thread only: apply meshes to Unity objects
    void Update()
    {
        while (finished.TryDequeue(out Result result))
        {
            result.chunk.ApplyMesh(result.mesh);
        }
    }
}
