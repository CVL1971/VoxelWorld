using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RenderQueueAsync
{
    private readonly Grid mGrid;

    public ConcurrentQueue<RenderJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<KeyValuePair<Chunk, MeshData>> mResultsLOD = new();

    // ---- CONFIGURACIÓN ----
    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;

    public RenderQueueAsync(Grid pGrid, int maxParallel = 5)
    {
        mGrid = pGrid;
        mSlots = new SemaphoreSlim(maxParallel);
    }

    // ---- ENQUEUE ----
    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;

        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new RenderJob(pChunk, pGenerator));
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

    private async Task ProcessLoop()
    {
        while (true)
        {
            if (!mQueue.TryDequeue(out var job))
            {
                mWorkerRunning = 0;

                // si alguien encoló mientras salíamos
                if (!mQueue.IsEmpty && Interlocked.CompareExchange(ref mWorkerRunning, 1, 0) == 0)
                    continue;

                return;
            }

            await mSlots.WaitAsync();

            _ = Task.Run(() =>
            {
                try { Execute(job); }
                finally
                {
                    mInWait.TryRemove(job.mChunk, out _);
                    mSlots.Release();
                }
            });
        }
    }

    // ---- EJECUCIÓN REAL ----
    private void Execute(RenderJob vRequest)
    {
        Chunk vChunk = vRequest.mChunk;

        // LOD
        if (vChunk.mTargetSize > 0)
            vChunk.Redim(vChunk.mTargetSize);

        // Generar malla
        MeshData vData = vRequest.mMeshGenerator.Generate(
            vChunk,
            mGrid.mChunks,
            mGrid.mSizeInChunks
        );

        vChunk.mTargetSize = 0;

        if (vData != null)
            mResultsLOD.Enqueue(new KeyValuePair<Chunk, MeshData>(vChunk, vData));
    }

    // Lógica de aplicación original íntegra
    public void Apply(Chunk pChunk, MeshData pData)
    {
        if (pChunk.mViewGO == null) return;

        // 1. GENERACIÓN DE MALLA (Sin cambios)
        Mesh vMesh = new Mesh();
        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        vMesh.SetVertices(pData.vertices);
        vMesh.SetNormals(pData.normals);
        vMesh.SetTriangles(pData.triangles, 0);
        vMesh.RecalculateBounds();

        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();
        if (vMf.sharedMesh != null) GameObject.Destroy(vMf.sharedMesh);
        vMf.sharedMesh = vMesh;

        MeshCollider vMc = pChunk.mViewGO.GetComponent<MeshCollider>();
        if (vMc != null) vMc.sharedMesh = vMesh;

        // 2. APLICACIÓN DE COLOR ESTABLE
        MeshRenderer vMr = pChunk.mViewGO.GetComponent<MeshRenderer>();

        //int index = pChunk.mIndex;

        //// Leemos el Target que guardó el Vigilante
        //int finalLod = mGrid.GetLodTarget(index);

        //// Actualizamos el estado oficial del Chunk
        //mGrid.SetLod(index, finalLod);

        //// IMPORTANTE: Liberamos el bit de procesamiento. 
        //// Ahora el Vigilante puede volver a evaluar este chunk.
        //mGrid.SetProcessing(index, false);

        if (vMr != null)
        {
            MaterialPropertyBlock vPropBlock = new MaterialPropertyBlock();

            vMr.GetPropertyBlock(vPropBlock);

            // --- CORRECCIÓN DE Z-FIGHTING VISUAL ---

            // Calculamos el factor de altura con un pequeño margen para evitar valores extremos (0 o 1)
            float worldHeight = Mathf.Max(1, mGrid.mSizeInChunks.y);
            float heightFactor = (float)pChunk.mCoord.y / worldHeight;
            heightFactor = Mathf.Clamp(heightFactor, 0.01f, 0.99f);

            // Usamos un ruido basado en una función de onda suave (Seno/Coseno) 
            // En lugar de Repeat/Multiplicación, esto crea transiciones coherentes entre vecinos
            float seed = (pChunk.mCoord.x * 0.13f) + (pChunk.mCoord.z * 0.17f);
            float waveNoise = (Mathf.Sin(seed) + 1f) * 0.5f; // Normalizado 0 a 1

            // Definimos los colores base
            Color colorBajo = new Color(0.1f, 0.4f, 0.1f);
            Color colorAlto = new Color(0.8f, 0.8f, 0.9f);

            // MEZCLA FINAL: Aplicamos el ruido al factor de mezcla, NO al color final.
            // Esto hace que el borde sea una transición de color y no un cambio de brillo.
            float finalMix = Mathf.Lerp(heightFactor, waveNoise, 0.05f);
            Color finalColor = Color.Lerp(colorBajo, colorAlto, finalMix);

            vPropBlock.SetColor("_BaseColor", finalColor);
            vPropBlock.SetColor("_Color", finalColor);
            vMr.SetPropertyBlock(vPropBlock);
        }
    }


}