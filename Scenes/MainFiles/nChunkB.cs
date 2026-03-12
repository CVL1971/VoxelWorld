using UnityEngine;

public sealed class nChunkB : IChunk
{
    // =========================================================
    // CONSTANTES
    // =========================================================

    public const int BASE_RESOLUTION = 32;
    public const int PADDING = 1;
    public const float ISO_LEVEL_CONST = 0.5f;
    public const float BASE_CHUNK_SIZE = 32f;

    // =========================================================
    // COORDENADAS CLIPMAP
    // =========================================================

    public struct RingCoords
    {
        public int x;
        public int y;
        public int z;
        public byte Rlvl;
    }

    // =========================================================
    // CAMPOS
    // =========================================================

    public int mSize { get; private set; }

    public int mResolution;

    public Vector3Int mWorldOrigin;

    public RingCoords mRingCoords;

    public float[] mSamples;

    // Dummy cache para compatibilidad con el mesher antiguo
    public ArrayPool.DCache mDummyCache;

    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public nChunkB(int pRx, int pRy, int pRz, byte pRlvl)
    {
        mSize = BASE_RESOLUTION;

        mRingCoords.x = pRx;
        mRingCoords.y = pRy;
        mRingCoords.z = pRz;
        mRingCoords.Rlvl = pRlvl;

        mResolution = mSize + 1 + (PADDING * 2);

        int vTotalSamples = mResolution * mResolution * mResolution;

        mSamples = new float[vTotalSamples];

    }

    // =========================================================
    // PROPIEDADES INTERFAZ
    // =========================================================

    public Vector3Int WorldOrigin
    {
        get { return mWorldOrigin; }
    }

    public float ISO_LEVEL
    {
        get { return ISO_LEVEL_CONST; }
    }

    public ArrayPool.DCache DCache
    {
        get { return null; }
    }

    // =========================================================
    // TAMAÑO FÍSICO
    // =========================================================

    public float ChunkSize
    {
        get
        {
            return BASE_CHUNK_SIZE * (1 << mRingCoords.Rlvl);
        }
    }

    // =========================================================
    // ORIGEN
    // =========================================================

    public void SetWorldOrigin(Vector3Int pOrigin)
    {
        mWorldOrigin = pOrigin;
    }

    // =========================================================
    // INDEXADO
    // =========================================================

    public int IndexSample(int pX, int pY, int pZ)
    {
        int vRes = mResolution;

        return (pX + PADDING)
             + vRes * ((pY + PADDING)
             + vRes * (pZ + PADDING));
    }

    // =========================================================
    // DENSIDAD
    // =========================================================

    public float GetDensity(int pX, int pY, int pZ)
    {
        return mSamples[IndexSample(pX, pY, pZ)];
    }

    public void SetDensity(int pX, int pY, int pZ, float pDensity)
    {
        mSamples[IndexSample(pX, pY, pZ)] = pDensity;
    }

    // =========================================================
    // FLAGS (compatibilidad con pipeline antiguo)
    // =========================================================

    public bool mBool1 = false;
    public bool mBool2 = false;

    public void ResetFlags()
    {
        mBool1 = false;
        mBool2 = false;
    }

    // =========================================================
    // DEBUG VISUALIZATION
    // =========================================================

    public void DebugDraw()
    {
        SuperChunkDebugRenderer.RegisterChunk(this);
    }

    // =========================================================
    // DEBUG DATA
    // =========================================================

    public Vector3 DebugCenter
    {
        get
        {
            return (Vector3)mWorldOrigin + Vector3.one * (ChunkSize * 0.5f);
        }
    }

    public float DebugHalfSize
    {
        get
        {
            return ChunkSize * 0.5f;
        }
    }

    public float DebugStep
    {
        get
        {
            return ChunkSize / BASE_RESOLUTION;
        }
    }

    public int SampleMin
    {
        get { return -1; }
    }

    public int SampleMax
    {
        get { return mSize + 1; }
    }

    public int SampleResolution
    {
        get { return SampleMax - SampleMin + 1; }
    }
}