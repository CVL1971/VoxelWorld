using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

public static class VoxelArrayPool
{
    
    // El "Libro de Cuentas" �nico
    private static readonly ConcurrentStack<(float[], float[], float[])> mPool =
         new ConcurrentStack<(float[], float[], float[])>();

    static int mLod0Length;
    static int mLod1Length;
    static int mLod2Length;

    static VoxelArrayPool()
    {
        VoxelUtils.EnsureAwake = true;
        mLod0Length = (int)Mathf.Pow(VoxelUtils.LOD_DATA[0] + 3, 3);
    mLod1Length = (int)Mathf.Pow(VoxelUtils.LOD_DATA[4] + 3, 3);
    mLod2Length = (int)Mathf.Pow(VoxelUtils.LOD_DATA[8] + 3, 3);
   
    }


    public static (float[], float[], float[]) Get()
    {
        (float[], float[], float[]) vLodSamples;

        if (!mPool.TryPop(out vLodSamples))
        {
            return (new float[mLod0Length], new float[mLod1Length], new float[mLod2Length]);
        }

        else return vLodSamples;
     
    }

    public static void Return(float[] pLod0, float[] pLod1, float[] pLod2)
    {
        mPool.Push( (pLod0,pLod1,pLod2));

    }
}