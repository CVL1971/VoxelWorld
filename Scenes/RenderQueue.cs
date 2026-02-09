using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

// Estructura explícita para las peticiones
public struct RenderRequest
{
    public Chunk chunk;
    public MeshGenerator generator;

    public RenderRequest(Chunk pChunk, MeshGenerator pGenerator)
    {
        this.chunk = pChunk;
        this.generator = pGenerator;
    }
}

public class RenderQueue
{
    private Grid mGrid;

    // Cola segura para hilos
    public ConcurrentQueue<RenderRequest> mQueue = new ConcurrentQueue<RenderRequest>();

    // Diccionario concurrente (hace de HashSet thread-safe)
    public ConcurrentDictionary<Chunk, byte> mInWait = new ConcurrentDictionary<Chunk, byte>();

    public RenderQueue(Grid pGrid)
    {
        mGrid = pGrid;
    }

    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;

        // TryAdd garantiza que solo se encole una vez de forma atómica
        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new RenderRequest(pChunk, pGenerator));
        }
    }

    public bool TryDequeue(out RenderRequest result)
    {
        if (mQueue.TryDequeue(out RenderRequest vRequest))
        {
            mInWait.TryRemove(vRequest.chunk, out _);
            result = vRequest;
            return true;
        }

        result = default(RenderRequest);
        return false;
    }

    // Este método SIEMPRE debe ejecutarse en el Main Thread
    public void Apply(Chunk pChunk, Mesh pMesh)
    {
        if (pChunk == null || pChunk.mViewGO == null) return;

        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();

        // 1. Limpieza de memoria: Destruimos la malla vieja de la GPU
        if (vMf.sharedMesh != null)
        {
            GameObject.Destroy(vMf.sharedMesh);
        }

        // 2. Asignación de la nueva malla
        vMf.sharedMesh = pMesh;

        // 3. Actualización de colisiones
        MeshCollider vMc = pChunk.mViewGO.GetComponent<MeshCollider>();
        if (vMc != null)
        {
            vMc.sharedMesh = pMesh;
        }
    }
}