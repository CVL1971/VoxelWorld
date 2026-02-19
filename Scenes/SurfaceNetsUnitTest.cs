using UnityEngine;

//public class SurfaceNetsUnitTest : MonoBehaviour
//{
//    public Material mat;

//    void Start()
//    {
//        int size = 32;

//        // Creamos un chunk falso

//        Chunk chunk = new Chunk(Vector3Int.zero, size);

//        FillPlane(chunk, 16f); // plano en altura 16

//        SurfaceNetsGeneratorTest mesher = new SurfaceNetsGeneratorTest();
//        MeshData data = mesher.Generate(chunk);


//        // visualizar
//        GameObject go = new GameObject("TEST_MESH", typeof(MeshFilter), typeof(MeshRenderer));
//        Mesh mesh = new Mesh();

//        mesh.SetVertices(data.vertices);
//        mesh.SetTriangles(data.triangles, 0);
//        mesh.SetNormals(data.normals);

//        go.GetComponent<MeshFilter>().mesh = mesh;
//        go.GetComponent<MeshRenderer>().material = mat;
//    }

//    // ----------------------------
//    // CAMPO ESCALAR SINTÉTICO
//    // ----------------------------
//    void FillPlane(Chunk chunk, float planeHeight)
//    {
//        int N = chunk.mSize;
//        int p = N + 2;

//        // dominio lógico [-1..N]
//        for (int z = -1; z <= N; z++)
//            for (int y = -1; y <= N; y++)
//                for (int x = -1; x <= N; x++)
//                {
//                    float density = y - planeHeight;
//                    chunk.SetDensity(x, y, z, density);
//                }
//    }
//}



public class SurfaceNetsTwoChunkTest : MonoBehaviour
{
    public Material mat;

    void Start()
    {
        int size = 32;

        // Creamos dos chunks vecinos
        Chunk A = new Chunk(new Vector3Int(0, 0, 0), size);
        Chunk B = new Chunk(new Vector3Int(1, 0, 0), size);

        FillWorldPlane(A, 16f);
        FillWorldPlane(B, 16f);

        SurfaceNetsGeneratorTest mesher = new SurfaceNetsGeneratorTest();

        CreateGO(A, mesher.Generate(A), "Chunk_A");
        CreateGO(B, mesher.Generate(B), "Chunk_B");
    }

    // -----------------------------------
    // FUNCIÓN MUNDIAL CONTINUA
    // -----------------------------------
    void FillWorldPlane(Chunk chunk, float planeHeight)
    {
        int N = chunk.mSize;
        float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / N;

        for (int z = -1; z <= N; z++)
            for (int y = -1; y <= N; y++)
                for (int x = -1; x <= N; x++)
                {
                    float worldY = chunk.mWorldOrigin.y + (y * vStep);
                    float density = worldY - planeHeight;

                    chunk.SetDensity(x, y, z, density);
                }
    }
    // -----------------------------------
    // VISUALIZACIÓN
    // -----------------------------------
    void CreateGO(Chunk chunk, MeshData data, string name)
    {
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));

        Mesh mesh = new Mesh();
        mesh.SetVertices(data.vertices);
        mesh.SetTriangles(data.triangles, 0);
        mesh.SetNormals(data.normals);

        go.GetComponent<MeshFilter>().mesh = mesh;
        go.GetComponent<MeshRenderer>().material = mat;

        go.transform.position = chunk.mWorldOrigin;
    }
}
