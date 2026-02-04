using UnityEngine;

public static class ChunkSurfaceRender
{
    public static void Render(
        Chunk pChunk,
        MeshGenerator pGenerator,
        Transform pParent,
        Material pMaterial
    )
    {
        // =========================
        // Crear vista si no existe
        // =========================

        if (pChunk.mViewGO == null)
        {
            GameObject go = new GameObject($"Chunk_{pChunk.mCoord}");
            go.transform.parent = pParent;
            go.transform.position = Vector3.zero;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = pMaterial;
            bool odd = ((pChunk.mCoord.x + pChunk. mCoord.y + pChunk.mCoord.z) & 1) == 0;
            //Material mat = Object.Instantiate(pMaterial);
            //mat.color *= odd ? 0.3f : 1.3f;
            //mr.material = mat;

            pChunk.mViewGO = go;
        }

        // =========================
        // Renderizar malla
        // =========================

        MeshFilter filter = pChunk.mViewGO.GetComponent<MeshFilter>();
        Mesh mesh = pGenerator.Generate(pChunk);
        //filter.mesh = mesh;
    }


}



