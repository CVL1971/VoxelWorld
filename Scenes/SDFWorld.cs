using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

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
    RenderQueueMono mRenderQueue;
    RenderQueue mRenderQueueMulti;

    MeshGenerator mMeshGenerator;
    SurfaceNetsGenerator mSurfaceNet = new SurfaceNetsGenerator();
    SurfaceNetsGeneratorQEFOriginal mSurfaceNetQEF = new SurfaceNetsGeneratorQEFOriginal();
    private CancellationTokenSource mCTS;

    // Los nuevos motores de LOD
    private Vigilante mVigilante;
    private DecimationManager mDecimator;

    [Header("Configuración de Carga / LOD")]
    [SerializeField] int mMinQueueToProcess = 5;  // Procesar en cuanto haya al menos 1 chunk (LOD activo)
    [SerializeField] float mMaxWaitTime = 0.2f;   // Si hay cola y no llegamos al mínimo, esperar como mucho esto
    [SerializeField] int mChunksPerFrame = 2;     // Chunks a resamplear + mallar por frame (LOD visible)
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

        mChunkSize = VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        mGridInChunks = new Vector3Int(16, 3, 16);
        mGridInUnits = mGridInChunks * mChunkSize;
        mGrid = new Grid(mGridInChunks, mChunkSize);
        mGrid.ApplyToChunks(SDFGenerator.Sample);
        mRenderQueue = new RenderQueueMono(mGrid);
        mRenderQueueMulti = new RenderQueue(mGrid);
        InitWorld();

        #region Vigilante Lod Code
        // 2. Inicializar el Decimator (Cerebro)
        mDecimator = new DecimationManager();
        mDecimator.Setup(mRenderQueue, mSurfaceNetQEF);

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
    }


    void InitWorld()
    {
        mMeshGenerator = mSurfaceNetQEF;

        for (int i = 0; i < mGrid.mChunks.Length; i++)
        {
            Chunk vChunk = mGrid.mChunks[i];
            vChunk.PrepareView(mGrid.mWorldRoot.transform, mSurfaceMaterial);
            mRenderQueueMulti.Enqueue(vChunk, mMeshGenerator);
        }

        mRenderQueueMulti.ProcessParallel(-1);

        while (mRenderQueueMulti.mResultsLOD.TryDequeue(out var vResultLOD))
            mRenderQueueMulti.Apply(vResultLOD.Key, vResultLOD.Value);
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
            if (mRenderQueue != null)
            {
                mRenderQueue.Enqueue(vChunk, mSurfaceNet);
            }
        }

        //mRenderQueue.ProcessParallel();
        //mRenderQueue.ProcessSequential();
    }

    void Update()
    {
        // 0. ACTUALIZAR POSICIÓN DE CÁMARA
        if (mVigilante != null && mCamera != null)
        {
            mVigilante.vCurrentCamPos = mCamera.transform.position;
        }

        // 1. RESAMPLE PENDIENTE (SDF Sampling)
        // El decimator ahora es el ÚNICO que hace Redim y Sample.
        // Al terminar, el propio decimator mete el chunk en mRenderQueue.mQueue.
        if (mDecimator != null)
            mDecimator.ProcessPendingResamples(mChunksPerFrame);

        // 2. PROCESAMIENTO DE GEOMETRÍA (Mallas)
        // Procesamos de forma gradual para no bloquear el frame.
        if (mRenderQueue.mQueue.Count > 0)
        {
            Stopwatch sw = Stopwatch.StartNew();
            int chunksProcessed = 0;

            // Extraemos directamente de la cola principal, eliminando buffers intermedios
            while (mRenderQueue.mQueue.Count > 0 &&
                   chunksProcessed < mChunksPerFrame &&
                   sw.Elapsed.TotalMilliseconds < mMaxMillisecondsPerFrame)
            {
                RenderJob vJob = mRenderQueue.mQueue.Dequeue();

                // Generamos la malla (sin volver a hacer Sample, el dato ya está listo)
                MeshData vData = vJob.mMeshGenerator.Generate(
                    vJob.mChunk,
                    mGrid.mChunks,
                    mGrid.mSizeInChunks
                );

                // Almacenamos en mResults para su aplicación
                mRenderQueue.mResults.Enqueue(new KeyValuePair<Chunk, MeshData>(vJob.mChunk, vData));

                // Liberamos el estado de espera en la cola
                mRenderQueue.mInWait.Remove(vJob.mChunk);
                chunksProcessed++;
            }
        }

        // 3. APLICACIÓN DE MALLAS A UNITY (Main Thread)
        // Aplicamos los resultados de la generación de mallas a los GameObjects.
        int appliedThisFrame = 0;
        while (mRenderQueue.mResults.Count > 0 && appliedThisFrame < 8)
        {
            if (mRenderQueue.mResults.TryDequeue(out var vResult))
            {
                mRenderQueue.Apply(vResult.Key, vResult.Value);
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