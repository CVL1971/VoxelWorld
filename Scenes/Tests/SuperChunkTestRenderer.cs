using UnityEngine;

public class SuperChunkTestRenderer : MonoBehaviour
{
    public Material mMaterial;

    void Start()
    {
        // =====================================================
        // 1. CREAR SUPERCHUNK
        // =====================================================

        nChunkB vChunk = new nChunkB(0, 0, 0, 4);

        vChunk.SetWorldOrigin(Vector3Int.zero);

        // =====================================================
        // 2. SAMPLEAR DENSIDADES
        // =====================================================

        SuperChunkSampler.Sample(vChunk);

        // =====================================================
        // 3. GENERAR MALLA
        // =====================================================

        SurfaceNetsGeneratorQEF3caches vMesher = new SurfaceNetsGeneratorQEF3caches();

        MeshData vMeshData = vMesher.Generate(vChunk, null, Vector3Int.zero);

        // =====================================================
        // 4. CONVERTIR A MESH UNITY
        // =====================================================

        Mesh vMesh = new Mesh();

        vMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        vMesh.SetVertices(vMeshData.vertices);
        vMesh.SetTriangles(vMeshData.triangles, 0);
        vMesh.SetNormals(vMeshData.normals);

        // =====================================================
        // 5. CREAR GAMEOBJECT
        // =====================================================

        GameObject vGO = new GameObject("SuperChunk");

        vGO.transform.position = vChunk.WorldOrigin;

        MeshFilter vMF = vGO.AddComponent<MeshFilter>();
        MeshRenderer vMR = vGO.AddComponent<MeshRenderer>();

        vMF.sharedMesh = vMesh;


        if (mMaterial != null)
            vMR.sharedMaterial = mMaterial;
        else
            vMR.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        vChunk.DebugDraw();
    }
}
