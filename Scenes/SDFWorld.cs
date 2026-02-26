using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class World : MonoBehaviour
{

    int mChunkSize;

    [Header("Rendering")]
    [SerializeField] Material mSolidMaterial;
    private float mDebugTimer = 0;


    [Header("PlayerPosition")]
    [SerializeField] Camera mCamera;

    [SerializeField]
    Material mSurfaceMaterial;
    Grid mGrid;
    Vector3Int mGridInUnits;
    Vector3Int mGridInChunks;
    Mesh mWorldMesh;

    RenderQueueAsync mRenderQueueAsync;
    DensitySamplerQueueAsync mDensitySampler;

    MeshGenerator mMeshGenerator;
    SurfaceNetsGeneratorQEF3caches mSurfaceNet = new SurfaceNetsGeneratorQEF3caches();
    SurfaceNetsGeneratorQEF3caches mSurfaceNetQEF = new SurfaceNetsGeneratorQEF3caches();
    private CancellationTokenSource mCTS;

  
    private Vigilante mVigilante;
    private DecimationManager mDecimator;

    [Header("Debug: límites de chunks")]
    [SerializeField] bool mDrawChunkBounds = false;

    [Header("Configuración de Carga / LOD")]
    [SerializeField] int mMinQueueToProcess = 1;  // Procesar en cuanto haya al menos 1 chunk (LOD activo)
    [SerializeField] float mMaxWaitTime = 0.4f;   // Si hay cola y no llegamos al mínimo, esperar como mucho esto
    [SerializeField] int mChunksPerFrame = 40;     // Chunks a resamplear + mallar por frame (LOD visible)
    [SerializeField] float mMaxMillisecondsPerFrame = 32.0f; // Tiempo máximo de procesamiento por frame (16ms = 60fps)
    private float mTimer = 0f;
    private bool mIsProcessing = false;           // Flag para saber si estamos procesando
   



    void Start()
    {
        #region Codigo para medir inicializacion.
        //Stopwatch sw = null;
        //sw = Stopwatch.StartNew();
        //sw.Stop();
        #endregion

       

        mChunkSize = VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        mGridInChunks = new Vector3Int(64, 4, 64);
        mGridInUnits = mGridInChunks * mChunkSize;
        mGrid = new Grid(mGridInChunks, mChunkSize, mCamera.transform.position);

        //mGrid.ApplyToChunks(SDFGenerator.Sample);
        //SDFGenerator.LoadHeightmapToGrid(mGrid, @"E:\maps\1.png");
        //mGrid.ApplyToChunks(mGrid.MarkSurface); 
        mRenderQueueAsync = new RenderQueueAsync(mGrid);
        mDensitySampler = new DensitySamplerQueueAsync();
        // 2. Inicializar el Decimator (Cerebro)
        mDecimator = new DecimationManager();
        mDecimator.Setup(mRenderQueueAsync, mDensitySampler, mSurfaceNetQEF);
        InitWorld();
       
        #region Vigilante Lod Code
       
        // 3. Inicializar el Vigilante (Ojos)
        mVigilante = new Vigilante();
        mVigilante.Setup(mGrid, mDecimator); // O el transform del Player
        mVigilante.vCurrentCamPos = mCamera.transform.position;

        //Task.Run(delegate { return mVigilante.Run(); });
        mCTS = new CancellationTokenSource();

        // Le pasamos el mCTS.Token para que el Vigilante sepa cuándo morir
        Task.Run(() => mVigilante.Run(mCTS.Token), mCTS.Token);

        #endregion

        //HeightmapManager.SaveGridToHeightmap(mGrid);
    }


    void InitWorld()
    {
        mMeshGenerator = mSurfaceNetQEF;

        for (int i = 0; i < mGrid.mChunks.Length; i++)
        {
            mGrid.SetProcessing(i, true);
            Chunk vChunk = mGrid.mChunks[i];
            vChunk.PrepareView(mGrid.mWorldRoot.transform, mSurfaceMaterial);
            mDensitySampler.Enqueue(vChunk);
        }

        //mRenderQueueMulti.ProcessParallel(-1);
    }

    public void ExecuteModification(Vector3 pHitPoint, Vector3 pHitNormal, byte pNewValue)
    {
        Vector3 vTargetPos = pHitPoint - pHitNormal * 0.2f;
        VoxelBrush vBrush = new VoxelBrush(vTargetPos, 20f, 0.3f, 0.18f, pNewValue == 0);

        // 1. El Grid nos devuelve la colección de "afectados"
        HashSet<int> vChunksToRebuild = mGrid.ModifyWorld(vBrush);

        // 2. World decide cómo y cuándo renderizarlos
        foreach (int vIndex in vChunksToRebuild)
        {
            Chunk vChunk = mGrid.mChunks[vIndex];

            //Aquí centralizamos la orden de renderizado
            if (mRenderQueueAsync != null)
            {
                mRenderQueueAsync.Enqueue(vChunk, mSurfaceNet);
            }
        }

        //mRenderQueue.ProcessParallel();
        //mRenderQueue.ProcessSequential();
    }

    void Update()
    {
        Vector3Int newChunk;

        if (mGrid.TryGetNewPlayerChunk(mCamera.transform.position, out newChunk))
        {
            UnityEngine.Debug.Log("Player entered new chunk: " + newChunk);

            mGrid.UpdateStreamingX(newChunk, mDensitySampler);
        }

      
        

        // 0. Actualizar posición de cámara (Vigilante la usa para detectar LOD)
        //if (mVigilante != null && mCamera != null)
        //    mVigilante.vCurrentCamPos = mCamera.transform.position;

        // 1. PRIMERO: Procesar cambios de LOD pendientes (Redim + Enqueue)
        //    Con 3 caches por chunk no hay resample, solo cambio de mSize y mallado
        if (mDecimator != null)
            mDecimator.ProcessPendingResamples(20);

        while (mDensitySampler.DensitySamplerResult.TryDequeue(out var r))
        {
            mGrid.MarkSurface(r);
            mRenderQueueAsync.Enqueue(r, mMeshGenerator);
        }

        // 2. Aplicar todos los resultados de mallado disponibles este frame
        while (mRenderQueueAsync.mResultsLOD.TryDequeue(out var r))
            mRenderQueueAsync.Apply(r.Key, r.Value);
    }

    void OnDisable()
    {
        if (mCTS != null)
        {
            mCTS.Cancel(); // Corta el flujo
            mCTS.Dispose(); // Libera el objeto de la memoria
        }
    }

    void OnDrawGizmos()
    {
        if (!mDrawChunkBounds || mGrid == null || mGrid.mChunks == null) return;

        Gizmos.color = Color.cyan;
        foreach (Chunk c in mGrid.mChunks)
        {
            if (c == null) continue;
            Vector3 min = (Vector3)c.WorldOrigin;
            float s = VoxelUtils.UNIVERSAL_CHUNK_SIZE;
            Vector3 max = min + new Vector3(s, s, s);

            Vector3 p000 = min;
            Vector3 p100 = new Vector3(max.x, min.y, min.z);
            Vector3 p010 = new Vector3(min.x, max.y, min.z);
            Vector3 p110 = new Vector3(max.x, max.y, min.z);
            Vector3 p001 = new Vector3(min.x, min.y, max.z);
            Vector3 p101 = new Vector3(max.x, min.y, max.z);
            Vector3 p011 = new Vector3(min.x, max.y, max.z);
            Vector3 p111 = max;

            Gizmos.DrawLine(p000, p100); Gizmos.DrawLine(p100, p110); Gizmos.DrawLine(p110, p010); Gizmos.DrawLine(p010, p000);
            Gizmos.DrawLine(p001, p101); Gizmos.DrawLine(p101, p111); Gizmos.DrawLine(p111, p011); Gizmos.DrawLine(p011, p001);
            Gizmos.DrawLine(p000, p001); Gizmos.DrawLine(p100, p101); Gizmos.DrawLine(p110, p111); Gizmos.DrawLine(p010, p011);
        }
    }

}