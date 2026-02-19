using UnityEngine;
using System.IO;

public static class HeightmapManager
{
    // Usamos .exr para mantener precisión de 32 bits (adiós escalones)
    private const string DEFAULT_PATH = @"E:\maps\1.exr";

    /// <summary>
    /// Guarda el Grid en un archivo EXR de alta precisión (32 bits).
    /// </summary>
    public static void SaveGridToHeightmap(Grid pGrid, string filePath = DEFAULT_PATH)
    {
        int res = pGrid.mChunks[0].mSize;
        int texWidth = pGrid.mSizeInChunks.x * res;
        int texHeight = pGrid.mSizeInChunks.z * res;

        // RFloat = 32 bits por píxel, linear (sin compresión de color)
        Texture2D texture = new Texture2D(texWidth, texHeight, TextureFormat.RFloat, false, true);
        float[] pixelData = new float[texWidth * texHeight];

        float maxHeight = pGrid.mSizeInChunks.y * VoxelUtils.UNIVERSAL_CHUNK_SIZE;
        float vStep = (float)VoxelUtils.UNIVERSAL_CHUNK_SIZE / res;

        for (int cz = 0; cz < pGrid.mSizeInChunks.z; cz++)
        {
            for (int cx = 0; cx < pGrid.mSizeInChunks.x; cx++)
            {
                for (int vz = 0; vz < res; vz++)
                {
                    for (int vx = 0; vx < res; vx++)
                    {
                        float hFound = 0;

                        // Buscamos de arriba hacia abajo
                        for (int cy = pGrid.mSizeInChunks.y - 1; cy >= 0; cy--)
                        {
                            int chunkIdx = cx + (cy * pGrid.mSizeInChunks.x) + (cz * pGrid.mSizeInChunks.x * pGrid.mSizeInChunks.y);
                            Chunk chunk = pGrid.mChunks[chunkIdx];

                            if (chunk.mBool1) // Si el chunk contiene sólidos
                            {
                                for (int vy = res - 1; vy >= 0; vy--)
                                {
                                    if (chunk.GetDensity(vx, vy, vz) >= 0.5f)
                                    {
                                        hFound = (cy * VoxelUtils.UNIVERSAL_CHUNK_SIZE) + (vy * vStep);
                                        goto Found;
                                    }
                                }
                            }
                        }

                    Found:
                        int px = (cx * res) + vx;
                        int pz = (cz * res) + vz;
                        pixelData[pz * texWidth + px] = hFound / maxHeight;
                    }
                }
            }
        }

        // Cargamos los datos raw de 32 bits en la textura
        texture.SetPixelData(pixelData, 0);
        texture.Apply();

        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // EncodeToEXR preserva los floats de 32 bits
            byte[] bytes = texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
            File.WriteAllBytes(filePath, bytes);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al guardar EXR: " + e.Message);
        }

        Object.DestroyImmediate(texture);
    }

}