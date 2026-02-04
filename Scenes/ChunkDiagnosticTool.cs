using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class ChunkDiagnosticTool : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool mLogEveryChunk = true;

    [ContextMenu("Ejecutar Diagnóstico Exhaustivo")]
    public void RunDiagnostic()
    {
        Debug.Log("<color=cyan><b>[DIAGNÓSTICO] Iniciando auditoría de sistema de terreno...</b></color>");

        // 1. Localizar el componente World
        World world = Object.FindObjectOfType<World>();
        if (world == null)
        {
            Debug.LogError("[ERROR] No se encontró ningún componente 'World' en la escena.");
            return;
        }

        // 2. Extraer datos privados mediante Reflection (para no obligarte a cambiar SDFWorld.cs)
        Chunk[] chunks = GetPrivateField<Chunk[]>(world, "mChunks");
        Vector3Int? worldSize = GetPrivateField<Vector3Int?>(world, "mWorldChunkSize");

        if (chunks == null)
        {
            Debug.LogError("[ERROR] El array de chunks en 'World' es nulo. ¿Se ha llamado a BuildWorld()?");
            return;
        }

        Debug.Log($"<b>[LOGICA]</b> Chunks detectados en memoria: {chunks.Length}. Dimensiones: {worldSize}");

        // 3. Mapeo de GameObjects en escena para detectar "Huérfanos" o "Zombis"
        MeshFilter[] allMeshFilters = Object.FindObjectsOfType<MeshFilter>();
        Dictionary<string, List<GameObject>> sceneObjectsByName = allMeshFilters
            .Select(mf => mf.gameObject)
            .GroupBy(go => go.name)
            .ToDictionary(g => g.Key, g => g.ToList());

        HashSet<GameObject> referencedObjects = new HashSet<GameObject>();

        // 4. RECORRIDO SECUENCIAL DE CHUNKS (Auditoría Lógica vs Visual)
        Debug.Log("<color=orange><b>--- VOLCADO SECUENCIAL DE CHUNKS ---</b></color>");

        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk c = chunks[i];
            if (c == null) { Debug.LogWarning($"Chunk [{i}]: Es NULO en el array."); continue; }

            // Analizar ocupación de voxeles
            int solidCount = c.mVoxels.Count(v => v.solid != 0);

            // Estado de la referencia visual
            string goStatus = "NULL";
            if (c.mViewGO != null)
            {
                goStatus = $"Asignado ({c.mViewGO.name})";
                referencedObjects.Add(c.mViewGO);

                // Verificar posición física vs lógica
                float dist = Vector3.Distance(c.mViewGO.transform.position, (Vector3)c.mWorldOrigin);
                if (dist > 0.01f)
                {
                    Debug.LogError($"<b>[ERROR POSICIÓN]</b> Chunk {c.mCoord}: El objeto visual está en {c.mViewGO.transform.position} pero su origen lógico es {c.mWorldOrigin}.");
                }
            }

            if (mLogEveryChunk)
            {
                Debug.Log($"<b>Chunk [{i}]</b> | Coord: {c.mCoord} | Origin: {c.mWorldOrigin} | Sólidos: {solidCount} | View: {goStatus}");
            }

            // Detectar si hay objetos en la escena con el nombre de este chunk que NO son su mViewGO
            string expectedName = $"SurfaceNet_Chunk_{c.mCoord.x}_{c.mCoord.y}_{c.mCoord.z}";
            if (sceneObjectsByName.ContainsKey(expectedName))
            {
                var clones = sceneObjectsByName[expectedName];
                if (clones.Count > 1 || (clones.Count == 1 && clones[0] != c.mViewGO))
                {
                    Debug.LogError($"<b>[DUPLICADO]</b> Se detectaron objetos 'fantasma' en la escena con el nombre {expectedName} que el Chunk lógico no reconoce como suyos.");
                }
            }
        }

        // 5. Detección de objetos "Zombis" (Existen en escena pero ningún Chunk los reclama)
        foreach (var kvp in sceneObjectsByName)
        {
            if (kvp.Key.StartsWith("SurfaceNet_Chunk_"))
            {
                foreach (var go in kvp.Value)
                {
                    if (!referencedObjects.Contains(go))
                    {
                        Debug.LogError($"<b>[ZOMBI]</b> El objeto '{go.name}' (ID: {go.GetInstanceID()}) existe en la jerarquía pero no pertenece a ningún Chunk lógico. Esto causará Raycasts e invasiones visuales erróneas.");
                    }
                }
            }
        }

        Debug.Log("<color=cyan><b>[DIAGNÓSTICO] Auditoría finalizada.</b></color>");
    }

    private T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (field == null) return default;
        return (T)field.GetValue(target);
    }
}