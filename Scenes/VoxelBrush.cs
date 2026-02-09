using UnityEngine;

public class VoxelBrush
{
    public Vector3 mCenter;
    public float mRadius;
    public float mK;
    public float mNoiseAmp;
    public bool mIsExcavating;

    public VoxelBrush(Vector3 pCenter, float pRadius, float pK, float pNoiseAmp, bool pIsExcavating)
    {
        mCenter = pCenter;
        mRadius = pRadius;
        mK = pK;
        mNoiseAmp = pNoiseAmp;
        mIsExcavating = pIsExcavating;
    }

    // El cálculo puro que antes ensuciaba el World
    public float CalculateDensity(Vector3 pWorldPos, float pCurrentDensity)
    {
        float vDist = Vector3.Distance(pWorldPos, mCenter);
        float vNoise = (Mathf.PerlinNoise(pWorldPos.x * 1.5f, pWorldPos.z * 1.5f) - 0.5f) * mNoiseAmp;
        float vField = Mathf.Clamp01((vDist + vNoise) / mRadius);

        if (mIsExcavating)
        {
            float vH = Mathf.Clamp01(0.5f + 0.5f * (vField - pCurrentDensity) / mK);
            return Mathf.Lerp(vField, pCurrentDensity, vH) - mK * vH * (1.0f - vH);
        }
        else
        {
            float vSolid = 1.0f - vField;
            float vH = Mathf.Clamp01(0.5f + 0.5f * (pCurrentDensity - vSolid) / mK);
            return Mathf.Lerp(pCurrentDensity, vSolid, vH) + mK * vH * (1.0f - vH);
        }
    }
}