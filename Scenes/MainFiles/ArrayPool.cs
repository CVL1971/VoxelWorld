using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public static class ArrayPool
{
    public class DCache
    {
        public DCache(int pLength0, int pLength1, int pLength2, int pRefs)
        {
            mSample0 = new float[pLength0];
            mSample1 = new float[pLength1]; 
            mSample2 = new float[pLength2]; 
            mRefs = pRefs;
        }

        public readonly float[] mSample0;
        public readonly float[] mSample1;
        public readonly float[] mSample2;
        public int mRefs;
    }

    // El "Libro de Cuentas" unico
    private static readonly ConcurrentQueue<DCache> mPool = new ConcurrentQueue<DCache>();

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


    public static DCache Get()
    {
        DCache vLodSamples;
        int count = mPool.Count;

        while (count-- >0 && mPool.TryDequeue(out vLodSamples))
        {
            if (Interlocked.CompareExchange(ref vLodSamples.mRefs, 1, 0) == 0)
                return vLodSamples;

            mPool.Enqueue(vLodSamples);
        }

        return (new DCache(mLod0Length, mLod1Length, mLod2Length, 1));

    }

    public static void Return(DCache pLodSamples)
    {
        mPool.Enqueue(pLodSamples);
    }
}