//UNA NUEVA MODIFICACION

using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

public class World : MonoBehaviour
{
 
   int mChunkSize = 32;

    [Header("Rendering")]
    [SerializeField] Material mSolidMaterial;

    [SerializeField]
    Material mSurfaceMaterial;
    Grid mGrid;
    Vector3Int mGridInUnits;
    Vector3Int mGridInChunks;
    Mesh mWorldMesh;
    RenderQueue mRenderQueue;

    MeshGenerator mMeshGenerator;
    SurfaceNetsGenerator mSurfaceNet = new SurfaceNetsGenerator();
    SurfaceNetsGeneratorQEF mSurfaceNetQEF = new SurfaceNetsGeneratorQEF();
  

    void Start()
    {

        Stopwatch sw = null;
        sw = Stopwatch.StartNew();

        mRenderQueue = new RenderQueue();
        mGridInChunks = new Vector3Int(16, 16, 16); ;
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

        mRenderQueue.ProcessParallel();
    }

    void Update()
    {

       // NO metas esto dentro de un if(generando). 
    // Debe estar siempre activo para "escuchar" cuando los hilos terminen.
    for(int i = 0; i < 8; i++) // Probemos con 8 para ir más rápido
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


        //if (mGrid == null) return;

        //// Usamos el out con el tipo de la estructura explícita
        //if (mRenderQueue.TryDequeue(out RenderRequest vRequest))
        //{
        //    // Acceso directo a los nombres de la estructura
        //    Chunk vChunk = vRequest.chunk;
        //    MeshGenerator vGenerator = vRequest.generator;

        //    if (vChunk.mViewGO == null)
        //    {
        //        string goName = $"SurfaceNet_Chunk_{vChunk.mCoord.x}_{vChunk.mCoord.y}_{vChunk.mCoord.z}";

        //        // Creamos el objeto solo si no existe en la referencia del Chunk
        //        vChunk.mViewGO = new GameObject(goName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        //        vChunk.mViewGO.transform.parent = mGrid.mWorldRoot.transform;

        //        // chunk.mViewGO.transform.position = Vector3.zero;

        //        // LA POSICIÓN DEL OBJETO es la única que debe tener el mWorldOrigin
        //        vChunk.mViewGO.transform.position = (Vector3)vChunk.mWorldOrigin;
        //        //renderer.material = mSurfaceMaterial;

        //        MeshRenderer vMr = vChunk.mViewGO.GetComponent<MeshRenderer>();
        //        if (vMr != null)
        //        {
        //            vMr.sharedMaterial = mSurfaceMaterial;

        //            // 2. Creamos y configuramos el bloque de propiedades
        //            MaterialPropertyBlock vPropBlock = new MaterialPropertyBlock();
        //            vMr.GetPropertyBlock(vPropBlock); // Obtenemos lo que ya tenga para no sobreescribir otros datos

        //            // 3. Generamos color y lo inyectamos (Usa "_BaseColor" para URP o "_Color" para Built-in)
        //            Color vRandomColor = new Color(Random.value, Random.value, Random.value);
        //            vPropBlock.SetColor("_BaseColor", vRandomColor);

        //            // 4. Aplicamos al renderer
        //            vMr.SetPropertyBlock(vPropBlock);
        //        }
        //    }

        //    // Generación de malla
        //    Mesh vNewMesh = vGenerator.Generate(
        //        vChunk,
        //        mGrid.mChunks,
        //        mGrid.mSizeInChunks
        //    );

        //    mRenderQueue.Apply(vChunk, vNewMesh);
        //}

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
        mRenderQueue.ProcessParallel();
    }

}

