using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public static class ArrayPool
{
    
    // El "Libro de Cuentas" unico
    private static readonly ConcurrentQueue<(float[] pLod0, float[] pLod1, float[] pLod2, int[] pRefs)> mPool =
         new ConcurrentQueue<(float[], float[], float[], int[])>();

    static readonly int mLod0Length;
    static readonly int mLod1Length;
    static readonly int mLod2Length;

    public static bool mEnsureAwake = false;

    static ArrayPool()
    {
        VoxelUtils.EnsureAwake = true;
        mLod0Length = (int)Mathf.Pow(VoxelUtils.LOD_DATA[0] + 3, 3);
        mLod1Length = (int)Mathf.Pow(VoxelUtils.LOD_DATA[4] + 3, 3);
        mLod2Length = (int)Mathf.Pow(VoxelUtils.LOD_DATA[8] + 3, 3);
   
    }


    public static (float[] pLod0, float[] pLod1, float[] pLod2, int[] pRefs) Get()
    {
        (float[], float[], float[], int[] pRefs) vLodSamples;
        int count = mPool.Count;

        while (count-- >0 && mPool.TryDequeue(out vLodSamples))
        {
            if (Interlocked.CompareExchange(ref vLodSamples.pRefs[0], 1, 0) == 0)
                return vLodSamples;

            mPool.Enqueue(vLodSamples);
        }

        return (new float[mLod0Length], new float[mLod1Length], new float[mLod2Length], new int[1] { 1 });
    }

    public static void Return((float[] pLod0, float[] pLod1, float[] pLod2, int[] pRefs) pLodSamples)
    {
        mPool.Enqueue(pLodSamples);
    }
}