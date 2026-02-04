using UnityEngine;
using System.Collections.Generic;

public class SDFWorld : MonoBehaviour
{
    [Header("SCHEMATIC input")]
    string mSchematicPath = @"E:\data_terrain_1.schem";

    [Header("World settings")]
    [SerializeField] int mChunkSize = 16;

    [Header("Rendering")]
    [SerializeField] Material mSolidMaterial;

    [SerializeField]
    Material mSurfaceMaterial;

    [SerializeField] bool mDebugTint = true;

    Chunk[] mChunks;
    Vector3Int mWorldChunkSize;
    GameObject mWorldRoot;
    Mesh mWorldMesh;

    MeshGenerator mMeshGenerator;

    int mDebugY = 10;

    void Start()
    {
        //BuildWorldFromSchematic();
        //if (true) BuildSurfaceNets();
    }
   

    void BuildWorldFromSchematic()
    {
        // -----------------------------
        // 1. Load schematic
        // -----------------------------
        //SchematicReader.VolumeData model =
        //    SchematicReader.Load(mSchematicPath);
        SchematicReader.VolumeData model =
            SchematicReader.Load(mSchematicPath, new Vector3Int(120, 40, 80), 256);


        if (mWorldRoot != null)
            Destroy(mWorldRoot);

        mWorldRoot = new GameObject("WorldRoot");
        mWorldRoot.transform.position = Vector3.zero;

        // -----------------------------
        // 3. Create chunks
        // -----------------------------
        mWorldChunkSize = new Vector3Int(
            Mathf.CeilToInt(model.sizeX / (float)mChunkSize),
            Mathf.CeilToInt(model.sizeY / (float)mChunkSize),
            Mathf.CeilToInt(model.sizeZ / (float)mChunkSize)
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
        foreach (var v in model.voxels)
        {
            int cx = v.x / mChunkSize;
            int cy = v.y / mChunkSize;
            int cz = v.z / mChunkSize;

            int lx = v.x % mChunkSize;
            int ly = v.y % mChunkSize;
            int lz = v.z % mChunkSize;

            Chunk chunk =
                mChunks[ChunkIndex(cx, cy, cz)];

            chunk.SetSolid(lx, ly, lz, 1);
        }

        // -----------------------------
        // 5. Phase 2: compute densities
        // -----------------------------
        ComputeAllDensities();

        // -----------------------------
        // 6. Create views (temporal)
        // -----------------------------
        MeshGenerator generator =
            new VoxelMeshGenerator();

        for (int i = 0; i < mChunks.Length; i++)
        {
            ChunkSurfaceRender.Render(mChunks[i], generator, mWorldRoot.transform, mSolidMaterial);
        }

    }

    // =================================================
    // Density computation (global bootstrap)
    // =================================================
    void ComputeAllDensities()
    {
        for (int cz = 0; cz < mWorldChunkSize.z; cz++)
            for (int cy = 0; cy < mWorldChunkSize.y; cy++)
                for (int cx = 0; cx < mWorldChunkSize.x; cx++)
                {
                    Chunk chunk =
                        mChunks[ChunkIndex(cx, cy, cz)];

                    ComputeChunkDensity(chunk);
                }
    }

    void ComputeChunkDensity(Chunk pChunk)
    {
        int size = pChunk.mSize;

        for (int z = 0; z <= size; z++)
            for (int y = 0; y <= size; y++)
                for (int x = 0; x <= size; x++)
                {
                    float d =
                        WeightedNeighborDensity.Sample(
                            pChunk,
                            mChunks,
                            mWorldChunkSize,
                            x, y, z
                        );

                    pChunk.SetDensity(x, y, z, d);
                }
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
    //void OnRenderObject()
    //{
    //    if (mChunks == null || mChunks.Length == 0)
    //        return;

    //    Material lineMaterial = mSolidMaterial;
    //    if (lineMaterial == null)
    //        return;

    //    lineMaterial.SetPass(0);

    //    GL.PushMatrix();
    //    GL.MultMatrix(Matrix4x4.identity);

    //    GL.Begin(GL.LINES);
    //    GL.Color(Color.yellow);

    //    //for (int z = 0; z < mWorldChunkSize.z; z++)
    //    //    for (int y = 0; y < mWorldChunkSize.y; y++)
    //    //        for (int x = 0; x < mWorldChunkSize.x; x++)
    //    //        {
    //    //            Vector3 min = new Vector3(
    //    //                x * mChunkSize,
    //    //                y * mChunkSize,
    //    //                z * mChunkSize
    //    //            );

    //    //            Vector3 max = min + Vector3.one * mChunkSize;
    //    //            DrawWireCube(min, max);
    //    //        }

    //    GL.End();
    //    GL.PopMatrix();
    //}

    //void DrawWireCube(Vector3 min, Vector3 max)
    //{
    //    // Bottom
    //    //GL.Vertex(new Vector3(min.x, min.y, min.z));
    //    //GL.Vertex(new Vector3(max.x, min.y, min.z));

    //    //GL.Vertex(new Vector3(max.x, min.y, min.z));
    //    //GL.Vertex(new Vector3(max.x, min.y, max.z));

    //    //GL.Vertex(new Vector3(max.x, min.y, max.z));
    //    //GL.Vertex(new Vector3(min.x, min.y, max.z));

    //    //GL.Vertex(new Vector3(min.x, min.y, max.z));
    //    //GL.Vertex(new Vector3(min.x, min.y, min.z));

    //    //// Top
    //    //GL.Vertex(new Vector3(min.x, max.y, min.z));
    //    //GL.Vertex(new Vector3(max.x, max.y, min.z));

    //    //GL.Vertex(new Vector3(max.x, max.y, min.z));
    //    //GL.Vertex(new Vector3(max.x, max.y, max.z));

    //    //GL.Vertex(new Vector3(max.x, max.y, max.z));
    //    //GL.Vertex(new Vector3(min.x, max.y, max.z));

    //    //GL.Vertex(new Vector3(min.x, max.y, max.z));
    //    //GL.Vertex(new Vector3(min.x, max.y, min.z));

    //    //// Vertical edges
    //    //GL.Vertex(new Vector3(min.x, min.y, min.z));
    //    //GL.Vertex(new Vector3(min.x, max.y, min.z));

    //    //GL.Vertex(new Vector3(max.x, min.y, min.z));
    //    //GL.Vertex(new Vector3(max.x, max.y, min.z));

    //    //GL.Vertex(new Vector3(max.x, min.y, max.z));
    //    //GL.Vertex(new Vector3(max.x, max.y, max.z));

    //    //GL.Vertex(new Vector3(min.x, min.y, max.z));
    //    //GL.Vertex(new Vector3(min.x, max.y, max.z));
    //}

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

        for (int i = 0; i < mChunks.Length; i++)
        {
            // 1. Generamos la malla específica de este chunk usando la información global
            Mesh chunkMesh = mMeshGenerator.Generate(mChunks[i], mChunks, mWorldChunkSize);

            // 2. Usamos una versión adaptada de tu ChunkSurfaceRender para no repetir código
            // O lo hacemos manualmente aquí:
            string goName = $"SurfaceNet_Chunk_{mChunks[i].mCoord.x}_{mChunks[i].mCoord.y}_{mChunks[i].mCoord.z}";
            GameObject go = GameObject.Find(goName) ?? new GameObject(goName, typeof(MeshFilter), typeof(MeshRenderer));

            go.transform.parent = mWorldRoot?.transform; // Para mantener la jerarquía limpia
            go.transform.position = (Vector3)mChunks[i].mWorldOrigin; // Posicionamos el chunk en su sitio

            go.GetComponent<MeshFilter>().mesh = chunkMesh;
            go.GetComponent<MeshRenderer>().material = mSurfaceMaterial;
        }

    }

}




//using UnityEngine;
//using System.Collections.Generic;

//public class World : MonoBehaviour
//{
//    [Header("SCHEMATIC input")]
//    [SerializeField]
//    string mSchematicPath;

//    [Header("Chunk settings")]
//    [SerializeField]
//    int mChunkSize = 16;

//    [Header("Rendering")]
//    [SerializeField]
//    Material mSolidMaterial;

//    [SerializeField]
//    Material mSurfaceMaterial;

//    [SerializeField]
//    bool mDebugTint = true;

//    GameObject mWorldRoot;

//    void Start()
//    {
//        GenerateWorld();
//        //DebugBuildMeshXY();
//    }

//    void GenerateWorld()
//    {
//        // -----------------------------
//        // 1. Generar terreno (DATOS)
//        // -----------------------------
//        Terrain terrain =
//    Terrain.GenerateTerrainFromSchem(
//        mSchematicPath,
//        mChunkSize,
//       128// volumen pequeño
//    );

//        GeneralData.mTerrain = terrain;
//        TestRandomGlobalToLocal();



//        // 1️⃣ Asignar generador de densidad
//        terrain.mDensityGenerator =
//            new WeightedNeighborDensity(
//                1.0f,   // faces
//                0.7f,   // edges
//                0.5f    // corners
//            );



//        // 2️⃣ Calcular densidades
//        terrain.ComputeAllDensities();

//        Debug.Log(
//    $"Density generator: {(terrain.mDensityGenerator == null ? "NULL" : "OK")}"
//);

//        // -----------------------------
//        // 2. Crear root visual
//        // -----------------------------
//        if (mWorldRoot != null)
//            Destroy(mWorldRoot);

//        mWorldRoot = new GameObject("WorldRoot");
//        mWorldRoot.transform.position = Vector3.zero;

//        // -----------------------------
//        // 3. Crear vistas por chunk
//        // -----------------------------
//        MeshGenerator generator =
//            new VoxelMeshGenerator();

//        for (int i = 0; i < terrain.mChunks.Length; i++)
//        {
//            terrain.mChunks[i].CreateView(
//                mWorldRoot.transform,
//                mSolidMaterial,
//                mDebugTint,
//                generator
//            );
//        }


//    }

//    // =================================================
//    // DEBUG VISUAL CENTRALIZADO
//    // =================================================

//    void OnDrawGizmos()
//    {
//        if (GeneralData.mTerrain == null)
//            return;

//        DrawOrigin();
//        DrawSchematicBounds();
//        DrawSolidVoxelSanity();
//        if (false) DebugDrawDensityVolume();
//        if (false)
//            DebugDrawCellVertices();
//        if (false)
//            DebugDrawActiveCells();
//        if (false)
//            DebugDrawConnectivityXY();
//        if (false)
//            DebugDrawConnectivityXZ();

//        if (false)
//            DebugDrawConnectivityYZ();
//        if (false)
//            DebugDrawQuadsXY();
//        if (GeneralData.mTerrain == null)
//            return;

//        if (true) DrawChunkWireframe();

//        if (false)
//            DebugDrawActiveCells();
//    }

//    void OnRenderObject()
//    {

//    }

//    // =================================================
//    // DEBUG: ORIGEN
//    // =================================================

//    void DrawOrigin()
//    {
//        Gizmos.color = Color.white;
//        Gizmos.DrawSphere(Vector3.zero, 0.5f);

//        Gizmos.color = Color.red;
//        Gizmos.DrawLine(Vector3.zero, Vector3.right * 5);

//        Gizmos.color = Color.green;
//        Gizmos.DrawLine(Vector3.zero, Vector3.up * 5);

//        Gizmos.color = Color.blue;
//        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 5);
//    }

//    // =================================================
//    // DEBUG: BOUNDS DEL .SCHEM
//    // =================================================

//    void DrawSchematicBounds()
//    {
//        Vector3 min = Vector3.zero;

//        Vector3 max = new Vector3(
//            GeneralData.mVolumeSizeX,
//            GeneralData.mVolumeSizeY,
//            GeneralData.mVolumeSizeZ
//        );

//        Gizmos.color = Color.cyan;
//        Gizmos.DrawWireCube(
//            (min + max) * 0.5f,
//            max - min
//        );
//    }

//    // =================================================
//    // DEBUG: SANITY DE VOXELS SÓLIDOS
//    // =================================================

//    void DrawSolidVoxelSanity()
//    {
//        Terrain terrain = GeneralData.mTerrain;

//        // Solo muestreamos UN chunk para sanity
//        Chunk chunk = terrain.GetChunk(0, 0, 0);
//        if (chunk == null)
//            return;

//        int size = chunk.mSize;

//        Gizmos.color = Color.magenta;

//        for (int z = 0; z < size; z++)
//            for (int y = 0; y < size; y++)
//                for (int x = 0; x < size; x++)
//                {
//                    if (!chunk.IsSolid(x, y, z))
//                        continue;

//                    Vector3 pos = new Vector3(
//                        chunk.mWorldOrigin.x + x + 0.5f,
//                        chunk.mWorldOrigin.y + y + 0.5f,
//                        chunk.mWorldOrigin.z + z + 0.5f
//                    );

//                    Gizmos.DrawSphere(pos, 0.15f);
//                    return; // con ver UNO basta
//                }
//    }

//    // =================================================
//    // DEBUG: WIREFRAME DE CHUNKS (EXACTO AL ORIGINAL)
//    // =================================================

//    void DrawChunkWireframe()
//    {
//        Material lineMaterial = mSolidMaterial;
//        if (lineMaterial == null)
//            return;

//        lineMaterial.SetPass(0);

//        GL.PushMatrix();
//        GL.MultMatrix(Matrix4x4.identity);

//        GL.Begin(GL.LINES);
//        GL.Color(Color.yellow);

//        int chunkSize = GeneralData.mChunkSize;

//        int cxCount = GeneralData.mChunkCountX;
//        int cyCount = GeneralData.mChunkCountY;
//        int czCount = GeneralData.mChunkCountZ;

//        for (int z = 0; z < czCount; z++)
//            for (int y = 0; y < cyCount; y++)
//                for (int x = 0; x < cxCount; x++)
//                {
//                    Vector3 min = new Vector3(
//                        x * chunkSize,
//                        y * chunkSize,
//                        z * chunkSize
//                    );

//                    Vector3 max = min + Vector3.one * chunkSize;

//                    DrawWireCube(min, max);
//                }

//        GL.End();
//        GL.PopMatrix();
//    }

//    void DrawWireCube(Vector3 min, Vector3 max)
//    {
//        // Bottom
//        GL.Vertex(new Vector3(min.x, min.y, min.z));
//        GL.Vertex(new Vector3(max.x, min.y, min.z));

//        GL.Vertex(new Vector3(max.x, min.y, min.z));
//        GL.Vertex(new Vector3(max.x, min.y, max.z));

//        GL.Vertex(new Vector3(max.x, min.y, max.z));
//        GL.Vertex(new Vector3(min.x, min.y, max.z));

//        GL.Vertex(new Vector3(min.x, min.y, max.z));
//        GL.Vertex(new Vector3(min.x, min.y, min.z));

//        // Top
//        GL.Vertex(new Vector3(min.x, max.y, min.z));
//        GL.Vertex(new Vector3(max.x, max.y, min.z));

//        GL.Vertex(new Vector3(max.x, max.y, min.z));
//        GL.Vertex(new Vector3(max.x, max.y, max.z));

//        GL.Vertex(new Vector3(max.x, max.y, max.z));
//        GL.Vertex(new Vector3(min.x, max.y, max.z));

//        GL.Vertex(new Vector3(min.x, max.y, max.z));
//        GL.Vertex(new Vector3(min.x, max.y, min.z));

//        // Vertical edges
//        GL.Vertex(new Vector3(min.x, min.y, min.z));
//        GL.Vertex(new Vector3(min.x, max.y, min.z));

//        GL.Vertex(new Vector3(max.x, min.y, min.z));
//        GL.Vertex(new Vector3(max.x, max.y, min.z));

//        GL.Vertex(new Vector3(max.x, min.y, max.z));
//        GL.Vertex(new Vector3(max.x, max.y, max.z));

//        GL.Vertex(new Vector3(min.x, min.y, max.z));
//        GL.Vertex(new Vector3(min.x, max.y, max.z));
//    }

//    void DebugDrawDensityVolume()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        // -------- CONFIG LOCAL --------
//        float radius = 0.06f;
//        // -----------------------------

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;
//            VoxelData[] voxels = chunk.mVoxels;

//            int slice = size * size;

//            for (int index = 0; index < voxels.Length; index++)
//            {
//                float d = voxels[index].density;
//                if (d <= 0.0f)
//                    continue;

//                // -------- índice → coordenadas --------
//                int x = index % size;
//                int y = (index / size) % size;
//                int z = index / slice;

//                // -------- coordenadas → mundo --------
//                Vector3 pos = new Vector3(
//                    chunk.mWorldOrigin.x + x,
//                    chunk.mWorldOrigin.y + y,
//                    chunk.mWorldOrigin.z + z
//                );

//                Gizmos.color = DensityDebugColor(d);
//                Gizmos.DrawSphere(pos, radius);
//            }
//        }
//    }




//    Color DensityDebugColor(float d)
//    {
//        if (d < 0.5f)
//            return Color.Lerp(Color.blue, Color.green, d * 2.0f);

//        return Color.Lerp(Color.green, Color.red, (d - 0.5f) * 2.0f);
//    }

//    void DebugDrawCellVertices()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;
//        float radius = 0.08f;

//        // Offsets de las 8 esquinas de una celda
//        Vector3Int[] vertexOffsets =
//        {
//        new Vector3Int(0,0,0),
//        new Vector3Int(1,0,0),
//        new Vector3Int(1,1,0),
//        new Vector3Int(0,1,0),
//        new Vector3Int(0,0,1),
//        new Vector3Int(1,0,1),
//        new Vector3Int(1,1,1),
//        new Vector3Int(0,1,1),
//    };

//        // Pares de esquinas que forman aristas
//        int[,] edgePairs =
//        {
//        {0,1},{1,2},{2,3},{3,0},
//        {4,5},{5,6},{6,7},{7,4},
//        {0,4},{1,5},{2,6},{3,7}
//    };

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            // Iteramos CELDAS
//            for (int z = 0; z < size - 1; z++)
//                for (int y = 0; y < size - 1; y++)
//                    for (int x = 0; x < size - 1; x++)
//                    {
//                        float[] d = new float[8];
//                        Vector3[] p = new Vector3[8];

//                        // Esquinas de la celda
//                        for (int i = 0; i < 8; i++)
//                        {
//                            Vector3Int o = vertexOffsets[i];

//                            int vx = x + o.x;
//                            int vy = y + o.y;
//                            int vz = z + o.z;

//                            d[i] = chunk.DensityAt(vx, vy, vz);

//                            p[i] = new Vector3(
//                                chunk.mWorldOrigin.x + vx,
//                                chunk.mWorldOrigin.y + vy,
//                                chunk.mWorldOrigin.z + vz
//                            );
//                        }

//                        bool inside = false;
//                        bool outside = false;

//                        for (int i = 0; i < 8; i++)
//                        {
//                            if (d[i] >= iso) inside = true;
//                            else outside = true;
//                        }

//                        // Si no hay cruce, no hay vértice
//                        if (!(inside && outside))
//                            continue;

//                        Vector3 sum = Vector3.zero;
//                        int count = 0;

//                        // Intersecciones en aristas
//                        for (int e = 0; e < 12; e++)
//                        {
//                            int a = edgePairs[e, 0];
//                            int b = edgePairs[e, 1];

//                            float da = d[a];
//                            float db = d[b];

//                            if ((da < iso && db >= iso) ||
//                                (da >= iso && db < iso))
//                            {
//                                float t = (iso - da) / (db - da);
//                                sum += Vector3.Lerp(p[a], p[b], t);
//                                count++;
//                            }
//                        }

//                        if (count == 0)
//                            continue;

//                        Vector3 v = sum / count;

//                        Gizmos.color = Color.white;
//                        Gizmos.DrawSphere(v, radius);
//                    }
//        }
//    }

//    // =================================================
//    // DEBUG — FASE 3: CELDAS ACTIVAS
//    // =================================================
//    void DebugDrawActiveCells()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            for (int z = 0; z < size - 1; z++)
//                for (int y = 0; y < size - 1; y++)
//                    for (int x = 0; x < size - 1; x++)
//                    {
//                        bool inside = false;
//                        bool outside = false;

//                        for (int dz = 0; dz <= 1; dz++)
//                            for (int dy = 0; dy <= 1; dy++)
//                                for (int dx = 0; dx <= 1; dx++)
//                                {
//                                    float d =
//                                        chunk.DensityAt(
//                                            x + dx,
//                                            y + dy,
//                                            z + dz
//                                        );

//                                    if (d >= iso)
//                                        inside = true;
//                                    else
//                                        outside = true;
//                                }

//                        if (!(inside && outside))
//                            continue;

//                        // Centro de la celda
//                        Vector3 pos = new Vector3(
//                            chunk.mWorldOrigin.x + x + 0.5f,
//                            chunk.mWorldOrigin.y + y + 0.5f,
//                            chunk.mWorldOrigin.z + z + 0.5f
//                        );

//                        Gizmos.color = Color.cyan;
//                        Gizmos.DrawWireCube(pos, Vector3.one * 0.9f);
//                    }
//        }
//    }

//    // =================================================
//    // DEBUG — FASE 4: CONECTIVIDAD (PLANO XY)
//    // =================================================
//    void DebugDrawConnectivityXY()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            for (int z = 0; z < size - 2; z++)
//                for (int y = 0; y < size - 1; y++)
//                    for (int x = 0; x < size - 1; x++)
//                    {
//                        bool a = IsCellActive(chunk, x, y, z, iso);
//                        bool b = IsCellActive(chunk, x, y, z + 1, iso);

//                        if (!(a && b))
//                            continue;

//                        // centro del plano compartido
//                        Vector3 pos = new Vector3(
//                            chunk.mWorldOrigin.x + x + 0.5f,
//                            chunk.mWorldOrigin.y + y + 0.5f,
//                            chunk.mWorldOrigin.z + z + 1.0f
//                        );

//                        Gizmos.color = Color.green;
//                        Gizmos.DrawWireCube(pos, new Vector3(0.9f, 0.9f, 0.01f));
//                    }
//        }
//    }

//    bool IsCellActive(Chunk chunk, int x, int y, int z, float iso)
//    {
//        bool inside = false;
//        bool outside = false;

//        for (int dz = 0; dz <= 1; dz++)
//            for (int dy = 0; dy <= 1; dy++)
//                for (int dx = 0; dx <= 1; dx++)
//                {
//                    float d = chunk.DensityAt(x + dx, y + dy, z + dz);

//                    if (d >= iso) inside = true;
//                    else outside = true;
//                }

//        return inside && outside;
//    }

//    // =================================================
//    // DEBUG — FASE 4: CONECTIVIDAD (PLANO XZ)
//    // =================================================
//    void DebugDrawConnectivityXZ()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            // celdas vecinas en Y
//            for (int z = 0; z < size - 1; z++)
//                for (int y = 0; y < size - 2; y++)
//                    for (int x = 0; x < size - 1; x++)
//                    {
//                        bool a = IsCellActive(chunk, x, y, z, iso);
//                        bool b = IsCellActive(chunk, x, y + 1, z, iso);

//                        if (!(a && b))
//                            continue;

//                        // centro del plano compartido (XZ)
//                        Vector3 pos = new Vector3(
//                            chunk.mWorldOrigin.x + x + 0.5f,
//                            chunk.mWorldOrigin.y + y + 1.0f,
//                            chunk.mWorldOrigin.z + z + 0.5f
//                        );

//                        Gizmos.color = Color.green;
//                        Gizmos.DrawWireCube(
//                            pos,
//                            new Vector3(0.9f, 0.01f, 0.9f)
//                        );
//                    }
//        }
//    }

//    // =================================================
//    // DEBUG — FASE 4: CONECTIVIDAD (PLANO YZ)
//    // =================================================
//    void DebugDrawConnectivityYZ()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            // Celdas vecinas en X
//            for (int z = 0; z < size - 1; z++)
//                for (int y = 0; y < size - 1; y++)
//                    for (int x = 0; x < size - 2; x++)
//                    {
//                        bool a = IsCellActive(chunk, x, y, z, iso);
//                        bool b = IsCellActive(chunk, x + 1, y, z, iso);

//                        if (!(a && b))
//                            continue;

//                        // Centro del plano compartido (YZ)
//                        Vector3 pos = new Vector3(
//                            chunk.mWorldOrigin.x + x + 1.0f,
//                            chunk.mWorldOrigin.y + y + 0.5f,
//                            chunk.mWorldOrigin.z + z + 0.5f
//                        );

//                        Gizmos.color = Color.cyan;
//                        Gizmos.DrawWireCube(
//                            pos,
//                            new Vector3(0.01f, 0.9f, 0.9f)
//                        );
//                    }
//        }
//    }

//    // =================================================
//    // DEBUG — FASE 5: QUADS (PLANO XY)
//    // =================================================
//    void DebugDrawQuadsXY()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            for (int z = 0; z < size - 2; z++)
//                for (int y = 0; y < size - 1; y++)
//                    for (int x = 0; x < size - 1; x++)
//                    {
//                        bool a = IsCellActive(chunk, x, y, z, iso);
//                        bool b = IsCellActive(chunk, x, y, z + 1, iso);

//                        if (!(a && b))
//                            continue;

//                        Vector3 p0 = new Vector3(
//                            chunk.mWorldOrigin.x + x,
//                            chunk.mWorldOrigin.y + y,
//                            chunk.mWorldOrigin.z + z + 1
//                        );

//                        Vector3 p1 = p0 + Vector3.right;
//                        Vector3 p2 = p0 + Vector3.right + Vector3.up;
//                        Vector3 p3 = p0 + Vector3.up;

//                        Gizmos.color = new Color(0, 1, 0, 0.4f);
//                        Gizmos.DrawLine(p0, p1);
//                        Gizmos.DrawLine(p1, p2);
//                        Gizmos.DrawLine(p2, p3);
//                        Gizmos.DrawLine(p3, p0);
//                    }
//        }
//    }

//    //=================================================
//    //DEBUG — FASE 6: TRIANGULACIÓN XY(SIN OPTIMIZAR)
//    //=================================================

//    void DebugBuildMeshXY()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        const float iso = 0.5f;

//        List<Vector3> vertices = new List<Vector3>();
//        List<int> triangles = new List<int>();

//        Chunk[] chunks = terrain.mChunks;

//        for (int ci = 0; ci < chunks.Length; ci++)
//        {
//            Chunk chunk = chunks[ci];
//            if (chunk == null)
//                continue;

//            int size = chunk.mSize;

//            for (int z = 0; z < size - 2; z++)
//                for (int y = 0; y < size - 1; y++)
//                    for (int x = 0; x < size - 1; x++)
//                    {
//                        bool a = IsCellActive(chunk, x, y, z, iso);
//                        bool b = IsCellActive(chunk, x, y, z + 1, iso);

//                        if (!(a && b))
//                            continue;

//                        // =================================================
//                        // QUAD XY en z+1
//                        // =================================================

//                        Vector3 v0 = new Vector3(
//                            chunk.mWorldOrigin.x + x,
//                            chunk.mWorldOrigin.y + y,
//                            chunk.mWorldOrigin.z + z + 1
//                        );

//                        Vector3 v1 = v0 + Vector3.right;
//                        Vector3 v2 = v0 + Vector3.right + Vector3.up;
//                        Vector3 v3 = v0 + Vector3.up;

//                        int baseIndex = vertices.Count;

//                        vertices.Add(v0);
//                        vertices.Add(v1);
//                        vertices.Add(v2);
//                        vertices.Add(v3);

//                        // Triángulos (orden FIJO)
//                        // Triángulo 1
//                        triangles.Add(baseIndex + 0);
//                        triangles.Add(baseIndex + 2);
//                        triangles.Add(baseIndex + 1);

//                        // Triángulo 2
//                        triangles.Add(baseIndex + 0);
//                        triangles.Add(baseIndex + 3);
//                        triangles.Add(baseIndex + 2);
//                    }
//        }

//        // =================================================
//        // Construcción del mesh de debug
//        // =================================================
//        Mesh mesh = new Mesh();
//        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
//        mesh.SetVertices(vertices);
//        mesh.SetTriangles(triangles, 0);
//        mesh.RecalculateBounds();
//        mesh.RecalculateNormals(); // solo para ver algo

//        // =================================================
//        // Mostrarlo
//        // =================================================
//        GameObject go = GameObject.Find("DebugMesh_XY");
//        if (go == null)
//        {
//            go = new GameObject("DebugMesh_XY");
//            go.AddComponent<MeshFilter>();
//            go.AddComponent<MeshRenderer>();
//        }

//        go.GetComponent<MeshFilter>().mesh = mesh;
//        go.GetComponent<MeshRenderer>().material = mSurfaceMaterial;
//    }

//    public void RebuildView()
//    {
//        Terrain terrain = GeneralData.mTerrain;

//        for (int i = 0; i < terrain.mChunks.Length; i++)
//        {
//            terrain.mChunks[i].DestroyView();
//            terrain.mChunks[i].CreateView(
//                mWorldRoot.transform,
//                mSolidMaterial,
//                mDebugTint,
//                new VoxelMeshGenerator()
//            );
//        }

//    }

//    void TestRandomGlobalToLocal()
//    {
//        Terrain terrain = GeneralData.mTerrain;
//        if (terrain == null)
//            return;

//        int DX = GeneralData.mVolumeSizeX;
//        int DY = GeneralData.mVolumeSizeY;
//        int DZ = GeneralData.mVolumeSizeZ;

//        // 1. Coordenada GLOBAL aleatoria
//        int gx = Random.Range(0, DX);
//        int gy = Random.Range(0, DY);
//        int gz = Random.Range(0, DZ);

//        Debug.Log(
//            $"[TEST GLOBAL->LOCAL] Global = ({gx}, {gy}, {gz})"
//        );

//        // 2. Transformación bajo test
//        var addr =
//            VoxelAddressing.GlobalToChunkAddress(
//                new VoxelAddressing.GlobalCoord
//                {
//                    X = gx,
//                    Y = gy,
//                    Z = gz
//                },
//                GeneralData.mChunkSize,
//                GeneralData.mChunkSize,
//                GeneralData.mChunkSize
//            );

//        Debug.Log(
//            $"[TEST GLOBAL->LOCAL] Chunk = ({addr.Chunk.X}, {addr.Chunk.Y}, {addr.Chunk.Z}) " +
//            $"Local = ({addr.Local.X}, {addr.Local.Y}, {addr.Local.Z})"
//        );

//        // 3. Seleccionar el chunk
//        int chunkIndex =
//            addr.Chunk.X +
//            GeneralData.mChunkCountX *
//            (addr.Chunk.Y + GeneralData.mChunkCountY * addr.Chunk.Z);

//        Chunk targetChunk = terrain.mChunks[chunkIndex];

//        // --- LOG ANTES ---
//        Debug.Log(
//            $"[TEST] BEFORE toggle -> solid = " +
//            targetChunk.IsSolid(
//                addr.Local.X,
//                addr.Local.Y,
//                addr.Local.Z
//            )
//        );

//        // 4. Mutación LOCAL (clave)
//        targetChunk.ToggleSolidLocal(
//            addr.Local.X,
//            addr.Local.Y,
//            addr.Local.Z
//        );

//        // --- LOG DESPUÉS ---
//        Debug.Log(
//            $"[TEST] AFTER toggle -> solid = " +
//            targetChunk.IsSolid(
//                addr.Local.X,
//                addr.Local.Y,
//                addr.Local.Z
//            )
//        );


//        if(mWorldRoot == null)
//        {
//            mWorldRoot = new GameObject("WorldRoot");
//        }

//        // 5. Refrescar vista (usa lo que ya tienes)
//        for (int i = 0; i < terrain.mChunks.Length; i++)
//        {
//            terrain.mChunks[i].DestroyView();
//            terrain.mChunks[i].CreateView(
//                mWorldRoot.transform,
//                mSolidMaterial,
//                mDebugTint,
//                new VoxelMeshGenerator()
//            );
//        }
//    }




//}




