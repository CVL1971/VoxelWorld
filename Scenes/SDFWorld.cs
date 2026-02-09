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
    SurfaceNetsGeneratorQEF mSurfaceNet = new SurfaceNetsGeneratorQEF();
    SurfaceNetsGeneratorQEF mSurfaceNetQEF = new SurfaceNetsGeneratorQEF();
  

    void Start()
    {

        Stopwatch sw = null;
        sw = Stopwatch.StartNew();

        mRenderQueue = new RenderQueue(mGrid);
        mGridInChunks = new Vector3Int(32, 2, 32);
        mGridInUnits = mGridInChunks * mChunkSize;
        mGrid = new Grid(mGridInChunks, mChunkSize);
        mGrid.ReadFromSDFGenerator();
        
   
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
    }

    void Update()
    {

        if (mGrid == null) return;

        // Usamos el out con el tipo de la estructura explícita
        if (mRenderQueue.TryDequeue(out RenderRequest vRequest))
        {
            // Acceso directo a los nombres de la estructura
            Chunk vChunk = vRequest.chunk;
            MeshGenerator vGenerator = vRequest.generator;

            if (vChunk.mViewGO == null)
            {
                string goName = $"SurfaceNet_Chunk_{vChunk.mCoord.x}_{vChunk.mCoord.y}_{vChunk.mCoord.z}";

                // Creamos el objeto solo si no existe en la referencia del Chunk
                vChunk.mViewGO = new GameObject(goName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                vChunk.mViewGO.transform.parent = mGrid.mWorldRoot.transform;

                // chunk.mViewGO.transform.position = Vector3.zero;

                // LA POSICIÓN DEL OBJETO es la única que debe tener el mWorldOrigin
                vChunk.mViewGO.transform.position = (Vector3)vChunk.mWorldOrigin;
                //renderer.material = mSurfaceMaterial;

                MeshRenderer vMr = vChunk.mViewGO.GetComponent<MeshRenderer>();
                if (vMr != null)
                {
                    vMr.sharedMaterial = mSurfaceMaterial;

                    // 2. Creamos y configuramos el bloque de propiedades
                    MaterialPropertyBlock vPropBlock = new MaterialPropertyBlock();
                    vMr.GetPropertyBlock(vPropBlock); // Obtenemos lo que ya tenga para no sobreescribir otros datos

                    // 3. Generamos color y lo inyectamos (Usa "_BaseColor" para URP o "_Color" para Built-in)
                    Color vRandomColor = new Color(Random.value, Random.value, Random.value);
                    vPropBlock.SetColor("_BaseColor", vRandomColor);

                    // 4. Aplicamos al renderer
                    vMr.SetPropertyBlock(vPropBlock);
                }
            }

            // Generación de malla
            Mesh vNewMesh = vGenerator.Generate(
                vChunk,
                mGrid.mChunks,
                mGrid.mSizeInChunks
            );

            mRenderQueue.Apply(vChunk, vNewMesh);
        }

    }

    void BuildSurfaceNets()
    {
        mMeshGenerator = mSurfaceNetQEF;
      
        for (int i = 0; i < mGrid.mChunks.Length; i++)
        {
            mRenderQueue.Enqueue(mGrid.mChunks[i], mMeshGenerator);

        }
    }

}

