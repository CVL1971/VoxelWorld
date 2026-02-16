//using System;
//using System.Collections.Concurrent;
//using System.Threading.Tasks;
//using UnityEngine;

//public static class SampleQueue
//{
//    // Cola de entrada concurrente: no se pierden solicitudes
//    private static ConcurrentQueue<DensityJob> mPendingJobs = new ConcurrentQueue<DensityJob>();

//    // Diccionario para evitar duplicados (si un chunk ya está en cola, no lo añadimos)
//    private static ConcurrentDictionary<Chunk, byte> mInWait = new ConcurrentDictionary<Chunk, byte>();

//    public static void Enqueue(Chunk pChunk, int pTargetRes)
//    {
//        if (pChunk == null) return;
//        if (mInWait.TryAdd(pChunk, 0))
//        {
//            mPendingJobs.Enqueue(new DensityJob(pChunk, pTargetRes));
//        }
//    }

//    /// <summary>
//    /// Procesa las densidades pendientes. 
//    /// nThreads define el presupuesto de núcleos para esta tarea específica.
//    /// </summary>
//    public static void ProcessParallel(int nThreads = 1)
//    {
//        if (mPendingJobs.IsEmpty) return;

//        ParallelOptions options = new ParallelOptions
//        {
//            MaxDegreeOfParallelism = nThreads
//        };

//        // Procesamos la cola actual
//        Parallel.ForEach(mPendingJobs, options, job =>
//        {
//            Chunk vChunk = job.mChunk;

//            // 1. Redimensionar el array si es necesario
//            vChunk.Redim(job.mTargetRes);

//            // 2. Ejecutar el muestreo pesado (Atomizado)
//            InternalSample(vChunk);

//            // 3. Liberar flags
//            vChunk.mAwaitingResample = false;
//            vChunk.mTargetSize = 0;

//            // Aquí podrías incluso auto-encolarlo en la RenderQueueMulti si quieres flujo total
//            // World.Instance.mRenderQueueMulti.Enqueue(vChunk, World.Instance.mSurfaceNetQEF);
//        });

//        // Limpieza de estructuras de control
//        DensityJob trash;
//        while (mPendingJobs.TryDequeue(out trash)) { }
//        mInWait.Clear();
//    }

//    private static void InternalSample(Chunk pChunk)
//    {
//        int res = pChunk.mCurrentRes;
//        Vector3Int origin = pChunk.mWorldOrigin;
//        float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / (float)res;

//        // Bucle interno optimizado
//        for (int z = 0; z <= res; z++)
//        {
//            for (int y = 0; y <= res; y++)
//            {
//                for (int x = 0; x <= res; x++)
//                {
//                    float worldX = origin.x + (x * vStep);
//                    float worldY = origin.y + (y * hFactor * vStep); // Ajuste si usas escalas
//                    float worldZ = origin.z + (z * vStep);

//                    float density = GetRawDistance(new Vector3(worldX, worldY, worldZ));

//                    // Escritura directa y segura por índice lineal
//                    int idx = x + y * (res + 1) + z * (res + 1) * (res + 1);
//                    if (idx < pChunk.mVoxels.Length)
//                        pChunk.mVoxels[idx] = density;
//                }
//            }
//        }
//    }

//    // ... (GetRawDistance y funciones matemáticas se mantienen igual)
//}