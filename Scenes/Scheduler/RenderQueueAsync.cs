using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public struct RenderJob
{
    public Chunk mChunk;
    public MeshGenerator mMeshGenerator;
    public RenderJob(Chunk pChunk, MeshGenerator pMeshGenerator)
    {
        mChunk = pChunk;
        mMeshGenerator = pMeshGenerator;
    }
}


internal class RenderQueueAsync
{
    private readonly Grid mGrid;

    public ConcurrentQueue<RenderJob> mQueue = new();
    public ConcurrentDictionary<Chunk, byte> mInWait = new();
    public ConcurrentQueue<(Chunk chunk, MeshData mesh)> mResultsLOD = new();

    // ---- CONFIGURACIùN ----
    private readonly SemaphoreSlim mSlots;
    private int mWorkerRunning = 0;
    public RenderQueueAsync(Grid pGrid, int maxParallel = 10)
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

                // si alguien encolù mientras salùamos
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

    // ---- EJECUCIOùN REAL ----
    private void Execute(RenderJob vRequest)
    {
        Chunk vChunk = vRequest.mChunk;

        //// LOD
        //if (vChunk.mTargetSize > 0)
        //    vChunk.Redim(vChunk.mTargetSize);

        MeshData vData = vRequest.mMeshGenerator.Generate(
            vChunk,
            mGrid.mChunks,
            mGrid.mSizeInChunks
        );

        if (vData != null)
            mResultsLOD.Enqueue((vChunk, vData));
    }

    public void Apply(Chunk pChunk, MeshData pData)
    {
        MeshFilter vMf = pChunk.mViewGO.GetComponent<MeshFilter>();
        Mesh vMesh = vMf.sharedMesh;
        if (vMesh == null)
        {
            vMesh = new Mesh();
            vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        else
        {
            vMesh.Clear();
        }
        vMesh.SetVertices(pData.vertices);
        vMesh.SetNormals(pData.normals);
        vMesh.SetTriangles(pData.triangles, 0);
        vMesh.RecalculateBounds();
        vMf.sharedMesh = vMesh;
        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        pChunk.mViewGO.GetComponent<MeshRenderer>().enabled = true;


        //MeshCollider vMc = pChunk.mViewGO.GetComponent<MeshCollider>();
        //if (vMc != null)
        //{
        //    if (IsValidForCollider(pData))
        //        vMc.sharedMesh = vMesh;
        //    else
        //    {
        //        vMc.sharedMesh = null;
        //        LogColliderSkip(pChunk, pData);
        //    }
        //}

        int index = pChunk.mIndex;
        int lodApplied = Grid.ResolutionToLodIndex(pChunk.mSize);
        mGrid.SetLod(index, lodApplied);
        mGrid.SetProcessing(index, false);

        
    }

    public static string DebugState(Chunk chunk)
    {
        if (chunk == null)
            return "[ChunkDebug] NULL chunk";

        if (chunk.mGrid == null)
            return "[ChunkDebug] Grid NULL";

        ushort status = chunk.mGrid.mStatusGrid[chunk.mIndex];

        bool surface = (status & Grid.BIT_SURFACE) != 0;
        bool processing = (status & Grid.MASK_PROCESSING) != 0;
        int lodCurrent = (status & Grid.MASK_LOD_CURRENT) >> 2;
        int lodTarget = (status & Grid.MASK_LOD_TARGET) >> 4;

        return
            $"[ChunkDebug] " +
            $"Slot={chunk.mCoord} | " +
            $"Global={chunk.mGlobalCoord} | " +
            $"Index={chunk.mIndex} | " +
            $"Size={chunk.mSize} | " +
            $"Edited={chunk.mIsEdited} | " +
            $"Bool1={chunk.mBool1} | " +
            $"Bool2={chunk.mBool2} | " +
            $"Surface={surface} | " +
            $"Processing={processing} | " +
            $"LOD_Current={lodCurrent} | " +
            $"LOD_Target={lodTarget} | " +
            $"StatusRaw=0x{status:X4} | " +
            $"WorldOrigin={chunk.WorldOrigin}";
    }

    /// <summary>
    /// Unity exige "at least three distinct vertices" para MeshCollider.
    /// Comprueba vùrtices totales y que haya al menos 3 posiciones distintas.
    /// </summary>
    private static bool IsValidForCollider(MeshData pData)
    {
        if (pData == null || pData.vertices == null || pData.vertices.Count < 3)
            return false;
        var distinct = new HashSet<Vector3>();
        for (int i = 0; i < pData.vertices.Count; i++)
            distinct.Add(pData.vertices[i]);
        return distinct.Count >= 3;
    }

    private static int _colliderSkipLogCount = 0;
    private const int COLLIDER_SKIP_LOG_CAP = 50;

    /// <summary>
    /// Diagnùstico: por quù se omitiù el collider (para dar certeza, no teorùa).
    /// </summary>
    private static void LogColliderSkip(Chunk pChunk, MeshData pData)
    {
        int totalV = pData?.vertices?.Count ?? 0;
        int distinctV = 0;
        if (pData != null && pData.vertices != null && pData.vertices.Count > 0)
        {
            var set = new HashSet<Vector3>();
            for (int i = 0; i < pData.vertices.Count; i++)
                set.Add(pData.vertices[i]);
            distinctV = set.Count;
        }
        int totalT = pData?.triangles?.Count ?? 0;
        _colliderSkipLogCount++;
        //if (_colliderSkipLogCount <= COLLIDER_SKIP_LOG_CAP)
        //    Debug.LogWarning($"[ColliderSkip #{_colliderSkipLogCount}] chunk={pChunk.mCoord} mSize={pChunk.mSize} mBool1={pChunk.mBool1} mBool2={pChunk.mBool2} | vertices={totalV} distinct={distinctV} triangles={totalT} | collider requiere >=3 distintos.");
        //else if (_colliderSkipLogCount == COLLIDER_SKIP_LOG_CAP + 1)
        //    Debug.LogWarning($"[ColliderSkip] Mùs skips (total>{COLLIDER_SKIP_LOG_CAP}). Dejar de loguear hasta reinicio.");
    }
}