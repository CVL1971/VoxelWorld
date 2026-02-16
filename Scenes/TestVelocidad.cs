using UnityEngine;
using System.Diagnostics;

public class VigilanteSimulator : MonoBehaviour
{
    // Simulamos la clase Chunk con sus datos internos
    public class MockChunk
    {
        public Vector3 mWorldOrigin;
        public int currentRes;
        public int voxelDataLength; // Para evitar llamar a array.Length constantemente
    }

    void Start() => RunArchitectTest();

    public void RunArchitectTest()
    {
        int W = 128, H = 16, D = 128;
        int totalChunks = W * H * D;
        MockChunk[] vChunks = new MockChunk[totalChunks];

        // Setup de datos: Simulamos un mundo con diferentes resoluciones
        for (int i = 0; i < totalChunks; i++)
        {
            vChunks[i] = new MockChunk
            {
                mWorldOrigin = new Vector3((i % W) * 32, ((i / W) % H) * 32, (i / (W * H)) * 32),
                currentRes = 32,
                voxelDataLength = 32768 // 32^3
            };
        }

        Vector3 vCurrentCamPos = new Vector3(2048, 256, 2048); // Centro del mundo en unidades
        float vHalf = 16.0f; // VoxelUtils.UNIVERSAL_CHUNK_SIZE * 0.5f

        UnityEngine.Debug.Log($"<b>[ARQUITECTO]</b> Simulando Vigilante sobre {totalChunks} objetos...");

        Stopwatch sw = Stopwatch.StartNew();
        int changeRequests = 0;

        // --- EL BUCLE REAL DE TU SCRIPT ---
        for (int i = 0; i < totalChunks; i++)
        {
            MockChunk vChunk = vChunks[i];
            if (vChunk == null) continue; // IF 1: Null Check

            // 1. CÁLCULO DE CENTRO (Acceso a Vector3 y aritmética)
            Vector3 vOrigin = vChunk.mWorldOrigin;
            Vector3 vCenter = new Vector3(
                vOrigin.x + vHalf,
                vOrigin.y + vHalf,
                vOrigin.z + vHalf
            );

            // 2. MÉTRICA (sqrMagnitude + Sqrt para emular GetInfoDist)
            float vDistSq = (vCenter - vCurrentCamPos).sqrMagnitude;
            float vDist = Mathf.Sqrt(vDistSq);

            // 3. ACCESO A CONFIGURACIÓN (Simulando VoxelUtils.LOD_DATA)
            int vTargetRes = vDist < 500 ? 32 : (vDist < 1000 ? 16 : 8);

            // 4. EL CÁLCULO PESADO (Raíz cúbica para resolución actual)
            // Esto es lo que mata el rendimiento en tu script original
            int vCurrentRes = Mathf.RoundToInt(Mathf.Pow(vChunk.voxelDataLength, 1f / 3f));

            // 5. DECISIÓN (IF 2)
            if (vCurrentRes != vTargetRes)
            {
                changeRequests++;
                // mDecimator.RequestLODChange(vChunk, vTargetRes);
            }
        }

        sw.Stop();

        // RESULTADOS
        double ms = sw.Elapsed.TotalMilliseconds;
        UnityEngine.Debug.Log("---------------------------------------");
        UnityEngine.Debug.Log($"<b>TIEMPO SIMULACIÓN:</b> {ms:F4} ms");
        UnityEngine.Debug.Log($"<b>PETICIONES GENERADAS:</b> {changeRequests}");
        UnityEngine.Debug.Log($"<b>NS POR CHUNK:</b> {(ms * 1000000.0 / totalChunks):F2} ns");
        UnityEngine.Debug.Log("---------------------------------------");
    }
}