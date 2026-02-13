using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class ChunkDiagnosticTool : MonoBehaviour
{
    [Header("Configuraci?n")]
    [SerializeField] private bool mLogEveryChunk = true;

    [ContextMenu("Ejecutar Diagn?stico Exhaustivo")]
    public void RunDiagnostic()
    {
        Debug.Log("<color=cyan><b>[DIAGN?STICO] Iniciando auditor?a de sistema de terreno...</b></color>");

        // 1. Localizar el componente World
        World world = Object.FindObjectOfType<World>();
        if (world == null)
        {
            Debug.LogError("[ERROR] No se encontr? ning?n componente 'World' en la escena.");
            return;
        }

        // 2. Extraer Grid desde World (los chunks est?n en mGrid)
        Grid grid = GetPrivateField<Grid>(world, "mGrid");
        if (grid == null)
        {
            Debug.LogError("[ERROR] No se encontr? 'mGrid' en World. ?Se ha inicializado el mundo?");
            return;
        }
        Chunk[] chunks = grid.mChunks;
        Vector3Int worldSize = grid.mSizeInChunks;

        if (chunks == null)
        {
            Debug.LogError("[ERROR] El array de chunks en Grid es nulo. ?Se ha llamado a BuildSurfaceNets?");
            return;
        }

        Debug.Log($"<b>[LOGICA]</b> Chunks detectados en memoria: {chunks.Length}. Dimensiones (en chunks): {worldSize}");

        // 3. Mapeo de GameObjects en escena para detectar "Hu?rfanos" o "Zombis"
        MeshFilter[] allMeshFilters = Object.FindObjectsOfType<MeshFilter>();
        Dictionary<string, List<GameObject>> sceneObjectsByName = allMeshFilters
            .Select(mf => mf.gameObject)
            .GroupBy(go => go.name)
            .ToDictionary(g => g.Key, g => g.ToList());

        HashSet<GameObject> referencedObjects = new HashSet<GameObject>();

        // 4. RECORRIDO SECUENCIAL DE CHUNKS (Auditor?a L?gica vs Visual)
        Debug.Log("<color=orange><b>--- VOLCADO SECUENCIAL DE CHUNKS ---</b></color>");

        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk c = chunks[i];
            if (c == null) { Debug.LogWarning($"Chunk [{i}]: Es NULO en el array."); continue; }

            // Analizar ocupaci?n de voxeles
            int solidCount = c.mVoxels.Count(v => v.solid != 0);

            // Estado de la referencia visual
            string goStatus = "NULL";
            if (c.mViewGO != null)
            {
                goStatus = $"Asignado ({c.mViewGO.name})";
                referencedObjects.Add(c.mViewGO);

                // Verificar posici?n f?sica vs l?gica
                float dist = Vector3.Distance(c.mViewGO.transform.position, (Vector3)c.mWorldOrigin);
                if (dist > 0.01f)
                {
                    Debug.LogError($"<b>[ERROR POSICI?N]</b> Chunk {c.mCoord}: El objeto visual est? en {c.mViewGO.transform.position} pero su origen l?gico es {c.mWorldOrigin}.");
                }
            }

            if (mLogEveryChunk)
            {
                Debug.Log($"<b>Chunk [{i}]</b> | Coord: {c.mCoord} | Origin: {c.mWorldOrigin} | S?lidos: {solidCount} | View: {goStatus}");
            }

            // Nombres en escena: "Chunk_(x, y, z)" (SDFWorld.BuildSurfaceNets)
            string expectedName = "Chunk_" + c.mCoord.ToString();
            if (sceneObjectsByName.ContainsKey(expectedName))
            {
                var clones = sceneObjectsByName[expectedName];
                if (clones.Count > 1 || (clones.Count == 1 && clones[0] != c.mViewGO))
                {
                    Debug.LogError($"<b>[DUPLICADO]</b> Se detectaron objetos 'fantasma' en la escena con el nombre {expectedName} que el Chunk l?gico no reconoce como suyos.");
                }
            }
        }

        // 5. Detecci?n de objetos "Zombis" (Existen en escena pero ning?n Chunk los reclama)
        foreach (var kvp in sceneObjectsByName)
        {
            if (kvp.Key.StartsWith("Chunk_"))
            {
                foreach (var go in kvp.Value)
                {
                    if (!referencedObjects.Contains(go))
                    {
                        Debug.LogError($"<b>[ZOMBI]</b> El objeto '{go.name}' (ID: {go.GetInstanceID()}) existe en la jerarqu?a pero no pertenece a ning?n Chunk l?gico. Esto causar? Raycasts e invasiones visuales err?neas.");
                    }
                }
            }
        }

        Debug.Log("<color=cyan><b>[DIAGN?STICO] Auditor?a finalizada.</b></color>");
    }

    private T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (field == null) return default;
        return (T)field.GetValue(target);
    }
}