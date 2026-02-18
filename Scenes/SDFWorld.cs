using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
    //RenderQueue mRenderQueueMulti;
    //RenderQueueAsync mRenderQueueAsync;
    RenderStackAsync mRenderQueueAsync;

    MeshGenerator mMeshGenerator;
    SurfaceNetsGenerator mSurfaceNet = new SurfaceNetsGenerator();
    SurfaceNetsGeneratorQEFOriginal2 mSurfaceNetQEF = new SurfaceNetsGeneratorQEFOriginal2();
    private CancellationTokenSource mCTS;

    // Los nuevos motores de LOD
    private Vigilante mVigilante;
    private DecimationManager mDecimator;

    [Header("Configuración de Carga / LOD")]
    [SerializeField] int mMinQueueToProcess = 5;  // Procesar en cuanto haya al menos 1 chunk (LOD activo)
    [SerializeField] float mMaxWaitTime = 0.2f;   // Si hay cola y no llegamos al mínimo, esperar como mucho esto
    [SerializeField] int mChunksPerFrame = 8;     // Chunks a resamplear + mallar por frame (LOD visible)
    [SerializeField] float mMaxMillisecondsPerFrame = 16.0f; // Tiempo máximo de procesamiento por frame (16ms = 60fps)
    private float mTimer = 0f;
    private bool mIsProcessing = false;           // Flag para saber si estamos procesando
    private Queue<RenderJob> mProcessingBuffer = new Queue<RenderJob>(); // Cola temporal de procesamiento



    void Start()
    {
        #region Codigo para medir inicializacion.
        //Stopwatch sw = null;
        //sw = Stopwatch.StartNew();
        //sw.Stop();
        //UnityEngine.Debug.Log($"[SurfaceNets] TERRAIN SAMPLER: {sw.Elapsed.TotalMilliseconds:F3} ms");
        #endregion

        // ... dentro de tu método de inicialización ...

        mChunkSize = VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        mGridInChunks = new Vector3Int(64, 4, 64);
        mGridInUnits = mGridInChunks * mChunkSize;
        mGrid = new Grid(mGridInChunks, mChunkSize);

        // Crear una instancia de Stopwatch para las mediciones
        Stopwatch sw = new Stopwatch();

        // --- MEDICIÓN 1: Sample ---
        sw.Start();
        mGrid.ApplyToChunks(SDFGenerator.Sample);
        //SDFGenerator.LoadHeightmapToGrid(mGrid, @"E:\maps\1.png");
        sw.Stop();
        UnityEngine.Debug.Log($"[PERF] mGrid.ApplyToChunks(SDFGenerator.Sample) tardó: {sw.ElapsedMilliseconds} ms");
        sw.Reset();

        // --- MEDICIÓN 2: MarkSurface ---
        sw.Start();
        mGrid.ApplyToChunks(mGrid.MarkSurface);
        sw.Stop();
        UnityEngine.Debug.Log($"[PERF] mGrid.ApplyToChunks(mGrid.MarkSurface) tardó: {sw.ElapsedMilliseconds} ms");
        sw.Reset();

        mRenderQueueAsync = new RenderStackAsync(mGrid);

        // --- MEDICIÓN 3: InitWorld ---
        sw.Start();
        InitWorld();
        sw.Stop();
        UnityEngine.Debug.Log($"[PERF] InitWorld tardó: {sw.ElapsedMilliseconds} ms");

        // --- RESUMEN ---
        // Pu

        #region Vigilante Lod Code
        // 2. Inicializar el Decimator (Cerebro)
        mDecimator = new DecimationManager();
        mDecimator.Setup(mRenderQueueAsync, mSurfaceNetQEF);

        // 3. Inicializar el Vigilante (Ojos)
        mVigilante = new Vigilante();
        mVigilante.Setup(mGrid, mDecimator); // O el transform del Player
        mVigilante.vCurrentCamPos = mCamera.transform.position;

        // 4. ¡ARRANQUE DEL HILO!
        // No usamos 'await' aquí para que no bloquee el Start de Unity.
        // Simplemente lanzamos la tarea al aire.

        //Task.Run(delegate { return mVigilante.Run(); });
        mCTS = new CancellationTokenSource();

        // INVOCACIÓN CORRECTA:
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
            Chunk vChunk = mGrid.mChunks[i];
            vChunk.PrepareView(mGrid.mWorldRoot.transform, mSurfaceMaterial);
            mRenderQueueAsync.Enqueue(vChunk, mMeshGenerator);
        }

        //mRenderQueueMulti.ProcessParallel(-1);

        //while (mRenderQueueMulti.mResultsLOD.TryDequeue(out var vResultLOD))
        //    mRenderQueueMulti.Apply(vResultLOD.Key, vResultLOD.Value);
        //mRenderQueue.ProcessSequential();
    }

    public void ExecuteModification(Vector3 pHitPoint, Vector3 pHitNormal, byte pNewValue)
    {
        Vector3 vTargetPos = pHitPoint - pHitNormal * 0.2f;
        VoxelBrush vBrush = new VoxelBrush(vTargetPos, 2.4f, 0.3f, 0.18f, pNewValue == 0);

        // 1. El Grid nos devuelve la colección de "afectados"
        HashSet<int> vChunksToRebuild = mGrid.ModifyWorld(vBrush);

        // 2. World decide cómo y cuándo renderizarlos
        foreach (int vIndex in vChunksToRebuild)
        {
            Chunk vChunk = mGrid.mChunks[vIndex];

            // Aquí centralizamos la orden de renderizado
            //if (mRenderQueue != null)
            //{
            //    mRenderQueue.Enqueue(vChunk, mSurfaceNet);
            //}
        }

        //mRenderQueue.ProcessParallel();
        //mRenderQueue.ProcessSequential();
    }

    void Update()
    {

        int appliedThisFrame = 0;
        bool cont = true;
        //// 0. ACTUALIZAR POSICIÓN DE CÁMARA
        if (mVigilante != null && mCamera != null)
        {
            mVigilante.vCurrentCamPos = mCamera.transform.position;
        }

        appliedThisFrame = 0;
        
        if (cont)
        {
            cont = mRenderQueueAsync.mResultsLOD.TryDequeue(out var r);
            if (cont)
            {
                mRenderQueueAsync.Apply(r.Key, r.Value);
                appliedThisFrame++;
            }
            
        }
        // 1. RESAMPLE PENDIENTE (SDF Sampling)
        // El decimator hace el Sample y ahora Encola en la cola MULTIHILO
        if (mDecimator != null)
            mDecimator.ProcessPendingResamples(100);

        // 2. PROCESAMIENTO MULTIHILO (Delegamos la carga pesada)
        // Usamos el 50% de los hilos (0 = hilos lógicos - 1, o un número específico)
        //nThreads: 0 o Environment.ProcessorCount / 2
        //int hilosAUsar = Mathf.Max(1, System.Environment.ProcessorCount / 2);
        //mRenderQueueMulti.ProcessParallel(hilosAUsar);

        // 3. APLICACIÓN DE RESULTADOS (Main Thread)
        // Consumimos los resultados que el proceso paralelo ha ido dejando en mResultsLOD
        appliedThisFrame = 0;
        cont = true;
        if (cont)
        {
            cont = mRenderQueueAsync.mResultsLOD.TryDequeue(out var vResult);
            if (cont)
            {
                mRenderQueueAsync.Apply(vResult.Key, vResult.Value);
                appliedThisFrame++;
            }


        }
    }

    void OnDisable()
    {
        if (mCTS != null)
        {
            mCTS.Cancel(); // Corta el flujo
            mCTS.Dispose(); // Libera el objeto de la memoria
        }
    }

}