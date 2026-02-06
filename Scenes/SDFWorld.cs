//UNA NUEVA MODIFICACION

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

    //Chunk[] mChunks;
    Grid mGrid;
    Vector3Int mGridInUnits;
    Vector3Int mGridInChunks;
    GameObject mWorldRoot;
    Mesh mWorldMesh;

    MeshGenerator mMeshGenerator;
    SurfaceNetsGenerator mSurfaceNet = new SurfaceNetsGenerator();
    SurfaceNetsGeneratorQEF mSurfaceNetQEF = new SurfaceNetsGeneratorQEF();
    private float mDebugTimer = 10f;
    private bool ms = true;

    void Start()
    {
        mGridInUnits= new Vector3Int (512, 64, 512);
        mGridInChunks = new Vector3Int(
          Mathf.CeilToInt(mGridInUnits.x / (float)mChunkSize),
          Mathf.CeilToInt(mGridInUnits.y / (float)mChunkSize),
          Mathf.CeilToInt(mGridInUnits.z / (float)mChunkSize)
      );
        mGrid = new Grid(mGridInChunks, mChunkSize);
        BuildWorld();
        if (true) BuildSurfaceNets();
    }


    void Update()
    {
      
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

        if (mWorldRoot != null)
            Destroy(mWorldRoot);

        mWorldRoot = new GameObject("WorldRoot");
        mWorldRoot.transform.position = Vector3.zero;

        // -----------------------------
        // 3. Create chunks
        // -----------------------------
      

       


        // -----------------------------
        // 4. Phase 1: populate solids
        // -----------------------------
        foreach (Chunk chunk in mGrid.mChunks)
        {
            // Una sola llamada por chunk:
            // 1. Calcula el ruido Perlin 2D (optimizado por columna)
            // 2. Rellena las densidades (para el suavizado visual)
            // 3. Establece los bytes de solidez (para colisiones y l�gica)
            SDFGenerator.Sample(chunk);
        }

        //-----------------------------
        //6.Create views(temporal), semitransparent view test only purpose
        //-----------------------------
       //MeshGenerator generator =
       //    new VoxelMeshGenerator();

       // for (int i = 0; i < mChunks.Length; i++)
       // {
       //     ChunkSurfaceRender.Render(mChunks[i], generator, mWorldRoot.transform, mSolidMaterial);
       // }




    }
    public void ExecuteModification(Vector3 hitPoint, Vector3 hitNormal, byte newValue)
    {
        //---CONFIGURACIÓN DEL PINCEL ---
       float radius = 2.4f;   // Radio base
        float k = 0.3f;        // Factor de suavizado (SmoothMin)
        float noiseAmp = 0.18f; // Amplitud del ruido para romper la simetría (0.1-0.3 voxels)

        Vector3 targetPos = hitPoint - hitNormal * 0.2f;
        VoxelUtils.VoxelHit info = VoxelUtils.GetHitInfo(targetPos, mChunkSize, mGrid.mSizeInChunks);
        if (!info.isValid) return;

        Vector3Int g = info.globalVoxelPos;
        HashSet<int> chunksToRebuild = new HashSet<int>();

        //Rango 3 para permitir que el SmoothMin y el ruido respiren en los bordes
        int range = 3;

        for (int bz = g.z - range; bz <= g.z + range; bz++)
            for (int by = g.y - range; by <= g.y + range; by++)
                for (int bx = g.x - range; bx <= g.x + range; bx++)
                {
                    int cx = bx / mChunkSize, cy = by / mChunkSize, cz = bz / mChunkSize;
                    if (!VoxelUtils.IsInBounds(cx, cy, cz, mGrid.mSizeInChunks)) continue;

                    int cIdx = VoxelUtils.GetChunkIndex(cx, cy, cz, mGrid.mSizeInChunks);
                    Chunk nChunk = mGrid.mChunks[cIdx];
                    int lx = bx - (cx * mChunkSize), ly = by - (cy * mChunkSize), lz = bz - (cz * mChunkSize);

                    Vector3 p = new Vector3(bx, by, bz);
                    float d = Vector3.Distance(p, targetPos);

                    //1.ROMPER SIMETRÍA RADIAL(Clave para evitar facetas cíclicas)
                    // Introducimos una perturbación de alta frecuencia que desalinea los vértices
                    float noise = (Mathf.PerlinNoise(p.x * 1.5f, p.z * 1.5f) - 0.5f) * noiseAmp;
                    float tunnelField = Mathf.Clamp01((d + noise) / radius);

                    float currentD = nChunk.GetDensity(lx, ly, lz);
                    float finalD;

                    if (newValue == 0) // EXCAVAR
                    {
                        //2.SMOOTH MIN(Polinómico)
                        // Fusiona el campo del túnel con el terreno sin crear aristas de resta
                        float h = Mathf.Clamp01(0.5f + 0.5f * (tunnelField - currentD) / k);
                        finalD = Mathf.Lerp(tunnelField, currentD, h) - k * h * (1.0f - h);
                    }
                    else // CONSTRUIR
                    {
                        //Smooth Max para añadir material de forma esférica
                        float tunnelSolid = 1.0f - tunnelField;
                        float h = Mathf.Clamp01(0.5f + 0.5f * (currentD - tunnelSolid) / k);
                        finalD = Mathf.Lerp(currentD, tunnelSolid, h) + k * h * (1.0f - h);
                    }

                    nChunk.SetDensity(lx, ly, lz, Mathf.Clamp01(finalD));
                    nChunk.SetSolid(lx, ly, lz, finalD > 0.5f ? (byte)1 : (byte)0);

                    chunksToRebuild.Add(cIdx);
                }

        foreach (int index in chunksToRebuild) RebuildChunkGeometry(mGrid.mChunks[index]);
    }


    //public void ExecuteModification(Vector3 hitPoint, Vector3 hitNormal, byte newValue)
    //{
    //    // 1. AJUSTE DE PROFUNDIDAD (Bias)
    //    // Importante: Al excavar (newValue=0), usamos un bias más agresivo (0.5f)
    //    // para asegurar que el punto de edición entre en el siguiente voxel sólido
    //    // y no se quede "flotando" en el aire que acabas de crear.
    //    float bias = (newValue == 0) ? -0.5f : 0.1f;
    //    Vector3 targetPos = hitPoint + hitNormal * bias;

    //    // 2. OBTENER INFORMACIÓN DE MAPEO
    //    VoxelUtils.VoxelHit info = VoxelUtils.GetHitInfo(targetPos, mChunkSize, mWorldChunkSize);

    //    if (info.isValid)
    //    {
    //        // 3. MODIFICAR EL SÓLIDO CENTRAL
    //        // (Corregido: x, y, z en lugar de x, x, z)
    //        Chunk targetChunk = mChunks[info.chunkIndex];
    //        targetChunk.SetSolid(info.localPos.x, info.localPos.y, info.localPos.z, newValue);

    //        // 4. PROPAGACIÓN DE DENSIDADES (Kernel 3x3x3)
    //        // Recorremos los vecinos globales para que el cambio de sólido afecte 
    //        // a las densidades de alrededor y la malla se "hunda".
    //        Vector3Int g = info.globalVoxelPos;
    //        HashSet<int> chunksToRebuild = new HashSet<int>();

    //        for (int bz = g.z - 1; bz <= g.z + 1; bz++)
    //            for (int by = g.y - 1; by <= g.y + 1; by++)
    //                for (int bx = g.x - 1; bx <= g.x + 1; bx++)
    //                {
    //                    // Indirección para encontrar el chunk y la posición local de cada vecino
    //                    int cx = bx / mChunkSize;
    //                    int cy = by / mChunkSize;
    //                    int cz = bz / mChunkSize;

    //                    if (VoxelUtils.IsInBounds(cx, cy, cz, mWorldChunkSize))
    //                    {
    //                        int cIdx = VoxelUtils.GetChunkIndex(cx, cy, cz, mWorldChunkSize);
    //                        Chunk nChunk = mChunks[cIdx];

    //                        int nlx = bx - (cx * mChunkSize);
    //                        int nly = by - (cy * mChunkSize);
    //                        int nlz = bz - (cz * mChunkSize);

    //                        // Recalculamos la densidad de este voxel específico
    //                        float d = GetSmoothDensity(nChunk, nlx, nly, nlz);
    //                        nChunk.SetDensity(nlx, nly, nlz, d);

    //                        // Guardamos el índice para reconstruir el chunk (evita duplicados)
    //                        chunksToRebuild.Add(cIdx);
    //                    }
    //                }

    //        // 5. RECONSTRUCCIÓN DE MALLAS AFECTADAS
    //        foreach (int index in chunksToRebuild)
    //        {
    //            RebuildChunkGeometry(mChunks[index]);
    //        }
    //    }
    //}

    /// <summary>
    /// Regenera la representación visual y física de un chunk específico.
    /// </summary>
    private void RebuildChunkGeometry(Chunk pChunk)
    {





        // 5. Eliminar la referencia lógica del array (opcional, dependiendo de si quieres que deje de existir)


        //// Invocamos al generador de Surface Nets
        //// Pasamos la colección completa de chunks para que GetDensitySafe resuelva los bordes
        mMeshGenerator = mSurfaceNet;
        Mesh newMesh = mMeshGenerator.Generate(pChunk, mGrid.mChunks, mGrid.mSizeInChunks);

        // Localizar el GameObject siguiendo tu convención de nombres en BuildSurfaceNets
        string goName = $"SurfaceNet_Chunk_{pChunk.mCoord.x}_{pChunk.mCoord.y}_{pChunk.mCoord.z}";
        GameObject chunkGO = GameObject.Find(goName);

        if (chunkGO != null)
        {
            // Asignación de la nueva malla visual
            MeshFilter filter = chunkGO.GetComponent<MeshFilter>();
            filter.sharedMesh = newMesh;
            MeshRenderer vmeshrenderer = chunkGO.GetComponent<MeshRenderer>();
            vmeshrenderer.material = mSurfaceMaterial;
            // Asignación del MeshCollider (Actualización de físicas)
            MeshCollider collider = chunkGO.GetComponent<MeshCollider>();
            if (collider != null)
            {
                // Resetear sharedMesh a null es vital para que Unity dispare el 'Bake' de nuevo
                collider.sharedMesh = null;
                collider.sharedMesh = newMesh;
            }
        }

    }
    
    

    // =================================================
    // Utilities
    // =================================================
   

    // --- Debug: chunk wireframe (unchanged) ---
    void OnRenderObject()
    {

      

    }

    void DrawWireCube(Vector3 min, Vector3 max)
    {
       
    }

    // 1. EL PROCESO PRINCIPAL
    void BuildSurfaceNets()
    {
        
        mMeshGenerator = mSurfaceNetQEF;
        Material[] matarray = GenerateMaterials(mGrid.mChunks.Length);

        for (int i = 0; i < mGrid.mChunks.Length; i++)
        {
            Chunk chunk = mGrid.mChunks[i];

            // 1. Generamos la malla (debe devolver coordenadas LOCALES 0-16)
            Mesh chunkMesh = mMeshGenerator.Generate(chunk, mGrid.mChunks, mGrid.mSizeInChunks);

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

    public void DrawHierarchyDebug(GameObject parent, float size = 16f, Color pColor = default)
    {
        if (parent == null) return;

        // Si no se pasa color, usamos verde por defecto
        if (pColor == default) pColor = Color.green;

        // Recorremos todos los hijos directos del objeto padre
        foreach (Transform child in parent.transform)
        {
            // Tomamos la posición global (que ya incluye padre + local)
            Vector3 min = child.position;
            Vector3 max = min + new Vector3(size, size, size);

            // --- DIBUJO DE LAS 12 LÍNEAS DEL CUBO ---

            // Base (Y inferior)
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), pColor);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), pColor);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), pColor);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), pColor);

            // Techo (Y superior)
            Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), pColor);
            Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), pColor);
            Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), pColor);
            Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), pColor);

            // Columnas verticales
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), pColor);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), pColor);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), pColor);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), pColor);
        }

    }

    public float GetSmoothDensity(Chunk pChunk, int lx, int ly, int lz)
    {
        // 1. Convertimos el punto local solicitado a coordenadas globales de una vez
        int baseGX = pChunk.mWorldOrigin.x + lx;
        int baseGY = pChunk.mWorldOrigin.y + ly;
        int baseGZ = pChunk.mWorldOrigin.z + lz;

        float solidCount = 0;

        // 2. Iteramos el kernel 3x3x3 alrededor de esa posición global
        for (int oz = -1; oz <= 1; oz++)
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = baseGX + ox;
                    int gy = baseGY + oy;
                    int gz = baseGZ + oz;

                    // 3. Indirección transparente: calculamos a qué chunk pertenece este vecino
                    int cx = gx / mChunkSize;
                    int cy = gy / mChunkSize;
                    int cz = gz / mChunkSize;

                    // Verificamos si el vecino está dentro de los límites del mundo
                    if (VoxelUtils.IsInBounds(cx, cy, cz, mGrid.mSizeInChunks))
                    {
                        int cIdx = VoxelUtils.GetChunkIndex(cx, cy, cz, mGrid.mSizeInChunks);
                        Chunk targetChunk = mGrid.mChunks[cIdx];

                        // Calculamos la posición local dentro de ESE chunk específico
                        int nlx = gx - (cx * mChunkSize);
                        int nly = gy - (cy * mChunkSize);
                        int nlz = gz - (cz * mChunkSize);

                        // Usamos SafeIsSolid que ya maneja internamente el array de bytes
                        if (targetChunk.SafeIsSolid(nlx, nly, nlz))
                            solidCount++;
                    }
                }

        // 4. Retornamos la densidad (0.0 a 1.0)
        return solidCount / 27f;
    }





}

