//using System.Threading;
//using System.Threading.Tasks;
//using UnityEngine;

//public class Vigilante
//{
//    private Grid mGrid;
//    private DecimationManager mDecimator;
//    public Vector3 vCurrentCamPos;

//    public void Setup(Grid pGrid, DecimationManager pDecimator)
//    {
//        mGrid = pGrid;
//        mDecimator = pDecimator;
//    }

//    public async Task Run(CancellationToken pToken)
//    {
//        // 1. EL PUNTO DE MIRA (La Baliza)
//        // Fijamos el centro en la mitad del mundo l?gico (8 chunks * 32 / 2)

//        await Task.Delay(500, pToken);

//        while (!pToken.IsCancellationRequested)
//        {
//            Chunk[] vChunks = mGrid.mChunks;

//            for (int i = 0; i < vChunks.Length; i++)
//            {
//                Chunk vChunk = vChunks[i];
//                if (vChunk == null) continue;

//                // --- ACCESO A DATOS 100% ORIGINALES DEL CHUNK ---
//                // No hay multiplicadores m?gicos, ni rejillas ad-hoc.
//                // Leemos lo que el objeto TIENE en su memoria interna.

//                Vector3 vOriginOriginal = (Vector3)vChunk.mWorldOrigin;
//                float vSizeOriginal = vChunk.mSize;

//                // Calculamos el centro usando SU origen y SU tama?o
//                //Vector3 vCenter = VoxelUtils.GetChunkCenter(vOriginOriginal, vSizeOriginal);
//                //Vector3 vCenter = VoxelUtils.GetChunkCenter(vOriginOriginal, vSizeOriginal);

//                float vHalf = vSizeOriginal * 0.5f;

//                Vector3 vCenter = new Vector3(

//                                   vOriginOriginal.x + vHalf,

//                                   vOriginOriginal.y + vHalf,

//                                   vOriginOriginal.z + vHalf

//                               );


//                // --- M?TRICA CONTRA LA BALIZA ---
//                float vDistSq = (vCenter - vCurrentCamPos).sqrMagnitude;

//                // --- DECISI?N ---
//                int vBaseIndex = VoxelUtils.GetInfoDist(vDistSq);
//                int vTargetRes = (int)VoxelUtils.LOD_DATA[vBaseIndex];
//                mDecimator.DispatchToRender(vChunk);
//                // Forzamos el deseo sobre el chunk

//                //if (vChunk.mTargetSize != vTargetRes)
//                //{
//                //    //vChunk.mTargetSize = vTargetRes;

//                //}

//                //vChunk.mTargetSize = vTargetRes;

//            }

//            try { await Task.Delay(500, pToken); }
//            catch { break; }
//        }
//    }
//}

using System.Threading;

using System.Threading.Tasks;

using UnityEngine;



public class Vigilante

{

    private Grid mGrid;

    private DecimationManager mDecimator;

    public Vector3 vCurrentCamPos;



    public void Setup(Grid pGrid, DecimationManager pDecimator)

    {

        mGrid = pGrid;

        mDecimator = pDecimator;

    }



    public async Task Run(CancellationToken pToken)

    {

        await Task.Delay(500, pToken);

        while (!pToken.IsCancellationRequested)
        {
            Chunk[] vChunks = mGrid.mChunks;

            for (int i = 0; i < vChunks.Length; i++)

            {

                Chunk vChunk = vChunks[i];

                if (vChunk == null) continue;

                Vector3 vOriginOriginal = (Vector3)vChunk.mWorldOrigin;

                float vHalf = VoxelUtils.UNIVERSAL_CHUNK_SIZE * 0.5f;

                Vector3 vCenter = new Vector3(

                    vOriginOriginal.x + vHalf,

                    vOriginOriginal.y + vHalf,

                    vOriginOriginal.z + vHalf

                );

                float vDistSq = (vCenter - vCurrentCamPos).sqrMagnitude;

                int vBase = VoxelUtils.GetInfoDist(vDistSq);
                int vTargetRes = (int)VoxelUtils.LOD_DATA[vBase];
                int vCurrentRes = Mathf.RoundToInt(Mathf.Pow(vChunk.mVoxels.Length, 1f / 3f));

                // No marcar ni encolar hasta que el resample est? listo (DecimationManager.RequestLODChange)
                if (vCurrentRes != vTargetRes)
                    mDecimator.RequestLODChange(vChunk, vTargetRes);

            }

            try { await Task.Delay(200, pToken); }
            catch { break; }

        }

    }

}