using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

public class World : MonoBehaviour
{

    int mChunkSize = VoxelUtils.UNIVERSAL_CHUNK_SIZE;

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
    RenderQueueMonohilo mRenderQueue;

    MeshGenerator mMeshGenerator;
    SurfaceNetsGenerator mSurfaceNet = new SurfaceNetsGenerator();
    SurfaceNetsGeneratorQEF mSurfaceNetQEF = new SurfaceNetsGeneratorQEF();
    private CancellationTokenSource mCTS;

    // Los nuevos motores de LOD
    private Vigilante mVigilante;
    private DecimationManager mDecimator;

    [Header("Configuración de Carga")]
    [SerializeField] int mMinQueueToProcess = 10; // No disparamos núcleos por menos de 10 chunks de LOD
    [SerializeField] float mMaxWaitTime = 1.0f;   // Pero si pasa 1 segundo, procesamos lo que haya
    [SerializeField] int mChunksPerFrame = 1;     // Cuántos chunks procesamos por frame (ajusta según rendimiento)
    [SerializeField] float mMaxMillisecondsPerFrame = 16.0f; // Tiempo máximo de procesamiento por frame (16ms = 60fps)
    private float mTimer = 0f;
    private bool mIsProcessing = false;           // Flag para saber si estamos procesando
    private Queue<RenderRequest> mProcessingBuffer = new Queue<RenderRequest>(); // Cola temporal de procesamiento


    void Start()
    {

        Stopwatch sw = null;
        sw = Stopwatch.StartNew();

        mRenderQueue = new RenderQueueMonohilo();
        mGridInChunks = new Vector3Int(8, 2, 8);
        mGridInUnits = mGridInChunks * mChunkSize;
        mGrid = new Grid(mGridInChunks, mChunkSize);
        mGrid.ReadFromSDFGenerator();
        mRenderQueue.Setup(mGrid);


        sw.Stop();
        UnityEngine.Debug.Log($"[SurfaceNets] TERRAIN SAMPLER: {sw.Elapsed.TotalMilliseconds:F3} ms");
        sw = Stopwatch.StartNew();

        if (true) BuildSurfaceNets();

        sw.Stop();
        UnityEngine.Debug.Log($"[SurfaceNets] MESH BUILDER: {sw.Elapsed.TotalMilliseconds:F3} ms");



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
        mRenderQueue.ProcessSequential();
    }

    void Update()
    {
        // Alimentamos la sonda con la posición actual de forma segura
        if (mVigilante != null && mCamera != null)
        {
            mVigilante.vCurrentCamPos = mCamera.transform.position;
        }

        // 1. APLICAR RESULTADOS (siempre rápido, solo aplicar mallas ya generadas)
        // Aplicamos varios resultados por frame ya que es una operación ligera
        for (int i = 0; i < 8; i++)
        {
            if (mRenderQueue.mResults.TryDequeue(out var vResult))
            {
                mRenderQueue.Apply(vResult.Key, vResult.Value);
            }
            else
            {
                break;
            }
        }

        // 2. GESTIÓN DE PROCESAMIENTO GRADUAL
        int vQueueCount = mRenderQueue.mQueue.Count;

        // Si hay trabajo pendiente, incrementamos el timer
        if (vQueueCount > 0)
        {
            mTimer += Time.deltaTime;
        }

        // DECISIÓN: ¿Iniciamos el procesamiento?
        if (!mIsProcessing && vQueueCount > 0)
        {
            // Condiciones para iniciar procesamiento:
            // 1. Hay suficientes chunks acumulados
            // 2. O ha pasado suficiente tiempo
            if (vQueueCount >= mMinQueueToProcess || mTimer >= mMaxWaitTime)
            {
                // Transferimos la cola principal al buffer de procesamiento
                while (mRenderQueue.mQueue.Count > 0)
                {
                    mProcessingBuffer.Enqueue(mRenderQueue.mQueue.Dequeue());
                }
                mRenderQueue.mInWait.Clear();
                mIsProcessing = true;
                mTimer = 0f;

                UnityEngine.Debug.Log($"[World] Iniciando procesamiento de {mProcessingBuffer.Count} chunks de forma gradual");
            }
        }

        // 3. PROCESAMIENTO GRADUAL (distribuido en el tiempo)
        if (mIsProcessing && mProcessingBuffer.Count > 0)
        {
            Stopwatch sw = Stopwatch.StartNew();
            int chunksProcessed = 0;

            // Procesamos chunks hasta alcanzar el límite de tiempo o chunks por frame
            while (mProcessingBuffer.Count > 0 &&
                   chunksProcessed < mChunksPerFrame &&
                   sw.Elapsed.TotalMilliseconds < mMaxMillisecondsPerFrame)
            {
                RenderRequest vRequest = mProcessingBuffer.Dequeue();
                Chunk vChunk = vRequest.chunk;

                // --- GESTIÓN DE RESOLUCIÓN (LOD) ---
                if (vChunk.mTargetSize > 0 && !vChunk.mIsEdited)
                {
                    vChunk.Redim(vChunk.mTargetSize);
                    SDFGenerator.Sample(vChunk);
                }

                // --- GENERACIÓN DE MALLA ---
                MeshData vData = vRequest.generator.Generate(
                    vChunk,
                    mGrid.mChunks,
                    mGrid.mSizeInChunks
                );

                // Consumimos la orden
                vChunk.mTargetSize = 0;

                KeyValuePair<Chunk, MeshData> vResultado = new KeyValuePair<Chunk, MeshData>(vChunk, vData);
                mRenderQueue.mResults.Enqueue(vResultado);

                chunksProcessed++;
            }

            sw.Stop();

            // Log de progreso (opcional, comenta si genera spam)
            if (chunksProcessed > 0)
            {
                UnityEngine.Debug.Log($"[World] Procesados {chunksProcessed} chunks en {sw.Elapsed.TotalMilliseconds:F2}ms. Quedan {mProcessingBuffer.Count}");
            }

            // ¿Hemos terminado?
            if (mProcessingBuffer.Count == 0)
            {
                mIsProcessing = false;
                UnityEngine.Debug.Log("[World] Procesamiento completado");
            }
        }
    }

    void BuildSurfaceNets()
    {
        mMeshGenerator = mSurfaceNetQEF;

        // 1. Encolamos todos los chunks de forma normal (Main Thread)
        for (int i = 0; i < mGrid.mChunks.Length; i++)
        {
            Chunk vChunk = mGrid.mChunks[i];

            // Inicializamos el GameObject aquí (Main Thread) porque es seguro
            if (vChunk.mViewGO == null)
            {
                vChunk.mViewGO = new GameObject("Chunk_" + vChunk.mCoord, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                vChunk.mViewGO.transform.parent = mGrid.mWorldRoot.transform;
                vChunk.mViewGO.transform.position = (Vector3)vChunk.mWorldOrigin;
                vChunk.mViewGO.GetComponent<MeshRenderer>().sharedMaterial = mSurfaceMaterial;
            }

            mRenderQueue.Enqueue(vChunk, mMeshGenerator);
        }

        // 2. DISPARAMOS LA PARALELIZACIÓN
        UnityEngine.Debug.Log("Iniciando generación paralela... Preparate para el error.");
        //mRenderQueue.ProcessParallel();
        mRenderQueue.ProcessSequential();
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