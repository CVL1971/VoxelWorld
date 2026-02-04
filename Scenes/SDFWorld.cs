using UnityEngine;
using System.Collections.Generic;

public class World : MonoBehaviour
{
 
    [Header("World settings")]
    [SerializeField] int mChunkSize = 16;

    [Header("Rendering")]
    [SerializeField] Material mSolidMaterial;

    [SerializeField]
    Material mSurfaceMaterial;

    Chunk[] mChunks;
    Vector3Int mWorldChunkSize;
    GameObject mWorldRoot;
    Mesh mWorldMesh;

    MeshGenerator mMeshGenerator;




    void Start()
    {
        BuildWorld();
        if (true) BuildSurfaceNets();
    }
   

    void BuildWorld()
    {

        if (mWorldRoot != null)
            Destroy(mWorldRoot);

        mWorldRoot = new GameObject("WorldRoot");
        mWorldRoot.transform.position = Vector3.zero;

        // -----------------------------
        // 3. Create chunks
        // -----------------------------
        mWorldChunkSize = new Vector3Int(
            Mathf.CeilToInt(256/ (float)mChunkSize),
            Mathf.CeilToInt(64/ (float)mChunkSize),
            Mathf.CeilToInt(256/ (float)mChunkSize)
        );

        int chunkCount =
            mWorldChunkSize.x *
            mWorldChunkSize.y *
            mWorldChunkSize.z;

        mChunks = new Chunk[chunkCount];

        for (int z = 0; z < mWorldChunkSize.z; z++)
            for (int y = 0; y < mWorldChunkSize.y; y++)
                for (int x = 0; x < mWorldChunkSize.x; x++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    int index = ChunkIndex(x, y, z);
                    mChunks[index] = new Chunk(coord, mChunkSize);
                }

        // -----------------------------
        // 4. Phase 1: populate solids
        // -----------------------------
        foreach (Chunk chunk in mChunks)
        {
            // Una sola llamada por chunk:
            // 1. Calcula el ruido Perlin 2D (optimizado por columna)
            // 2. Rellena las densidades (para el suavizado visual)
            // 3. Establece los bytes de solidez (para colisiones y l�gica)
            SDFGenerator.Sample(chunk);
        }


        // -----------------------------
        // 6. Create views (temporal), semitransparent view test only purpose
        // -----------------------------
        //MeshGenerator generator =
        //    new VoxelMeshGenerator();

        //for (int i = 0; i < mChunks.Length; i++)
        //{
        //    ChunkSurfaceRender.Render(mChunks[i], generator, mWorldRoot.transform, mSolidMaterial);
        //}

    }

    // Integrar estos procedimientos en tu clase World (SDFWorld.cs)

    /// <summary>
    /// Punto de entrada para la modificación del terreno desde el exterior.
    /// </summary>
    public void ExecuteModification(Vector3 hitPoint, Vector3 hitNormal, byte newValue)
    {
        
        // 1. Ajuste de profundidad (Bias)
        // Para excavar (0), empujamos el punto de impacto un poco hacia adentro de la normal.
        // Para construir (1), lo empujamos hacia afuera.
        Vector3 targetPos = (newValue == 0) ? hitPoint - hitNormal * 0.1f : hitPoint + hitNormal * 0.1f;

        // 2. Obtener información de mapeo global -> local
        VoxelUtils.VoxelHit info = VoxelUtils.GetHitInfo(targetPos, mChunkSize, mWorldChunkSize);

        if (info.isValid)
        {
            // 3. Modificar el dato en el Chunk (Vaciar/Llenar Voxel)
            Chunk targetChunk = mChunks[info.chunkIndex];
            targetChunk.SetSolid(info.localPos.x, info.localPos.y, info.localPos.z, newValue);

            // 4. Identificar chunks que requieren reconstrucción (principal + limítrofes)
            List<int> affectedChunks = VoxelUtils.GetAffectedChunkIndices(info.globalVoxelPos, mChunkSize, mWorldChunkSize);

            // 5. Reconstrucción de mallas y colisionadores
            foreach (int index in affectedChunks)
            {
                RebuildChunkGeometry(mChunks[index]);
            }
        }
    }

    /// <summary>
    /// Regenera la representación visual y física de un chunk específico.
    /// </summary>
    private void RebuildChunkGeometry(Chunk pChunk)
    {

        
          
                Destroy(pChunk.mViewGO);
        pChunk = null;


        // 5. Eliminar la referencia lógica del array (opcional, dependiendo de si quieres que deje de existir)

    
        //// Invocamos al generador de Surface Nets
        //// Pasamos la colección completa de chunks para que GetDensitySafe resuelva los bordes
        //Mesh newMesh = mMeshGenerator.Generate(pChunk, mChunks, mWorldChunkSize);

        //// Localizar el GameObject siguiendo tu convención de nombres en BuildSurfaceNets
        //string goName = $"SurfaceNet_Chunk_{pChunk.mCoord.x}_{pChunk.mCoord.y}_{pChunk.mCoord.z}";
        //GameObject chunkGO = GameObject.Find(goName);

        //if (chunkGO != null)
        //{
        //    // Asignación de la nueva malla visual
        //    MeshFilter filter = chunkGO.GetComponent<MeshFilter>();
        //    filter.sharedMesh = newMesh;
        //    MeshRenderer vmeshrenderer = chunkGO.GetComponent<MeshRenderer>();
        //    vmeshrenderer.material = mSurfaceMaterial;
        //    // Asignación del MeshCollider (Actualización de físicas)
        //    MeshCollider collider = chunkGO.GetComponent<MeshCollider>();
        //    if (collider != null)
        //    {
        //        // Resetear sharedMesh a null es vital para que Unity dispare el 'Bake' de nuevo
        //        collider.sharedMesh = null;
        //        collider.sharedMesh = newMesh;
           }
    
    

    // =================================================
    // Utilities
    // =================================================
    int ChunkIndex(int x, int y, int z)
    {
        return x +
               mWorldChunkSize.x *
               (y + mWorldChunkSize.y * z);
    }

    // --- Debug: chunk wireframe (unchanged) ---
    void OnRenderObject()
    {
        //if (mChunks == null || mChunks.Length == 0)
        //    return;

        //Material lineMaterial = mSolidMaterial;
        //if (lineMaterial == null)
        //    return;

        //lineMaterial.SetPass(0);

        //GL.PushMatrix();
        //GL.MultMatrix(Matrix4x4.identity);

        //GL.Begin(GL.LINES);
        //GL.Color(Color.yellow);

        //for (int z = 0; z < mWorldChunkSize.z; z++)
        //    for (int y = 0; y < mWorldChunkSize.y; y++)
        //        for (int x = 0; x < mWorldChunkSize.x; x++)
        //        {
        //            Vector3 min = new Vector3(
        //                x * mChunkSize,
        //                y * mChunkSize,
        //                z * mChunkSize
        //            );

        //            Vector3 max = min + Vector3.one * mChunkSize;
        //            DrawWireCube(min, max);
        //        }

        //GL.End();
        //GL.PopMatrix();
    }

    void DrawWireCube(Vector3 min, Vector3 max)
    {
        //// Bottom
        //GL.Vertex(new Vector3(min.x, min.y, min.z));
        //GL.Vertex(new Vector3(max.x, min.y, min.z));

        //GL.Vertex(new Vector3(max.x, min.y, min.z));
        //GL.Vertex(new Vector3(max.x, min.y, max.z));

        //GL.Vertex(new Vector3(max.x, min.y, max.z));
        //GL.Vertex(new Vector3(min.x, min.y, max.z));

        //GL.Vertex(new Vector3(min.x, min.y, max.z));
        //GL.Vertex(new Vector3(min.x, min.y, min.z));

        //// Top
        //GL.Vertex(new Vector3(min.x, max.y, min.z));
        //GL.Vertex(new Vector3(max.x, max.y, min.z));

        //GL.Vertex(new Vector3(max.x, max.y, min.z));
        //GL.Vertex(new Vector3(max.x, max.y, max.z));

        //GL.Vertex(new Vector3(max.x, max.y, max.z));
        //GL.Vertex(new Vector3(min.x, max.y, max.z));

        //GL.Vertex(new Vector3(min.x, max.y, max.z));
        //GL.Vertex(new Vector3(min.x, max.y, min.z));

        //// Vertical edges
        //GL.Vertex(new Vector3(min.x, min.y, min.z));
        //GL.Vertex(new Vector3(min.x, max.y, min.z));

        //GL.Vertex(new Vector3(max.x, min.y, min.z));
        //GL.Vertex(new Vector3(max.x, max.y, min.z));

        //GL.Vertex(new Vector3(max.x, min.y, max.z));
        //GL.Vertex(new Vector3(max.x, max.y, max.z));

        //GL.Vertex(new Vector3(min.x, min.y, max.z));
        //GL.Vertex(new Vector3(min.x, max.y, max.z));
    }

    //void OnDrawGizmos()
    //{
    //    if (!Application.isPlaying)
    //        return;

    //    //if (false) DebugDrawDensityVolume();


    //}
    // 1. EL PROCESO PRINCIPAL
    void BuildSurfaceNets()
    {
        mMeshGenerator = new SurfaceNetsGenerator();
        Material[] matarray = GenerateMaterials(mChunks.Length);

        for (int i = 0; i < mChunks.Length; i++)
        {
            Chunk chunk = mChunks[i];

            // 1. Generamos la malla (debe devolver coordenadas LOCALES 0-16)
            Mesh chunkMesh = mMeshGenerator.Generate(chunk, mChunks, mWorldChunkSize);

            // 2. Gestión de Ciclo de Vida: Evitar duplicados
            if (chunk.mViewGO == null)
            {
                string goName = $"SurfaceNet_Chunk_{chunk.mCoord.x}_{chunk.mCoord.y}_{chunk.mCoord.z}";

                // Creamos el objeto solo si no existe en la referencia del Chunk
                chunk.mViewGO = new GameObject(goName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                chunk.mViewGO.transform.parent = mWorldRoot.transform;

               // chunk.mViewGO.transform.position = Vector3.zero;

                // LA POSICIÓN DEL OBJETO es la única que debe tener el mWorldOrigin
                chunk.mViewGO.transform.position = (Vector3)chunk.mWorldOrigin;
            }

            // 3. Asignación de componentes
            MeshFilter filter = chunk.mViewGO.GetComponent<MeshFilter>();
            filter.sharedMesh = chunkMesh;

            MeshRenderer renderer = chunk.mViewGO.GetComponent<MeshRenderer>();
            renderer.material = matarray[i];
            //renderer.material = mSurfaceMaterial;

            MeshCollider meshCollider = chunk.mViewGO.GetComponent<MeshCollider>();
            if (chunkMesh != null && chunkMesh.vertexCount > 0)
            {
                meshCollider.sharedMesh = null; // Forzar actualización de PhysX
                meshCollider.sharedMesh = chunkMesh;
                meshCollider.enabled = true;
            }
            else
            {
                meshCollider.enabled = false;
            }
        }
    }

    public Material[] GenerateMaterials(int n)
    {
        // 1. Inicializamos el array con el tamaño deseado
        Material[] materialArray = new Material[n];

        // 2. Buscamos el Shader base (Standard es el común en Built-in)
        // Si usas URP, cambia "Standard" por "Universal Render Pipeline/Lit"
        Shader baseShader = Shader.Find("Universal Render Pipeline/Lit");

        for (int i = 0; i < n; i++)
        {
            // 3. Creamos una nueva instancia del material en memoria
            Material newMat = new Material(baseShader);

            // 4. Le asignamos un nombre para identificarlo en el inspector
            newMat.name = "ProceduralMaterial_" + i;

            // 5. Generamos y aplicamos el color aleatorio
            newMat.color = new Color(Random.value, Random.value, Random.value);

            // 6. Lo guardamos en el array
            materialArray[i] = newMat;
        }

        return materialArray;
    }



}

