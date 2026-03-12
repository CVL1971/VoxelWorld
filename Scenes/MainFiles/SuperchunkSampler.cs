using UnityEngine;

public static class SuperChunkSampler
{
    public static void Sample(nChunkB pChunk)
    {
        Vector3Int vOrigin = pChunk.WorldOrigin;

        float vChunkSize = pChunk.ChunkSize;

        int vN = pChunk.mSize;

        int vPaddedRes = vN + 3;

        float vStep = vChunkSize / vN;

        float[] vCache = pChunk.mSamples;

        pChunk.ResetFlags();

        for (int z = 0; z < vPaddedRes; z++)
        {

           
            float vWorldZ = vOrigin.z + ((z - 1) * vStep);

            for (int x = 0; x < vPaddedRes; x++)
            {
                float vWorldX = vOrigin.x + ((x - 1) * vStep);

                float vHeight = SDFGenerator.GetGeneratedHeight(vWorldX, vWorldZ);

                for (int y = 0; y < vPaddedRes; y++)
                {
                    float vWorldY = vOrigin.y + ((y - 1) * vStep);

                    float vDensity = (vHeight - vWorldY) + nChunkB.ISO_LEVEL_CONST;

                    int vIdx =
                        x +
                        vPaddedRes * (y + vPaddedRes * z);

                    vCache[vIdx] = vDensity;

                    if (vDensity >= nChunkB.ISO_LEVEL_CONST)
                        pChunk.mBool1 = true;
                    else
                        pChunk.mBool2 = true;
                }
            }
        }
    }
}
