using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class RenderQueueMonohilo
{
    private Grid mGrid;
    public Queue<RenderRequest> mQueue = new Queue<RenderRequest>();
    public HashSet<Chunk> mInWait = new HashSet<Chunk>();
    public Queue<KeyValuePair<Chunk, MeshData>> mResults = new Queue<KeyValuePair<Chunk, MeshData>>();

    public void Setup(Grid pGrid) { mGrid = pGrid; }

    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;
        if (mInWait.Add(pChunk))
        {
            mQueue.Enqueue(new RenderRequest(pChunk, pGenerator));
        }
    }

    public void ProcessSequential()
    {
        // Procesamos la cola de forma secuencial (Monohilo) para permitir depuración
        foreach (RenderRequest vRequest in mQueue)
        {
            Chunk vChunk = vRequest.chunk;

            // --- GESTIÓN DE RESOLUCIÓN (LOD) ---
            if (vChunk.mTargetSize > 0)
            {
                // Ejecutamos la redimensión
                vChunk.Redim(vChunk.mTargetSize);

            }

            SDFGenerator.Sample(vChunk);

            // --- GENERACIÓN DE MALLA ---
            MeshData vData = vRequest.generator.Generate(
                vChunk,
                mGrid.mChunks,
                mGrid.mSizeInChunks
            );

            // Consumimos la orden
            //vChunk.mSize = vChunk.mTargetSize; 
            vChunk.mTargetSize = 0;
          
            KeyValuePair<Chunk, MeshData> vResultado = new KeyValuePair<Chunk, MeshData>(vChunk, vData);
            mResults.Enqueue(vResultado);
        }

        // Limpieza de la cola
        mQueue.Clear();
        mInWait.Clear();
    }

    // Se ejecuta en el Main Thread (Copia exacta de la lógica original)
    public void Apply(Chunk pChunk, MeshData pData)
    {
        if (pChunk.mViewGO == null) return;

        // 1. Creación de Malla
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

        // 2. APLICACIÓN DE COLOR POR CHUNK
        MeshRenderer vMr = pChunk.mViewGO.GetComponent<MeshRenderer>();
        if (vMr != null)
        {
            MaterialPropertyBlock vPropBlock = new MaterialPropertyBlock();
            vMr.GetPropertyBlock(vPropBlock);

            float heightFactor = (float)pChunk.mCoord.y / Mathf.Max(1, mGrid.mSizeInChunks.y);

            Color colorBajo = new Color(0.1f, 0.4f, 0.1f);
            Color colorAlto = new Color(0.8f, 0.8f, 0.9f);

            Color finalColor = Color.Lerp(colorBajo, colorAlto, heightFactor);

            float noise = (float)pChunk.mCoord.x * 0.1f + (float)pChunk.mCoord.z * 0.1f;
            finalColor *= (0.9f + Mathf.Repeat(noise, 0.2f));

            vPropBlock.SetColor("_BaseColor", finalColor);
            vPropBlock.SetColor("_Color", finalColor);
            vMr.SetPropertyBlock(vPropBlock);
        }
    }
}