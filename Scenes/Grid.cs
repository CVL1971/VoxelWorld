using UnityEngine;
using System.Collections.Generic;

public class Grid
{
    // 1. Declaramos la variable miembro inicializada a zero
    //private Vector3 mWorldPosition = Vector3.zero;
    public readonly Chunk[] mChunks;
    public readonly ushort[] mStatusGrid;
    public readonly Vector3Int mSizeInChunks;
    public readonly int mChunkSize;
    public GameObject mWorldRoot;
    public delegate void ChunkAction(Chunk chunk);
    private Vector3Int mCenterChunk;   // chunk donde está el jugador
    private Vector3Int mHalfSize;      // mitad del volumen local
    private Vector3Int mActiveMin;
    private Vector3Int mActiveMax;
    private int mXOffset;
    private int mYOffset;
    private int mZOffset;

    // MÁSCARAS DE BITS (Estructura de 16 bits)

    public const ushort BIT_SURFACE = 0x0001; // Bit 0
    public const ushort MASK_PROCESSING = 0x0002; // Bit 1
    public const ushort MASK_LOD_CURRENT = 0x000C; // Bits 2-3 (1100)
    public const ushort MASK_LOD_TARGET = 0x0030; // Bits 4-5 (110000)

    private const int SHIFT_LOD_CURRENT = 2;
    private const int SHIFT_LOD_TARGET = 4;

    public Vector3Int CenterChunk => mCenterChunk;
    public Vector3Int HalfSize => mHalfSize;

    private ChunkPipeline mPipeline;

    public void SetPipeline(ChunkPipeline pPipeline)
    {
        mPipeline = pPipeline;
    }

    public Grid(Vector3Int pSizeInChunks, int pChunkSize, Vector3 playerWorldPos)
    {
        mSizeInChunks = pSizeInChunks;
        mChunkSize = pChunkSize;

        mHalfSize = new Vector3Int(
       mSizeInChunks.x / 2,
       mSizeInChunks.y / 2,
       mSizeInChunks.z / 2
   );

        EmptyChunksInstances();

        mCenterChunk = new Vector3Int(
       Mathf.FloorToInt(playerWorldPos.x / mChunkSize),
       Mathf.FloorToInt(playerWorldPos.y / mChunkSize),
       Mathf.FloorToInt(playerWorldPos.z / mChunkSize)
   );
        mActiveMin = mCenterChunk - mHalfSize;
        mActiveMax = mCenterChunk + mHalfSize;
        mXOffset = 0;
        mYOffset = 0;
        mZOffset = 0;

        int minX = mCenterChunk.x - mSizeInChunks.x / 2;
        int minY = mCenterChunk.y - mSizeInChunks.y / 2;
        int minZ = mCenterChunk.z - mSizeInChunks.z / 2;

        int maxX = minX + mSizeInChunks.x - 1;
        int maxY = minY + mSizeInChunks.y - 1;
        int maxZ = minZ + mSizeInChunks.z - 1;

        int chunkCount =
           mSizeInChunks.x *
           mSizeInChunks.y *
           mSizeInChunks.z;

        mChunks = new Chunk[chunkCount];
        mStatusGrid = new ushort[chunkCount];

        for (int z = 0; z < mSizeInChunks.z; z++)
            for (int y = 0; y < mSizeInChunks.y; y++)
                for (int x = 0; x < mSizeInChunks.x; x++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    int index = ChunkIndex(x, y, z);
                    mChunks[index] = new Chunk(coord, mChunkSize, this);
                    Vector3Int initialGlobal = mCenterChunk + (coord - mHalfSize);
                    mChunks[index].mGlobalCoord = initialGlobal;
                    mStatusGrid[index] = 0; // Inicialmente todo a 0
                }

    }

    public bool TryGetNewPlayerChunk(Vector3 playerWorldPos, out Vector3Int newPlayerChunk)
    {
        newPlayerChunk = new Vector3Int(
            Mathf.FloorToInt(playerWorldPos.x / mChunkSize),
            Mathf.FloorToInt(playerWorldPos.y / mChunkSize),
            Mathf.FloorToInt(playerWorldPos.z / mChunkSize)
        );

        return newPlayerChunk != mCenterChunk;
    }

    public void UpdateStreamingX(Vector3Int newPlayerChunk)
    {
        int deltaX = newPlayerChunk.x - mCenterChunk.x;
        if (deltaX == 0) return;
        mCenterChunk = new Vector3Int(newPlayerChunk.x, mCenterChunk.y, mCenterChunk.z);

        Vector3Int newMin = mCenterChunk - mHalfSize;
        Vector3Int newMax = mCenterChunk + mHalfSize;

        int sx = mSizeInChunks.x;
        if (deltaX > 0)
        {
            for (int i = 0; i < deltaX; i++)
            {
                int incomingX = mActiveMax.x + 1;
                RecycleLayerX(mXOffset, incomingX);
                mXOffset = (mXOffset + 1) % sx;
                mActiveMin = new Vector3Int(mActiveMin.x + 1, mActiveMin.y, mActiveMin.z);
                mActiveMax = new Vector3Int(mActiveMax.x + 1, mActiveMax.y, mActiveMax.z);
            }
        }
        else
        {
            int absDelta = -deltaX;
            for (int i = 0; i < absDelta; i++)
            {
                mXOffset = (mXOffset - 1 + sx) % sx;
                int incomingX = mActiveMin.x - 1;
                RecycleLayerX(mXOffset, incomingX);
                mActiveMin = new Vector3Int(mActiveMin.x - 1, mActiveMin.y, mActiveMin.z);
                mActiveMax = new Vector3Int(mActiveMax.x - 1, mActiveMax.y, mActiveMax.z);
            }
        }

        mActiveMin = newMin;
        mActiveMax = newMax;
    }

    public void UpdateStreamingY(Vector3Int newPlayerChunk)
    {
        //int deltaY = newPlayerChunk.y - mCenterChunk.y;
        //if (deltaY == 0) return;
        //mCenterChunk = new Vector3Int(mCenterChunk.x, newPlayerChunk.y, mCenterChunk.z);
        //Vector3Int newMin = mCenterChunk - mHalfSize;
        //Vector3Int newMax = mCenterChunk + mHalfSize;
        //int sy = mSizeInChunks.y;
        //if (deltaY > 0)
        //{
        //    for (int i = 0; i < deltaY; i++)
        //    {
        //        int incomingY = mActiveMax.y + 1;
        //        RecycleLayerY(mYOffset, incomingY);
        //        mYOffset = (mYOffset + 1) % sy;
        //        mActiveMin = new Vector3Int(mActiveMin.x, mActiveMin.y + 1, mActiveMin.z);
        //        mActiveMax = new Vector3Int(mActiveMax.x, mActiveMax.y + 1, mActiveMax.z);
        //    }
        //}
        //else
        //{
        //    int absDelta = -deltaY;
        //    for (int i = 0; i < absDelta; i++)
        //    {
        //        mYOffset = (mYOffset - 1 + sy) % sy;
        //        int incomingY = mActiveMin.y - 1;
        //        RecycleLayerY(mYOffset, incomingY);
        //        mActiveMin = new Vector3Int(mActiveMin.x, mActiveMin.y - 1, mActiveMin.z);
        //        mActiveMax = new Vector3Int(mActiveMax.x, mActiveMax.y - 1, mActiveMax.z);
        //    }
        //}
        //mActiveMin = newMin;
        //mActiveMax = newMax;
    }

    public void UpdateStreamingZ(Vector3Int newPlayerChunk)
    {
        int deltaZ = newPlayerChunk.z - mCenterChunk.z;
        if (deltaZ == 0) return;
        mCenterChunk = new Vector3Int(mCenterChunk.x, mCenterChunk.y, newPlayerChunk.z);
        Vector3Int newMin = mCenterChunk - mHalfSize;
        Vector3Int newMax = mCenterChunk + mHalfSize;
        int sz = mSizeInChunks.z;
        if (deltaZ > 0)
        {
            for (int i = 0; i < deltaZ; i++)
            {
                int incomingZ = mActiveMax.z + 1;
                RecycleLayerZ(mZOffset, incomingZ);
                mZOffset = (mZOffset + 1) % sz;
                mActiveMin = new Vector3Int(mActiveMin.x, mActiveMin.y, mActiveMin.z + 1);
                mActiveMax = new Vector3Int(mActiveMax.x, mActiveMax.y, mActiveMax.z + 1);
            }
        }
        else
        {
            int absDelta = -deltaZ;
            for (int i = 0; i < absDelta; i++)
            {
                mZOffset = (mZOffset - 1 + sz) % sz;
                int incomingZ = mActiveMin.z - 1;
                RecycleLayerZ(mZOffset, incomingZ);
                mActiveMin = new Vector3Int(mActiveMin.x, mActiveMin.y, mActiveMin.z - 1);
                mActiveMax = new Vector3Int(mActiveMax.x, mActiveMax.y, mActiveMax.z - 1);
            }
        }
        mActiveMin = newMin;
        mActiveMax = newMax;
    }

    private void RecycleLayerX(int physicalColumnX, int incomingX)
    {
        ResetLayerX(physicalColumnX);
        for (int z = 0; z < mSizeInChunks.z; z++)
        {
            for (int y = 0; y < mSizeInChunks.y; y++)
            {
                Chunk chunk = mChunks[ChunkIndex(physicalColumnX, y, z)];
                Vector3Int newCoord = new Vector3Int(incomingX, chunk.mGlobalCoord.y, chunk.mGlobalCoord.z);
                ReassignChunk(chunk, newCoord);
            }
        }
    }

    private void RecycleLayerY(int physicalColumnY, int incomingY)
    {
        ResetLayerY(physicalColumnY);
        for (int z = 0; z < mSizeInChunks.z; z++)
        {
            for (int x = 0; x < mSizeInChunks.x; x++)
            {
                Chunk chunk = mChunks[ChunkIndex(x, physicalColumnY, z)];
                Vector3Int newCoord = new Vector3Int(chunk.mGlobalCoord.x, incomingY, chunk.mGlobalCoord.z);
                ReassignChunk(chunk, newCoord);
            }
        }
    }

    private void RecycleLayerZ(int physicalColumnZ, int incomingZ)
    {
        ResetLayerZ(physicalColumnZ);
        for (int y = 0; y < mSizeInChunks.y; y++)
        {
            for (int x = 0; x < mSizeInChunks.x; x++)
            {
                Chunk chunk = mChunks[ChunkIndex(x, y, physicalColumnZ)];
                Vector3Int newCoord = new Vector3Int(chunk.mGlobalCoord.x, chunk.mGlobalCoord.y, incomingZ);
                ReassignChunk(chunk, newCoord);
            }
        }
    }

    private void ReassignChunk(Chunk chunk, Vector3Int newGlobalCoord)
    {
        chunk.mGlobalCoord = newGlobalCoord;
        chunk.mGenerationId++;

        if (chunk.mViewGO != null)
        {
            chunk.ClearMesh();
            chunk.mViewGO.transform.position =
                (Vector3)(chunk.mGlobalCoord * VoxelUtils.UNIVERSAL_CHUNK_SIZE);
        }

        chunk.mIsEdited = false;
        chunk.ResetGenericBools();

        int index = chunk.mIndex;
        mStatusGrid[index] = 0;
        SetProcessing(index, true);

        if (mPipeline != null)
            mPipeline.ForceEnqueueDensity(chunk);
    }

    private void ResetLayerX(int physicalColumnX)
    {
        for (int z = 0; z < mSizeInChunks.z; z++)
        {
            for (int y = 0; y < mSizeInChunks.y; y++)
            {
                int index = ChunkIndex(physicalColumnX, y, z);
                ResetSlot(index);
            }
        }
    }

    private void ResetLayerY(int physicalColumnY)
    {
        for (int z = 0; z < mSizeInChunks.z; z++)
        {
            for (int x = 0; x < mSizeInChunks.x; x++)
            {
                int index = ChunkIndex(x, physicalColumnY, z);
                ResetSlot(index);
            }
        }
    }

    private void ResetLayerZ(int physicalColumnZ)
    {
        for (int y = 0; y < mSizeInChunks.y; y++)
        {
            for (int x = 0; x < mSizeInChunks.x; x++)
            {
                int index = ChunkIndex(x, y, physicalColumnZ);
                ResetSlot(index);
            }
        }
    }

    private void ResetSlot(int index)
    {
        Chunk chunk = mChunks[index];
        ResetStatusFlags(index);
        chunk.mIsEdited = false;
        chunk.ResetGenericBools();
        chunk.mGenerationId++;
    }

    private Vector3 mInternalWorldOrigin = Vector3.zero;

    public static int ResolutionToLodIndex(int pRes)
    {
        if (pRes >= 32) return 0;
        if (pRes >= 16) return 1;
        return 2;
    }

    public static string DebugState(Chunk chunk)
    {
        if (chunk == null)
            return "[ChunkDebug] NULL chunk";
        if (chunk.mGrid == null)
            return "[ChunkDebug] Grid NULL";

        ushort status = chunk.mGrid.mStatusGrid[chunk.mIndex];
        bool surface = (status & Grid.BIT_SURFACE) != 0;
        bool processing = (status & Grid.MASK_PROCESSING) != 0;
        int lodCurrent = (status & Grid.MASK_LOD_CURRENT) >> 2;
        int lodTarget = (status & Grid.MASK_LOD_TARGET) >> 4;

        return
            $"[ChunkDebug] " +
            $"Slot={chunk.mCoord} | " +
            $"Global={chunk.mGlobalCoord} | " +
            $"Index={chunk.mIndex} | " +
            $"GenId={chunk.mGenerationId} | " +
            $"Size={chunk.mSize} | " +
            $"Edited={chunk.mIsEdited} | " +
            $"Bool1={chunk.mBool1} | " +
            $"Bool2={chunk.mBool2} | " +
            $"Surface={surface} | " +
            $"Processing={processing} | " +
            $"LOD_Current={lodCurrent} | " +
            $"LOD_Target={lodTarget} | " +
            $"StatusRaw=0x{status:X4} | " +
            $"WorldOrigin={chunk.WorldOrigin}";
    }

    public int ChunkIndex(int x, int y, int z)
    {
        return x + mSizeInChunks.x * (y + mSizeInChunks.y * z);
    }

    public Chunk GetChunkByGlobalCoord(int cx, int cy, int cz)
    {
        if (cx < mActiveMin.x || cx > mActiveMax.x ||
            cy < mActiveMin.y || cy > mActiveMax.y ||
            cz < mActiveMin.z || cz > mActiveMax.z)
            return null;

        int lx = cx - mActiveMin.x;
        int ly = cy - mActiveMin.y;
        int lz = cz - mActiveMin.z;
        int sx = mSizeInChunks.x;
        int sy = mSizeInChunks.y;
        int sz = mSizeInChunks.z;
        int physicalX = (lx + mXOffset) % sx; if (physicalX < 0) physicalX += sx;
        int physicalY = (ly + mYOffset) % sy; if (physicalY < 0) physicalY += sy;
        int physicalZ = (lz + mZOffset) % sz; if (physicalZ < 0) physicalZ += sz;
        return mChunks[ChunkIndex(physicalX, physicalY, physicalZ)];
    }

    public void ApplyToChunks(ChunkAction pMethod)
    {
        foreach (Chunk chunk in mChunks) pMethod(chunk);
    }

    public void EmptyChunksInstances()
    {
        if (mWorldRoot == null)
        {
            mWorldRoot = new GameObject("WorldRoot");
            mWorldRoot.transform.position = Vector3.zero;
        }
        else
        {
            foreach (Transform child in mWorldRoot.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    public HashSet<int> ModifyWorld(VoxelBrush pBrush)
    {
        HashSet<int> vAffectedChunks = new HashSet<int>();
        float vRadius = pBrush.mRadius + pBrush.mK;
        Vector3Int vMin = new Vector3Int(
            Mathf.FloorToInt(pBrush.mCenter.x - vRadius),
            Mathf.FloorToInt(pBrush.mCenter.y - vRadius),
            Mathf.FloorToInt(pBrush.mCenter.z - vRadius)
        );
        Vector3Int vMax = new Vector3Int(
            Mathf.CeilToInt(pBrush.mCenter.x + vRadius),
            Mathf.CeilToInt(pBrush.mCenter.y + vRadius),
            Mathf.CeilToInt(pBrush.mCenter.z + vRadius)
        );

        for (int vz = vMin.z; vz <= vMax.z; vz++)
        {
            for (int vy = vMin.y; vy <= vMax.y; vy++)
            {
                for (int vx = vMin.x; vx <= vMax.x; vx++)
                {
                    Vector3 vPos = new Vector3(vx, vy, vz);
                    int vCx = Mathf.FloorToInt((float)vx / mChunkSize);
                    int vCy = Mathf.FloorToInt((float)vy / mChunkSize);
                    int vCz = Mathf.FloorToInt((float)vz / mChunkSize);

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                int targetCx = vCx + dx;
                                int targetCy = vCy + dy;
                                int targetCz = vCz + dz;

                                Chunk vTargetChunk = GetChunkByGlobalCoord(targetCx, targetCy, targetCz);
                                if (vTargetChunk == null) continue;

                                int lX = vx - (targetCx * mChunkSize);
                                int lY = vy - (targetCy * mChunkSize);
                                int lZ = vz - (targetCz * mChunkSize);

                                if (lX >= -1 && lX <= mChunkSize + 1 &&
                                    lY >= -1 && lY <= mChunkSize + 1 &&
                                    lZ >= -1 && lZ <= mChunkSize + 1)
                                {
                                    float vCurrentD = vTargetChunk.GetDensity(lX, lY, lZ);
                                    float vNewD = pBrush.CalculateDensity(vPos, vCurrentD);
                                    vTargetChunk.SetDensity(lX, lY, lZ, Mathf.Clamp01(vNewD));
                                    vTargetChunk.mIsEdited = true;
                                    vAffectedChunks.Add(vTargetChunk.mIndex);
                                }
                            }
                        }
                    }
                }
            }
        }
        return vAffectedChunks;
    }

    // ====================================================================================================
    // ALMACÉN DE ESTADOS (mStatusGrid) Y GESTIÓN DE FLAGS/LOD
    // ====================================================================================================

    public void MarkSurface(Chunk pChunk)
    {
        Surface(pChunk, pChunk.mBool1 && pChunk.mBool2);
    }

    public bool Surface(int pIndex)
    {
        return (mStatusGrid[pIndex] & BIT_SURFACE) != 0;
    }

    public void Surface(int pIndex, bool pValue)
    {
        byte vBitValue = System.Convert.ToByte(pValue);
        mStatusGrid[pIndex] = (ushort)((mStatusGrid[pIndex] & ~BIT_SURFACE) | vBitValue);
    }

    public void Surface(Chunk pChunk, bool pValue)
    {
        Vector3Int vCoords = pChunk.mCoord;
        byte vBitValue = System.Convert.ToByte(pValue);
        int pIndex = ChunkIndex(vCoords.x, vCoords.y, vCoords.z);
        mStatusGrid[pIndex] = (ushort)((mStatusGrid[pIndex] & ~BIT_SURFACE) | vBitValue);

        int lodIdx = ResolutionToLodIndex(pChunk.mSize);
        SetLod(pIndex, lodIdx);
    }

    public bool IsProcessing(int index)
    {
        return (mStatusGrid[index] & MASK_PROCESSING) != 0;
    }

    public void SetProcessing(int index, bool value)
    {
        if (value)
        {
            mStatusGrid[index] = (ushort)(mStatusGrid[index] | MASK_PROCESSING);
        }
        else
        {
            mStatusGrid[index] = (ushort)(mStatusGrid[index] & ~MASK_PROCESSING);
        }
    }

    public int GetLod(int index)
    {
        return (mStatusGrid[index] & MASK_LOD_CURRENT) >> SHIFT_LOD_CURRENT;
    }

    public void SetLod(int index, int lodValue)
    {
        ushort cleared = (ushort)(mStatusGrid[index] & ~MASK_LOD_CURRENT);
        mStatusGrid[index] = (ushort)(cleared | (ushort)(lodValue << SHIFT_LOD_CURRENT));
    }

    public int GetLodTarget(int index)
    {
        return (mStatusGrid[index] & MASK_LOD_TARGET) >> SHIFT_LOD_TARGET;
    }

    public void SetLodTarget(int index, int lodValue)
    {
        ushort cleared = (ushort)(mStatusGrid[index] & ~MASK_LOD_TARGET);
        mStatusGrid[index] = (ushort)(cleared | (ushort)(lodValue << SHIFT_LOD_TARGET));
    }

    // ====================================================================================================
    // REINICIALIZACIÓN DE FLAGS (ESTADOS) UTILIZANDO MÉTODOS DE ACCESO
    // ====================================================================================================

    public void ResetStatusFlags(int pIndex)
    {
        // Usamos los métodos existentes para asegurar que se respeten las máscaras y lógica definida
        Surface(pIndex, false);
        SetProcessing(pIndex, false);
        SetLod(pIndex, 0);
        SetLodTarget(pIndex, 0);
    }

    public void ResetStatusFlags(int x, int y, int z)
    {
        int vIndex = ChunkIndex(x, y, z);
        ResetStatusFlags(vIndex);
    }

    public void ResetStatusFlags(Vector3Int pLocalCoords)
    {
        int vIndex = ChunkIndex(pLocalCoords.x, pLocalCoords.y, pLocalCoords.z);
        ResetStatusFlags(vIndex);
    }
}