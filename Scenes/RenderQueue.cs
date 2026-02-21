using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;



public class RenderQueue
{
    private Grid mGrid;
    public ConcurrentQueue<RenderJob> mQueue = new ConcurrentQueue<RenderJob>();
    public ConcurrentDictionary<Chunk, byte> mInWait = new ConcurrentDictionary<Chunk, byte>();
    public ConcurrentQueue<KeyValuePair<Chunk, MeshData>> mResultsLOD = new ConcurrentQueue<KeyValuePair<Chunk, MeshData>>();

    // Constructor original: La clase nace con su contexto
    public RenderQueue(Grid pGrid)
    {
        mGrid = pGrid;
    }

    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;
        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new RenderJob(pChunk, pGenerator));
        }
    }

    /// <summary>
    /// Procesa la generación de mallas. 
    /// nThreads = -1 (Todo), 0 (Todo menos uno), N (Hilos exactos).
    /// </summary>
    public void ProcessParallel(int nThreads = -1)
    {
        int hilosLogicos = Environment.ProcessorCount;
        int hilosAUsar;

        // Lógica de graduación de hilos (Sustituye a la versión monohilo)
        if (nThreads == -1) hilosAUsar = hilosLogicos;
        else if (nThreads == 0) hilosAUsar = Math.Max(1, hilosLogicos - 1);
        else if (nThreads == 1) hilosAUsar = 1; // Modo monohilo explícito
        else hilosAUsar = Math.Min(nThreads, hilosLogicos);

        ParallelOptions opciones = new ParallelOptions { MaxDegreeOfParallelism = hilosAUsar };

        Parallel.ForEach(mQueue, opciones, delegate (RenderJob vRequest)
        {
            Chunk vChunk = vRequest.mChunk;

            //// 1. Gestión de LOD
            //if (vChunk.mTargetSize > 0)
            //{
            //    vChunk.Redim(vChunk.mTargetSize);
            //}

            // 2. Generación de malla (Lógica acoplada y rápida)
            MeshData vData = vRequest.mMeshGenerator.Generate(
                vChunk,
                mGrid.mChunks,
                mGrid.mSizeInChunks
            );

            //vChunk.mTargetSize = 0;

            // 3. Encolado de resultados
            if (vData != null)
            {
                mResultsLOD.Enqueue(new KeyValuePair<Chunk, MeshData>(vChunk, vData));
            }
        });

        // Limpieza de cola de entrada
        RenderJob vTrash;
        while (mQueue.TryDequeue(out vTrash)) { }
        mInWait.Clear();
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