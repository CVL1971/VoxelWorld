using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;


public static class GameObjectPool
{

    private static readonly ConcurrentStack<GameObject> mPool =
         new ConcurrentStack<GameObject>();

    private static Transform mWorldRoot;
    private static Material mSurfaceMaterial;
    

    public static void Initialize(Transform pWorldRoot, Material pSurfaceMaterial)
    {
        mWorldRoot = pWorldRoot;
        mSurfaceMaterial = pSurfaceMaterial;
        ArrayPool.mEnsureAwake = true;
    }

    public static GameObject CreateView(Transform worldRoot, Material surfaceMaterial, Chunk vChunk)
    {
        GameObject vGameObject;
        vGameObject = new GameObject("Chunk_" + vChunk.mCoord, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        vGameObject.transform.SetParent(worldRoot, false);
        vGameObject.transform.position = (Vector3)vChunk.WorldOrigin;
        vGameObject.GetComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
       

        return vGameObject;
    }

    public static GameObject RecycleView(GameObject pGameObject)
    {
        pGameObject.name = "GO_Recicled";
        pGameObject.SetActive(false);
        pGameObject.GetComponent<MeshCollider>().sharedMesh = null;
        Mesh mesh = pGameObject.GetComponent<MeshFilter>().sharedMesh;
        if (mesh != null)
        {
            mesh.Clear();
        }

        return pGameObject;
    }

    public static GameObject Update(Chunk pChunk, GameObject pGameObject)
    {
        pGameObject.name = ("Chunk_" + pChunk.mCoord);
        pGameObject.transform.position = (Vector3)pChunk.WorldOrigin;
        pGameObject.SetActive(true);

        return pGameObject;
    }


    public static GameObject Get(Chunk vChunk)
    {
        GameObject vGameObject;

        if (!mPool.TryPop(out vGameObject)) return CreateView(mWorldRoot, mSurfaceMaterial, vChunk);
        else return Update(vChunk,vGameObject);
   
    }

    public static void Return(Chunk pChunk)
    {
        mPool.Push(RecycleView(pChunk.mViewGO));
    }

   

   
}
