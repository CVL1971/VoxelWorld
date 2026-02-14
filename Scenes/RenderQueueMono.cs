using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class RenderQueueMono
{
    private Grid mGrid;
    public Queue<RenderJob> mQueue = new Queue<RenderJob>();
    public HashSet<Chunk> mInWait = new HashSet<Chunk>();
    public Queue<KeyValuePair<Chunk, MeshData>> mResults = new Queue<KeyValuePair<Chunk, MeshData>>();
    /// <summary> Resultados de LOD (remesh por distancia). Se aplican antes que mResults para que la geometría por nivel se vea de inmediato. </summary>
  

    public RenderQueueMono(Grid pGrid) { mGrid = pGrid; }

    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;
        if (mInWait.Add(pChunk))
        {
            mQueue.Enqueue(new RenderJob(pChunk, pGenerator));
        }
    }

    // Se ejecuta en el Main Thread (Copia exacta de la l?gica original)
    public void Apply(Chunk pChunk, MeshData pData)
    {
        if (pChunk.mViewGO == null) return;

        // 1. CREACIÓN DE LA NUEVA MALLA
        Mesh vMesh = new Mesh();
        vMesh.name = "Chunk_Mesh_" + pChunk.mCoord.ToString(); // Ayuda a depurar
        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        vMesh.SetVertices(pData.vertices);
        vMesh.SetNormals(pData.normals);
        vMesh.SetTriangles(pData.triangles, 0);
        vMesh.RecalculateBounds();

        // 2. ACTUALIZACIÓN DEL FILTRO (Evitar competencia)
        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();
        if (vMf != null)
        {
            // Guardamos la malla vieja para destruirla después de liberar el slot
            Mesh oldMesh = vMf.sharedMesh;

            vMf.sharedMesh = null;   // Vaciamos el slot para que no haya dos mallas activas
            vMf.sharedMesh = vMesh;  // Asignamos la nueva

            if (oldMesh != null) GameObject.Destroy(oldMesh);
        }

        // 3. ACTUALIZACIÓN DEL COLLIDER (Sincronizado)
        MeshCollider vMc = pChunk.mViewGO.GetComponent<MeshCollider>();
        if (vMc != null)
        {
            vMc.sharedMesh = null;
            vMc.sharedMesh = vMesh;
        }

        // 4. COLOR COHERENTE (Sin ruidos que parpadeen)
        MeshRenderer vMr = pChunk.mViewGO.GetComponent<MeshRenderer>();
        if (vMr != null)
        {
            MaterialPropertyBlock vPropBlock = new MaterialPropertyBlock();
            vMr.GetPropertyBlock(vPropBlock);

            // Normalizamos la altura de forma suave
            float worldHeight = Mathf.Max(1, mGrid.mSizeInChunks.y);
            float hFactor = Mathf.Clamp01((float)pChunk.mCoord.y / worldHeight);

            Color colorBajo = new Color(0.1f, 0.4f, 0.1f);
            Color colorAlto = new Color(0.8f, 0.8f, 0.9f);

            // El ruido debe ser determinista basado en la posición, 
            // para que si el chunk se recarga, el color sea IDÉNTICO.
            float deterministicNoise = Mathf.Repeat(pChunk.mCoord.x * 0.123f + pChunk.mCoord.z * 0.456f, 0.1f);
            Color finalColor = Color.Lerp(colorBajo, colorAlto, hFactor);

            // Sumamos el ruido al color en lugar de multiplicar para evitar saltos de brillo
            finalColor += new Color(deterministicNoise, deterministicNoise, deterministicNoise) * 0.5f;

            vPropBlock.SetColor("_BaseColor", finalColor);
            vPropBlock.SetColor("_Color", finalColor);
            vMr.SetPropertyBlock(vPropBlock);
        }
    }


}