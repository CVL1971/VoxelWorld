using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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
    public ConcurrentQueue<RenderRequest> mQueue = new ConcurrentQueue<RenderRequest>();
    public ConcurrentDictionary<Chunk, byte> mInWait = new ConcurrentDictionary<Chunk, byte>();
    public ConcurrentQueue<KeyValuePair<Chunk, MeshData>> mResults = new ConcurrentQueue<KeyValuePair<Chunk, MeshData>>();

    public void Setup(Grid pGrid) { mGrid = pGrid; }

    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;
        if (mInWait.TryAdd(pChunk, 0))
        {
            mQueue.Enqueue(new RenderRequest(pChunk, pGenerator));
        }
    }

    public void ProcessParallel()
    {
        // Usamos el delegado explícito para evitar azúcar sintáctico
        Parallel.ForEach(mQueue, delegate (RenderRequest vRequest)
        {
            Chunk vChunk = vRequest.chunk;

            // --- GESTIÓN DE RESOLUCIÓN (LOD) ---
            // Si hay una orden pendiente (marcada por el Vigía o Decimator)
            if (vChunk.mTargetSize > 0)
            {
                // Ejecutamos la redimensión usando tu función específica
                vChunk.Redim(vChunk.mTargetSize);

                // Consumimos la orden
                vChunk.mTargetSize = 0;

                // Por ahora, el ResampleData se queda pendiente para la fase 2
                SDFGenerator.ResampleData(vChunk); 
            }

            // --- GENERACIÓN DE MALLA (Tu lógica original) ---
            MeshData vData = vRequest.generator.Generate(
                vChunk,
                mGrid.mChunks,
                mGrid.mSizeInChunks
            );

            KeyValuePair<Chunk, MeshData> vResultado = new KeyValuePair<Chunk, MeshData>(vChunk, vData);
            mResults.Enqueue(vResultado);
        });

        // Limpieza manual de la cola
        RenderRequest vTrash;
        while (mQueue.TryDequeue(out vTrash)) { }
        mInWait.Clear();
    }

    // Se ejecuta en el Main Thread
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

        // 2. APLICACIÓN DE COLOR ALEATORIO POR CHUNK
        MeshRenderer vMr = pChunk.mViewGO.GetComponent<MeshRenderer>();
        if (vMr != null)
        {
            MaterialPropertyBlock vPropBlock = new MaterialPropertyBlock();
            vMr.GetPropertyBlock(vPropBlock);

            // 1. Normalizamos la altura (Y) entre 0 y 1 basándonos en el tamaño del mundo
            // Si mSizeInChunks.y es 2, el factor irá de 0 a 0.5 o 1.0
            float heightFactor = (float)pChunk.mCoord.y / Mathf.Max(1, mGrid.mSizeInChunks.y);

            // 2. Definimos dos colores para la transición (ejemplo: de Valle a Cima)
            Color colorBajo = new Color(0.1f, 0.4f, 0.1f); // Verde oscuro/Bosque
            Color colorAlto = new Color(0.8f, 0.8f, 0.9f); // Gris nieve/Cielo

            // 3. Interpolación lineal (Lerp)
            Color finalColor = Color.Lerp(colorBajo, colorAlto, heightFactor);

            // 4. Añadimos un toque de variación aleatoria sutil para que los chunks 
            // vecinos no sean exactamente iguales (opcional)
            float noise = (float)pChunk.mCoord.x * 0.1f + (float)pChunk.mCoord.z * 0.1f;
            finalColor *= (0.9f + Mathf.Repeat(noise, 0.2f));

            vPropBlock.SetColor("_BaseColor", finalColor);
            vPropBlock.SetColor("_Color", finalColor);
            vMr.SetPropertyBlock(vPropBlock);
        }
    }
}