using UnityEngine;

public static class GeneralData
{
    // -----------------------
    // Reader global
    // -----------------------

    public static SchematicReader.VolumeData mVolumeData;

    // -----------------------
    // Dimensiones del volumen leído
    // -----------------------

    public static int mVolumeSizeX;
    public static int mVolumeSizeY;
    public static int mVolumeSizeZ;

    // -----------------------
    // Chunking
    // -----------------------

    public static int mChunkSize;

    public static int mChunkCountX;
    public static int mChunkCountY;
    public static int mChunkCountZ;

    // -----------------------
    // Terreno
    // -----------------------

    public static Terrain mTerrain;
}
