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

    // MÁSCARAS DE BITS (Estructura de 16 bits)

    public const ushort BIT_SURFACE = 0x0001; // Bit 0
    public const ushort MASK_PROCESSING = 0x0002; // Bit 1
    public const ushort MASK_LOD_CURRENT = 0x000C; // Bits 2-3 (1100)
    public const ushort MASK_LOD_TARGET = 0x0030; // Bits 4-5 (110000)

    private const int SHIFT_LOD_CURRENT = 2;
    private const int SHIFT_LOD_TARGET = 4;

    public Vector3Int CenterChunk => mCenterChunk;
    public Vector3Int HalfSize => mHalfSize;

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

        int minX = mCenterChunk.x - mSizeInChunks.x / 2;
        int minY = mCenterChunk.y - mSizeInChunks.y / 2;
        int minZ = mCenterChunk.z - mSizeInChunks.z / 2;

        int maxX = minX + mSizeInChunks.x - 1;
        int maxY = minY + mSizeInChunks.y - 1;
        int maxZ = minZ + mSizeInChunks.z - 1;

        Debug.Log($"Dominio X: {minX} → {maxX}");
        Debug.Log($"Dominio Y: {minY} → {maxY}");
        Debug.Log($"Dominio Z: {minZ} → {maxZ}");
        Debug.Log($"PlayerChunk: {mCenterChunk}");

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

    public void UpdateStreamingX(Vector3Int newPlayerChunk, DensitySamplerQueueAsync samplerQueue)
    {
        int deltaX = newPlayerChunk.x - mCenterChunk.x;

        if (deltaX == 0)
            return;

        Vector3Int oldCenter = mCenterChunk;
        mCenterChunk = newPlayerChunk;

        Vector3Int newMin = mCenterChunk - mHalfSize;
        Vector3Int newMax = mCenterChunk + mHalfSize;

        if (deltaX > 0)
        {
            int outgoingX = mActiveMin.x;
            int incomingX = newMax.x;

            RecycleLayerX(outgoingX, incomingX, samplerQueue);
        }
        else
        {
            int outgoingX = mActiveMax.x;
            int incomingX = newMin.x;

            RecycleLayerX(outgoingX, incomingX, samplerQueue);
        }

        mActiveMin = newMin;
        mActiveMax = newMax;
    }

    private void RecycleLayerX(int outgoingX, int incomingX, DensitySamplerQueueAsync samplerQueue)
    {
        for (int i = 0; i < mChunks.Length; i++)
        {
            Chunk chunk = mChunks[i];

            if (chunk.mGlobalCoord.x == outgoingX)
            {
                Vector3Int oldCoord = chunk.mGlobalCoord;

                Vector3Int newCoord = new Vector3Int(
                    incomingX,
                    oldCoord.y,
                    oldCoord.z
                );

                ReassignChunk(chunk, newCoord, samplerQueue);
            }
        }
    }

    private void ReassignChunk(Chunk chunk, Vector3Int newGlobalCoord, DensitySamplerQueueAsync samplerQueue)
    {
        chunk.mGlobalCoord = newGlobalCoord;

        // Invalidar generación anterior
        chunk.mGenerationId++;

        if (chunk.mViewGO != null)
        {
            chunk.mViewGO.transform.position =
                (Vector3)(chunk.mGlobalCoord * VoxelUtils.UNIVERSAL_CHUNK_SIZE);
        }

        // Reset flags lógicos
        chunk.mIsEdited = false;
        chunk.ResetGenericBools();

        int index = chunk.mIndex;

        // Reset estado compacto
        mStatusGrid[index] = 0;

        // Marcar como procesando
        SetProcessing(index, true);

        // Lanzar nuevo sampleo
        samplerQueue.Enqueue(chunk);

        Debug.Log(DebugState(chunk));
    }

    // 1. Declaramos la variable miembro inicializada a zero
    private Vector3 mInternalWorldOrigin = Vector3.zero;

    // 2. Propiedad que setea los valores y devuelve la variable sin usar 'new'
    //public Vector3 WorldPosition
    //{
    //    get
    //    {
    //        mWorldPosition.x = mXOffset * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
    //        mWorldPosition.y = mYOffset * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
    //        mWorldPosition.z = mZOffset * VoxelUtils.UNIVERSAL_CHUNK_SIZE;

    //        return mWorldPosition;
    //    }
    //}

    public static int ResolutionToLodIndex(int pRes)
    {
        // Según tu tabla LOD_DATA: 32 -> Index 0, 16 -> Index 1, 8 -> Index 2
        if (pRes >= 32) return 0;
        if (pRes >= 16) return 1;
        return 2;
    }

    public void MarkSurface(Chunk pChunk)
    {
        Surface(pChunk, pChunk.mBool1 && pChunk.mBool2);
    }

    public bool Surface(int pIndex)
    {
        return (mStatusGrid[pIndex] & BIT_SURFACE) != 0;


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

        // 1. Obtener el índice de LOD basado en el mSize actual del chunk
        int lodIdx = ResolutionToLodIndex(pChunk.mSize);

        // 2. Guardar en los bits 2-3 (MASK_LOD_CURRENT)
        // Usamos el método SetLod que ya tienes en Grid.cs
        SetLod(pIndex, lodIdx);
    }

    public int ChunkIndex(int x, int y, int z)
    {
        return x + mSizeInChunks.x * (y + mSizeInChunks.y * z);
    }

    public void ApplyToChunks(ChunkAction pMethod)
    {

        // 1. Calcula el ruido Perlin 2D, densidades y solidez
        foreach (Chunk chunk in mChunks) pMethod(chunk);

    }


    public void EmptyChunksInstances()
    {

        if (mWorldRoot == null)
        {
            // Solo se crea si no existe
            mWorldRoot = new GameObject("WorldRoot");
            mWorldRoot.transform.position = Vector3.zero;
        }
        else
        {
            // Si ya existe, eliminamos a los hijos para dejarlo limpio
            foreach (Transform child in mWorldRoot.transform)
            {
                Object.Destroy(child.gameObject);
            }
        }

    }


    //public HashSet<int> ModifyWorld(VoxelBrush pBrush)
    //    {
    //        HashSet<int> vAffectedChunks = new HashSet<int>();

    //        // 1. Calculamos el área de influencia en voxeles globales
    //        float vRadius = pBrush.mRadius + pBrush.mK;
    //        Vector3Int vMin = new Vector3Int(
    //            Mathf.FloorToInt(pBrush.mCenter.x - vRadius),
    //            Mathf.FloorToInt(pBrush.mCenter.y - vRadius),
    //            Mathf.FloorToInt(pBrush.mCenter.z - vRadius)
    //        );
    //        Vector3Int vMax = new Vector3Int(
    //            Mathf.CeilToInt(pBrush.mCenter.x + vRadius),
    //            Mathf.CeilToInt(pBrush.mCenter.y + vRadius),
    //            Mathf.CeilToInt(pBrush.mCenter.z + vRadius)
    //        );

    //        // 2. Iteramos solo sobre los voxeles del pincel (Rápido)
    //        for (int vz = vMin.z; vz <= vMax.z; vz++)
    //            for (int vy = vMin.y; vy <= vMax.y; vy++)
    //                for (int vx = vMin.x; vx <= vMax.x; vx++)
    //                {
    //                    // 3. Conversión de Global a Chunk usando vx, vy, vz (CORREGIDO)
    //                    int vCx = vx / mChunkSize;
    //                    int vCy = vy / mChunkSize;
    //                    int vCz = vz / mChunkSize; // Ahora usa vz correctamente

    //                    if (!VoxelUtils.IsInBounds(vCx, vCy, vCz, mSizeInChunks)) continue;

    //                    int vCIdx = VoxelUtils.GetChunkIndex(vCx, vCy, vCz, mSizeInChunks);
    //                    Chunk vChunk = mChunks[vCIdx];

    //                    // MARCADO DE EDICIÓN: El usuario ha tocado este chunk
    //                    vChunk.mIsEdited = true;

    //                    // 4. Conversión a coordenadas locales del Chunk
    //                    int vLx = vx - (vCx * mChunkSize);
    //                    int vLy = vy - (vCy * mChunkSize);
    //                    int vLz = vz - (vCz * mChunkSize);

    //                    // 5. Aplicación del pincel
    //                    Vector3 vPos = new Vector3(vx, vy, vz);
    //                    float vCurrentD = vChunk.GetDensity(vLx, vLy, vLz);
    //                    float vNewD = pBrush.CalculateDensity(vPos, vCurrentD);

    //                    vChunk.SetDensity(vLx, vLy, vLz, Mathf.Clamp01(vNewD));


    //                    vAffectedChunks.Add(vCIdx);
    //                }

    //        return vAffectedChunks;
    //    }

    public HashSet<int> ModifyWorld(VoxelBrush pBrush)
    {
        HashSet<int> vAffectedChunks = new HashSet<int>();

        // 1. Calculamos el área de influencia en voxeles globales, 
        // incluyendo el radio de suavizado (k)
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

        // 2. Iteramos sobre cada voxel global que la brocha toca
        for (int vz = vMin.z; vz <= vMax.z; vz++)
        {
            for (int vy = vMin.y; vy <= vMax.y; vy++)
            {
                for (int vx = vMin.x; vx <= vMax.x; vx++)
                {
                    Vector3 vPos = new Vector3(vx, vy, vz);

                    // 3. Calculamos las coordenadas del chunk "central" para este voxel
                    int vCx = Mathf.FloorToInt((float)vx / mChunkSize);
                    int vCy = Mathf.FloorToInt((float)vy / mChunkSize);
                    int vCz = Mathf.FloorToInt((float)vz / mChunkSize);

                    // 4. PROPAGACIÓN A VECINOS: 
                    // Un voxel global puede ser el "padding" de hasta 26 vecinos.
                    // Iteramos en un bloque de 3x3x3 chunks alrededor del central.
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                int targetCx = vCx + dx;
                                int targetCy = vCy + dy;
                                int targetCz = vCz + dz;

                                // Verificamos si el chunk vecino existe dentro de los límites del mundo
                                if (!VoxelUtils.IsInBounds(targetCx, targetCy, targetCz, mSizeInChunks))
                                    continue;

                                int vCIdx = VoxelUtils.GetChunkIndex(targetCx, targetCy, targetCz, mSizeInChunks);
                                Chunk vTargetChunk = mChunks[vCIdx];

                                // 5. Convertimos la posición global a la local de este chunk específico
                                int lX = vx - (targetCx * mChunkSize);
                                int lY = vy - (targetCy * mChunkSize);
                                int lZ = vz - (targetCz * mChunkSize);

                                // 6. Verificamos si cae en el rango extendido del generador (-1 a size+1)
                                // Esto asegura que actualizamos la geometría de costura (seams).
                                if (lX >= -1 && lX <= mChunkSize + 1 &&
                                    lY >= -1 && lY <= mChunkSize + 1 &&
                                    lZ >= -1 && lZ <= mChunkSize + 1)
                                {
                                    // Obtenemos densidad actual, calculamos la nueva y aplicamos
                                    float vCurrentD = vTargetChunk.GetDensity(lX, lY, lZ);
                                    float vNewD = pBrush.CalculateDensity(vPos, vCurrentD);

                                    // Aplicamos clamping 0-1 para mantener consistencia con ISO_THRESHOLD 0.5
                                    vTargetChunk.SetDensity(lX, lY, lZ, Mathf.Clamp01(vNewD));

                                    // Marcamos el chunk para que el sistema sepa que debe regenerar la malla
                                    vTargetChunk.mIsEdited = true;
                                    vAffectedChunks.Add(vCIdx);
                                }
                            }
                        }
                    }
                }
            }
        }

        return vAffectedChunks;
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
        // Limpiamos los bits antiguos y ponemos los nuevos
        ushort cleared = (ushort)(mStatusGrid[index] & ~MASK_LOD_CURRENT);
        mStatusGrid[index] = (ushort)(cleared | (ushort)(lodValue << SHIFT_LOD_CURRENT));
    }

    public int GetLodTarget(int index)
    {   //0,1,2 lodtarget 3 Lodcomplete
        return (mStatusGrid[index] & MASK_LOD_TARGET) >> SHIFT_LOD_TARGET;
    }

    public void SetLodTarget(int index, int lodValue)
    {    //0,1,2 lodtarget 3 Lodcomplete
        ushort cleared = (ushort)(mStatusGrid[index] & ~MASK_LOD_TARGET);
        mStatusGrid[index] = (ushort)(cleared | (ushort)(lodValue << SHIFT_LOD_TARGET));
    }


}