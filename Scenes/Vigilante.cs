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
        while (!pToken.IsCancellationRequested)
        {
            Vector3 vPos = vCurrentCamPos;
            Chunk[] vChunks = mGrid.mChunks;

            for (int i = 0; i < vChunks.Length; i++)
            {
                Chunk vChunk = vChunks[i];
                float vDistSq = (vChunk.mWorldOrigin - vPos).sqrMagnitude;

                // 1. Consulta a la Autoridad (VoxelUtils) para obtener el bloque de datos
                int vBase = VoxelUtils.GetInfoDist(vDistSq);
                int vTargetRes = (int)VoxelUtils.LOD_DATA[vBase]; // Resolución objetivo
                float vLimitSq = VoxelUtils.LOD_DATA[vBase + 2]; // Distancia límite del bloque

                // 2. Detección de necesidad de cambio
                bool vNecesitaCambio = (vChunk.mSize != vTargetRes);
                string vTag = vNecesitaCambio ? "<color=orange><b>[CAMBIO]</b></color> " : "";

                // 3. Log de Vigilante con datos objetivos
                Debug.Log($"{vTag}[Vigilante] Chunk:{vChunk.mWorldOrigin} | " +
                          $"DistSq:{vDistSq:F0} | " +
                          $"LimitSq:{vLimitSq} | " +
                          $"Res:{vChunk.mSize} -> {vTargetRes}");

                if (vNecesitaCambio)
                {
                    vChunk.mTargetSize = vTargetRes;
                    mDecimator.DispatchToRender(vChunk); //
                }
            }

            try { await Task.Delay(200, pToken); }
            catch { break; }
        }
    }
}