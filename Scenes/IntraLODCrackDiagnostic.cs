using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Diagnóstico de grietas intra-LOD: visualiza bordes de chunk, celdas limítrofes
/// y ayuda a detectar geometría faltante en fronteras (típico en Surface Nets sin padding).
/// </summary>
public class IntraLODCrackDiagnostic : MonoBehaviour
{
    [Header("Visualización")]
    [SerializeField] bool mDrawChunkBounds = true;
    [SerializeField] bool mDrawBoundaryFaces = true;
    [SerializeField] Color mBoundaryColor = new Color(1f, 0.5f, 0f, 0.8f);
    [SerializeField] float mLineDuration = 2f;

    [Header("Log")]
    [SerializeField] bool mLogBoundaryStats = false;

    Grid mGrid;

    void OnEnable()
    {
        World w = FindObjectOfType<World>();
        if (w == null) return;
        mGrid = (Grid)w.GetType().GetField("mGrid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(w);
    }

    void Update()
    {
        if (mGrid == null || mGrid.mChunks == null) return;

        if (mDrawChunkBounds || mDrawBoundaryFaces)
        {
            int boundaryCellCount = 0;
            foreach (Chunk c in mGrid.mChunks)
            {
                if (c == null || c.mViewGO == null) continue;

                Vector3 origin = c.WorldOrigin;
                float size = (c.mSize > 0 ? c.mSize : VoxelUtils.UNIVERSAL_CHUNK_SIZE) * (VoxelUtils.UNIVERSAL_CHUNK_SIZE / (float)(c.mSize > 0 ? c.mSize : VoxelUtils.UNIVERSAL_CHUNK_SIZE));
                float step = VoxelUtils.UNIVERSAL_CHUNK_SIZE / (float)(c.mSize > 0 ? c.mSize : VoxelUtils.UNIVERSAL_CHUNK_SIZE);
                Vector3 sizeV = new Vector3(VoxelUtils.UNIVERSAL_CHUNK_SIZE, VoxelUtils.UNIVERSAL_CHUNK_SIZE, VoxelUtils.UNIVERSAL_CHUNK_SIZE);

                if (mDrawChunkBounds)
                {
                    DrawBox(origin, origin + sizeV, Color.cyan, mLineDuration * 0.5f);
                }

                if (mDrawBoundaryFaces)
                {
                    float s = VoxelUtils.UNIVERSAL_CHUNK_SIZE;
                    Vector3 o = origin;
                    DrawQuad(o, o + new Vector3(s, 0, 0), o + new Vector3(s, s, 0), o + new Vector3(0, s, 0), mBoundaryColor);
                    DrawQuad(o + new Vector3(0, 0, s), o + new Vector3(s, 0, s), o + new Vector3(s, s, s), o + new Vector3(0, s, s), mBoundaryColor);
                    DrawQuad(o, o + new Vector3(0, s, 0), o + new Vector3(0, s, s), o + new Vector3(0, 0, s), mBoundaryColor);
                    DrawQuad(o + new Vector3(s, 0, 0), o + new Vector3(s, 0, s), o + new Vector3(s, s, s), o + new Vector3(s, s, 0), mBoundaryColor);
                    DrawQuad(o, o + new Vector3(s, 0, 0), o + new Vector3(s, 0, s), o + new Vector3(0, 0, s), mBoundaryColor);
                    DrawQuad(o + new Vector3(0, s, 0), o + new Vector3(0, s, s), o + new Vector3(s, s, s), o + new Vector3(s, s, 0), mBoundaryColor);
                    boundaryCellCount += 6;
                }
            }
        }
    }

    static void DrawBox(Vector3 min, Vector3 max, Color c, float dur)
    {
        Vector3 p000 = min; Vector3 p100 = new Vector3(max.x, min.y, min.z);
        Vector3 p010 = new Vector3(min.x, max.y, min.z); Vector3 p110 = new Vector3(max.x, max.y, min.z);
        Vector3 p001 = new Vector3(min.x, min.y, max.z); Vector3 p101 = new Vector3(max.x, min.y, max.z);
        Vector3 p011 = new Vector3(min.x, max.y, max.z); Vector3 p111 = max;
        Debug.DrawLine(p000, p100, c, dur); Debug.DrawLine(p100, p110, c, dur); Debug.DrawLine(p110, p010, c, dur); Debug.DrawLine(p010, p000, c, dur);
        Debug.DrawLine(p001, p101, c, dur); Debug.DrawLine(p101, p111, c, dur); Debug.DrawLine(p111, p011, c, dur); Debug.DrawLine(p011, p001, c, dur);
        Debug.DrawLine(p000, p001, c, dur); Debug.DrawLine(p100, p101, c, dur); Debug.DrawLine(p110, p111, c, dur); Debug.DrawLine(p010, p011, c, dur);
    }

    static void DrawQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        float t = 2f;
        Debug.DrawLine(a, b, col, t); Debug.DrawLine(b, c, col, t); Debug.DrawLine(c, d, col, t); Debug.DrawLine(d, a, col, t);
    }

    [ContextMenu("Diagnóstico: resumen por chunk (LOD y bordes)")]
    public void LogPerChunkSummary()
    {
        World w = FindObjectOfType<World>();
        if (w == null) { Debug.LogError("No World in scene."); return; }
        Grid g = (Grid)w.GetType().GetField("mGrid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(w);
        if (g == null || g.mChunks == null) { Debug.LogError("No Grid/chunks."); return; }
    }
}
