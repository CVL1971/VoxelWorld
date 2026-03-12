using UnityEngine;

/// <summary>
/// Clipmap de 3 niveles según formulación original (Losasso & Hoppe 2004, GPU Gems 2).
/// Estructura emergente exclusivamente de fórmulas matemáticas.
/// Representación con cubos de malla real para verificación visual.
/// </summary>
[ExecuteInEditMode]
public class ClipmapVisualizer : MonoBehaviour
{
    const int TILE_RES = 4;       // Resolución por tile (paper)
    const int TILES_PER_SIDE = 4; // Grid 4×4 por nivel (paper)
    const float BASE_SIZE = 4f;
    const int LEVELS = 3;

    const string CUBES_ROOT_NAME = "ClipmapCubes";

    /// <summary>
    /// Factor de escala para separación visual entre tiles.
    /// Reducción intencional para que cada tile sea distinguible en vista 2D
    /// y no se funda con las superficies colindantes.
    /// </summary>
    const float TILE_VISUAL_SCALE = 0.95f;

    Transform mCubesRoot;
    Material[] mLevelMaterials;

    #region ITERACIÓN

    void OnEnable()
    {
        RebuildCubes();
    }

    void OnDisable()
    {
        DestroyCubes();
    }

    void RebuildCubes()
    {
        DestroyCubes();

        mLevelMaterials = CreateLevelMaterials();
        mCubesRoot = new GameObject(CUBES_ROOT_NAME).transform;
        mCubesRoot.SetParent(transform, false);

        for (int vLevel = 0; vLevel < LEVELS; vLevel++)
        {
            for (int vX = 0; vX < TILES_PER_SIDE; vX++)
            {
                for (int vY = 0; vY < TILES_PER_SIDE; vY++)
                {
                    ClipmapTileData vData;
                    if (!GetTileFromIndices(vLevel, vX, vY, out vData))
                        continue;

                    CreateCube(vData);
                }
            }
        }
    }

    void CreateCube(ClipmapTileData pData)
    {
        GameObject vGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(vGo.GetComponent<Collider>());

        vGo.name = string.Format("L{0}_{1}_{2}", pData.Level, pData.Position.x, pData.Position.z);
        vGo.transform.SetParent(mCubesRoot, false);
        vGo.transform.localPosition = pData.Position;
        vGo.transform.localScale = Vector3.one * pData.Size * TILE_VISUAL_SCALE;
        vGo.transform.localRotation = Quaternion.identity;

        MeshRenderer vRenderer = vGo.GetComponent<MeshRenderer>();
        vRenderer.sharedMaterial = mLevelMaterials[pData.Level];
    }

    void DestroyCubes()
    {
        if (mCubesRoot != null)
        {
            if (Application.isPlaying)
                Object.Destroy(mCubesRoot.gameObject);
            else
                Object.DestroyImmediate(mCubesRoot.gameObject);
            mCubesRoot = null;
        }

        if (mLevelMaterials != null)
        {
            for (int i = 0; i < mLevelMaterials.Length; i++)
            {
                if (mLevelMaterials[i] != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(mLevelMaterials[i]);
                    else
                        Object.DestroyImmediate(mLevelMaterials[i]);
                }
            }
            mLevelMaterials = null;
        }
    }

    Material[] CreateLevelMaterials()
    {
        Shader vShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        Material[] vMats = new Material[LEVELS];
        for (int i = 0; i < LEVELS; i++)
        {
            vMats[i] = new Material(vShader);
            if (vMats[i].HasProperty("_BaseColor"))
                vMats[i].SetColor("_BaseColor", LevelColor(i));
            else
                vMats[i].SetColor("_Color", LevelColor(i));
        }
        return vMats;
    }

    #endregion

    #region FÓRMULAS DEL PAPER

    /// <summary>
    /// Obtiene datos del tile a partir de índices (level, x, y).
    /// Fórmulas: scale = 2^l, snapped = floor(cam/scale)×scale, base = snapped − 2×tile_size.
    /// </summary>
    bool GetTileFromIndices(int pLevel, int pX, int pY, out ClipmapTileData pData)
    {
        pData = default(ClipmapTileData);

        // scale(l) = 2^l — espaciado de vértices (paper §4)
        int vScale = 1 << pLevel;

        // tile_size = TILE_RES × scale × base (paper)
        float vTileSize = TILE_RES * vScale * BASE_SIZE;

        // snapped_pos = floor(cam / scale) × scale — cam = 0 para centro
        float vSnapped = 0f;

        // base = snapped − 2 × tile_size (paper: esquina del tile 0,0)
        float vBase = vSnapped - 2f * vTileSize;

        // tile_bl = base + (x, y) × tile_size
        float vBlX = vBase + pX * vTileSize;
        float vBlZ = vBase + pY * vTileSize;

        // Centro del tile (DrawCube usa centro)
        pData.Position = new Vector3(
            vBlX + vTileSize * 0.5f,
            0f,
            vBlZ + vTileSize * 0.5f
        );
        pData.Level = pLevel;
        pData.Size = vTileSize;

        // render_region(l) = active(l) − active(l+1) → anillo.
        // En grid 4×4: omitir centro 2×2 para l>0.
        // Fórmula: dibujar si l=0 o tile está en el anillo.
        // Anillo ⟺ (x·(3−x)=0) ∨ (y·(3−y)=0) — bordes del grid.
        bool vInRing = (pX * (3 - pX) == 0) || (pY * (3 - pY) == 0);
        bool vDraw = (pLevel == 0) || vInRing;

        return vDraw;
    }

    #endregion

    #region UTILIDADES

    Color LevelColor(int pLevel)
    {
        switch (pLevel)
        {
            case 0: return Color.green;
            case 1: return Color.yellow;
            case 2: return Color.red;
        }
        return Color.white;
    }

    #endregion

    #region ESTRUCTURA

    struct ClipmapTileData
    {
        public Vector3 Position;
        public int Level;
        public float Size;
    }

    #endregion
}
