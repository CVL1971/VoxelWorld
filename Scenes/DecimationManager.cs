//using UnityEngine;
//using System.Collections.Generic;

//public class DecimationManager
//{
//    private float[] mLodDistances;
//    private float mUpdateInterval;
//    private float mTimeSinceLastUpdate;

//    public DecimationManager(float pDist0, float pDist1, float pDist2, float pInterval)
//    {
//        mLodDistances = new float[] { pDist0, pDist1, pDist2 };
//        mUpdateInterval = pInterval;
//        mTimeSinceLastUpdate = 0.0f;
//    }

//    // Este método se llamaría desde World.Update()
//    public void RunDecimationTick(float pDeltaTime, Vector3 pCameraPos, List<Chunk> pActiveChunks)
//    {
//        mTimeSinceLastUpdate += pDeltaTime;

//        if (mTimeSinceLastUpdate >= mUpdateInterval)
//        {
//            // Cacheamos los cuadrados de las distancias para el bucle
//            float vDist0Sq = mLodDistances[0] * mLodDistances[0];
//            float vDist1Sq = mLodDistances[1] * mLodDistances[1];
//            float vDist2Sq = mLodDistances[2] * mLodDistances[2];

//            for (int vI = 0; vI < pActiveChunks.Count; vI++)
//            {
//                Chunk vCurrentChunk = pActiveChunks[vI];

//                // Cálculo de distancia cuadrada (rápido)
//                float vSqDist = (pCameraPos - vCurrentChunk.Position).sqrMagnitude;

//                int vTargetLOD = 3;
//                if (vSqDist < vDist0Sq) vTargetLOD = 0;
//                else if (vSqDist < vDist1Sq) vTargetLOD = 1;
//                else if (vSqDist < vDist2Sq) vTargetLOD = 2;

//                if (vCurrentChunk.CurrentLOD != vTargetLOD)
//                {
//                    vCurrentChunk.UpdateLOD(vTargetLOD);
//                }
//            }

//            mTimeSinceLastUpdate = 0.0f;
//        }
//    }
//}