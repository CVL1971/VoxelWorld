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
            ushort[] vStatus = mGrid.mStatusGrid;
            Vector3Int vGridSize = mGrid.mSizeInChunks;
            int vChunkSize = mGrid.mChunkSize;
            int vCount = vStatus.Length;
        
            for (int i = 0; i < vCount; i++)
            {
                
                ushort status = vStatus[i];

                // 1. FILTROS RÁPIDOS
                // Si no hay superficie o ya se está procesando, ignoramos.
                if ((status & Grid.BIT_SURFACE) == 0 || (status & Grid.MASK_PROCESSING) != 0)
                    continue;

                // 2. POSICIÓN SIN CHUNK
                // Usamos la función que añadimos a VoxelUtils para evitar tocar el objeto Chunk
                //Vector3 vCenter = VoxelUtils.GetChunkCenterByIndex(i, vGridSize, vChunkSize);
                Vector3 vCenter = VoxelUtils.GetChunkCenter(mGrid.mChunks[i].mWorldOrigin, vChunkSize);
                float vDistSq = (vCenter - vCurrentCamPos).sqrMagnitude;

                // 3. CÁLCULO DE OBJETIVO
                int vInfoBlock = VoxelUtils.GetInfoDist(vDistSq);
                int vTargetLodIdx = (int)VoxelUtils.LOD_DATA[vInfoBlock + 3];
                int vCurrentLodIdx = (status & Grid.MASK_LOD_CURRENT) >> 2;

                if (vCurrentLodIdx != vTargetLodIdx)
                {
                    // 4. BLOQUEO Y ENVÍO
                    mGrid.SetLodTarget(i, vTargetLodIdx);
                    mGrid.SetProcessing(i, true);

                    int vTargetRes = (int)VoxelUtils.LOD_DATA[vInfoBlock];

                    mDecimator.RequestLODChange(mGrid.mChunks[i], vTargetRes);
                }
            }

            
            await Task.Delay(100, pToken);
        }
    }
}








