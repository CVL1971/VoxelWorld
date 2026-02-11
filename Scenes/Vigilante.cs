using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class Vigilante
{
    private Grid mGrid;
    private DecimationManager mDecimator;

    // Ignorada en el cálculo para este test, tú eres el espectador libre
    public Vector3 vCurrentCamPos;

    public void Setup(Grid pGrid, DecimationManager pDecimator)
    {
        mGrid = pGrid;
        mDecimator = pDecimator;
    }

    public async Task Run(CancellationToken pToken)
    {
        // 1. EL PUNTO DE MIRA (La Baliza)
        // Fijamos el centro en la mitad del mundo lógico (8 chunks * 32 / 2)
        Vector3 vViewLocation = vCurrentCamPos;

        
        await Task.Delay(500, pToken);

        while (!pToken.IsCancellationRequested)
        {
            Chunk[] vChunks = mGrid.mChunks;

            for (int i = 0; i < vChunks.Length; i++)
            {
                Chunk vChunk = vChunks[i];
                if (vChunk == null) continue;

                // --- ACCESO A DATOS 100% ORIGINALES DEL CHUNK ---
                // No hay multiplicadores mágicos, ni rejillas ad-hoc.
                // Leemos lo que el objeto TIENE en su memoria interna.

                Vector3 vOriginOriginal = (Vector3)vChunk.mWorldOrigin;
                float vSizeOriginal = vChunk.mSize;

                // Calculamos el centro usando SU origen y SU tamaño

                Vector3 vCenter = VoxelUtils.GetChunkCenter(vOriginOriginal, vSizeOriginal);
               
                // --- MÉTRICA CONTRA LA BALIZA ---
                float vDistSq = (vCenter - vViewLocation).sqrMagnitude;

                // --- DECISIÓN ---
                int vBaseIndex = VoxelUtils.GetInfoDist(vDistSq);
                int vTargetRes = (int)VoxelUtils.LOD_DATA[vBaseIndex];

       

                // 2. --- ASIGNACIÓN DE COLOR (Sin condicionantes) ---
                // Dividimos por 4 para tener el índice del switch (0, 1, 2)
                int vLodIndex = vBaseIndex / 4;
                Color vDebugColor;

                switch (vLodIndex)
                {
                    case 0: // LOD Máximo (Res 32)
                        vDebugColor = Color.white;
                        break;
                    case 1: // LOD Medio (Res 16)
                        vDebugColor = Color.blue;
                        break;
                    case 2: // LOD Bajo (Res 8)
                        vDebugColor = Color.red;
                        break;
                    default:
                        vDebugColor = Color.gray;
                        break;
                }

                // 4. DIBUJO DE LA CAJA (Visualización constante)
                // Usamos 0.6s para que no parpadee si el Delay es de 0.5s
                vChunk.DrawDebug(vDebugColor, 0.6f);




                // Forzamos el deseo sobre el chunk

                if (vChunk.mTargetSize != vTargetRes)
                {
                    vChunk.mTargetSize = vTargetRes;
                    mDecimator.DispatchToRender(vChunk);
                }
                // --- LOG DE VERIFICACIÓN DE IDENTIDAD ---
                // Si el mWorldOrigin no es el que debería ser, este log lo cantará.
                //if (vChunk.mSize != vTargetRes)
                //{
                //    Debug.Log($"<color=white><b>[FRANKENSTEIN-DATA]</b></color> " +
                //              $"ID:{i} | Coord:{vChunk.mCoord} | " +
                //              $"Origin:{vOriginOriginal} | Size:{vSizeOriginal} | " +
                //              $"DistSq:{vDistSq:F0} -> Target:{vTargetRes}");
                //}

                // Mandamos a renderizar la caja de test

            }

            try { await Task.Delay(500, pToken); }
            catch { break; }
        }
    }
}