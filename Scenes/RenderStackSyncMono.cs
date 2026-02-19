using System.Collections.Generic;
using UnityEngine;

public class RenderStackSyncMono
{
    private readonly Grid mGrid;
    // Lista simple en lugar de Stack concurrente
    private Queue<RenderJob> mQueue = new Queue<RenderJob>();

    public RenderStackSyncMono(Grid pGrid)
    {
        mGrid = pGrid;
    }

    public void Enqueue(Chunk pChunk, MeshGenerator pGenerator)
    {
        if (pChunk == null || pGenerator == null) return;

        // Añadimos a la cola para procesar en el siguiente frame
        mQueue.Enqueue(new RenderJob(pChunk, pGenerator));
    }

    // Llamar a esto desde el Update de tu WorldManager o ChunkManager
    public void UpdateSync()
    {
        // Procesamos uno o varios chunks por frame para no ahogar el Update
        // Si quieres procesar TODOS de golpe, cambia el 'if' por 'while'
        if (mQueue.Count > 0)
        {
            RenderJob job = mQueue.Dequeue();
            ExecuteSync(job);
        }
    }

    private void ExecuteSync(RenderJob vRequest)
    {
        Chunk vChunk = vRequest.mChunk;

        // Usamos la misma lógica de construcción de strings que te funciona
        System.Text.StringBuilder _dbgSync = new System.Text.StringBuilder();
        _dbgSync.AppendLine($"<color=yellow>[Sync Path]</color> Procesando {vChunk.mCoord}");

        try
        {
            if (vChunk.mTargetSize > 0)
                vChunk.Redim(vChunk.mTargetSize);

            // EJECUCIÓN SÍNCRONA: Aquí verás cualquier error real
            MeshData vData = vRequest.mMeshGenerator.Generate(
                vChunk,
                mGrid.mChunks,
                mGrid.mSizeInChunks
            );

            vChunk.mTargetSize = 0;

            if (vData != null)
            {
                _dbgSync.AppendLine($"Generados {vData.vertices.Count} vértices. Aplicando malla...");
                Apply(vChunk, vData); // Aplicamos directamente al ser monohilo
            }
            else
            {
                _dbgSync.AppendLine("MeshData resultó NULL.");
            }
        }
        catch (System.Exception e)
        {
            _dbgSync.AppendLine($"<color=red>FATAL ERROR:</color> {e.Message}");
            _dbgSync.AppendLine(e.StackTrace);
        }

        Debug.Log(_dbgSync.ToString());
    }

    public void Apply(Chunk pChunk, MeshData pData)
    {
        // Mantengo tu lógica original de RenderStackAsync.cs
        if (pChunk.mViewGO == null) return;

        Mesh vMesh = new Mesh();
        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        vMesh.SetVertices(pData.vertices);
        vMesh.SetNormals(pData.normals);
        vMesh.SetTriangles(pData.triangles, 0);
        vMesh.RecalculateBounds();

        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();
        if (vMf.sharedMesh != null) Object.Destroy(vMf.sharedMesh);
        vMf.sharedMesh = vMesh;

        int index = pChunk.mIndex;
        int targetLod = (mGrid.mStatusGrid[index] & Grid.MASK_LOD_TARGET) >> 4;
        mGrid.SetLod(index, targetLod);
        mGrid.SetProcessing(index, false);
    }
}