using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

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

    //El cálculo puro que antes ensuciaba el World
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

    //public float CalculateDensity(Vector3 pWorldPos, float pCurrentDensity)
    //{
    //    // 1. Cálculo base de la brocha (Distancia + Ruido)
    //    float vDist = Vector3.Distance(pWorldPos, mCenter);
    //    float vNoise = (Mathf.PerlinNoise(pWorldPos.x * 1.5f, pWorldPos.z * 1.5f) - 0.5f) * mNoiseAmp;

    //    // vField es 0 en el centro y 1 en el borde del radio
    //    float vField = Mathf.Clamp01((vDist + vNoise) / mRadius);

    //    if (mIsExcavating)
    //    {
    //        // EXCAVACIÓN (Smooth Minimum)
    //        // Sustrae el volumen de la brocha de la densidad actual
    //        float vH = Mathf.Clamp01(0.5f + 0.5f * (vField - pCurrentDensity) / mK);
    //        return Mathf.Lerp(vField, pCurrentDensity, vH) - mK * vH * (1.0f - vH);
    //    }
    //    else
    //    {
    //        // EXTRUSIÓN (Smooth Union / Maximum)
    //        // Definimos el sólido de la brocha (1 en el centro, 0 en el borde)
    //        float vBrushSolid = 1.0f - vField;

    //        // Calculamos la unión suave para rellenar el volumen
    //        float vH = Mathf.Clamp01(0.5f + 0.5f * (vBrushSolid - pCurrentDensity) / mK);
    //        return Mathf.Lerp(pCurrentDensity, vBrushSolid, vH) + mK * vH * (1.0f - vH);
    //    }
}

//    else if (mIsSmoothing) 
//{
//    // El objetivo es llevar la densidad actual hacia un valor intermedio (0.5)
//    // pero solo dentro del radio de la brocha (vField)
//    float vTarget = 0.5f;
//    float vSmoothFactor = 1.0f - vField; // Más fuerte en el centro de la brocha
    
//    return Mathf.Lerp(pCurrentDensity, vTarget, vSmoothFactor* mK);
//}
//}