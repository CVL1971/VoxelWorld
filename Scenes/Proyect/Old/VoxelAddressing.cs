public static class VoxelAddressing
{
    // ================================
    // Estructuras de datos
    // ================================

    public struct GlobalCoord
    {
        public int X;
        public int Y;
        public int Z;
    }

    public struct ChunkCoord
    {
        public int X;
        public int Y;
        public int Z;
    }

    public struct LocalCoord
    {
        public int X;
        public int Y;
        public int Z;
    }

    public struct ChunkAddress
    {
        public ChunkCoord Chunk;
        public LocalCoord Local;
        public int Index;
    }

    // ================================
    // Global -> Chunk + Local + Index
    // ================================

    public static ChunkAddress GlobalToChunkAddress(
        GlobalCoord pGlobal,
        int pSizeX,
        int pSizeY,
        int pSizeZ
    )
    {
        ChunkAddress result;

        // Chunk coordinates
        result.Chunk.X = pGlobal.X / pSizeX;
        result.Chunk.Y = pGlobal.Y / pSizeY;
        result.Chunk.Z = pGlobal.Z / pSizeZ;

        // Local coordinates
        result.Local.X = pGlobal.X % pSizeX;
        result.Local.Y = pGlobal.Y % pSizeY;
        result.Local.Z = pGlobal.Z % pSizeZ;

        // Linear index (X -> Z -> Y)
        result.Index =
            result.Local.X +
            pSizeX * (
                result.Local.Z +
                pSizeZ * result.Local.Y
            );

        return result;
    }

    // ================================
    // Chunk + Index -> Global
    // ================================

    public static GlobalCoord ChunkAddressToGlobal(
        ChunkCoord pChunk,
        int pIndex,
        int pSizeX,
        int pSizeY,
        int pSizeZ
    )
    {
        GlobalCoord result;

        // Decode local coordinates
        int x = pIndex % pSizeX;
        int tmp = pIndex / pSizeX;

        int z = tmp % pSizeZ;
        int y = tmp / pSizeZ;

        // Global coordinates
        result.X = pChunk.X * pSizeX + x;
        result.Y = pChunk.Y * pSizeY + y;
        result.Z = pChunk.Z * pSizeZ + z;

        return result;
    }
}
