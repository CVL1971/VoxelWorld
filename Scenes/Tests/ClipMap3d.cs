using UnityEngine;

/// <summary>
/// Clipmap 3D: estructura del paper (4×4×4, omitir centro 2×2×2).
/// Garantiza ratio 1:4 en fronteras y grosor de un cubo en la frontera interior.
/// </summary>
[ExecuteInEditMode]
public class ClipMap3d : MonoBehaviour
{
    const int TILE_RES = 4;
    const int TILES_PER_SIDE = 4;
    const float BASE_SIZE = 4f;
    const int LEVELS = 5;
    /// <summary>Resolución de muestreo fija por cubo (32 celdas → 33 samples en los bordes).</summary>
    const int DENSITY_RES = 32;

    const string CUBES_ROOT_NAME = "ClipmapCubes3D";
    /// <summary>Escala visual &lt; 1 para reducir el volumen del cubo y crear separación entre tiles.</summary>
    const float TILE_VISUAL_SCALE = 0.95f;

    [SerializeField]
    [Range(0f, 1f)]
    float mTransparency = 0.5f;

    Transform mCubesRoot;
    Material[] mLevelMaterials;
    Material mDensityMaterial;

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
            for (int vTx = 0; vTx < TILES_PER_SIDE; vTx++)
            {
                for (int vTy = 0; vTy < TILES_PER_SIDE; vTy++)
                {
                    for (int vTz = 0; vTz < TILES_PER_SIDE; vTz++)
                    {
                        ClipMap3dTileData vData;
                        if (!GetTileFromIndices(vLevel, vTx, vTy, vTz, out vData))
                            continue;

                        CreateCube(vData);
                        CreateDensities(vData);
                    }
                }
            }
        }
    }

    void CreateCube(ClipMap3dTileData pData)
    {
        GameObject vGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(vGo.GetComponent<Collider>());

        vGo.name = string.Format("L{0}_{1:F0}_{2:F0}_{3:F0}", pData.Level, pData.Position.x, pData.Position.y, pData.Position.z);
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
            Object.DestroyImmediate(mCubesRoot.gameObject);
            mCubesRoot = null;
        }

        if (mLevelMaterials != null)
        {
            for (int i = 0; i < mLevelMaterials.Length; i++)
            {
                if (mLevelMaterials[i] != null)
                    Object.DestroyImmediate(mLevelMaterials[i]);
            }
            mLevelMaterials = null;
        }

        if (mDensityMaterial != null)
        {
            Object.DestroyImmediate(mDensityMaterial);
            mDensityMaterial = null;
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
            Color vColor = LevelColor(i);
            vColor.a = mTransparency;

            if (vMats[i].HasProperty("_BaseColor"))
                vMats[i].SetColor("_BaseColor", vColor);
            else
                vMats[i].SetColor("_Color", vColor);

            vMats[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            vMats[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            vMats[i].SetInt("_ZWrite", 0);
            vMats[i].renderQueue = 3000;   // Transparent
            if (vMats[i].HasProperty("_Surface"))
                vMats[i].SetFloat("_Surface", 1f);   // Transparent (URP)
        }

        if (mDensityMaterial == null)
        {
            mDensityMaterial = new Material(vShader);
            Color vDensityColor = Color.white;
            vDensityColor.a = mTransparency;

            if (mDensityMaterial.HasProperty("_BaseColor"))
                mDensityMaterial.SetColor("_BaseColor", vDensityColor);
            else
                mDensityMaterial.SetColor("_Color", vDensityColor);

            mDensityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mDensityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mDensityMaterial.SetInt("_ZWrite", 0);
            mDensityMaterial.renderQueue = 3000;
            if (mDensityMaterial.HasProperty("_Surface"))
                mDensityMaterial.SetFloat("_Surface", 1f);
        }
        return vMats;
    }

    #endregion

    #region DENSIDAD

    /// <summary>
    /// Crea puntos de muestreo alineados con los bordes del cubo.
    /// Muestreo típico de chunk: N + 1 por dimensión (no en el centro de la celda).
    /// </summary>
    void CreateDensities(ClipMap3dTileData pData)
    {
        float vSize = pData.Size;
        float vStep = vSize / DENSITY_RES;

        Vector3 vHalf = new Vector3(vSize * 0.5f, vSize * 0.5f, vSize * 0.5f);
        Vector3 vMin = pData.Position - vHalf;

        for (int vX = 0; vX <= DENSITY_RES; vX++)
        {
            for (int vY = 0; vY <= DENSITY_RES; vY++)
            {
                for (int vZ = 0; vZ <= DENSITY_RES; vZ++)
                {
                    Vector3 vOffset = new Vector3(
                        vX * vStep,
                        vY * vStep,
                        vZ * vStep
                    );
                    Vector3 vPos = vMin + vOffset;

                    GameObject vSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(vSphere.GetComponent<Collider>());

                    vSphere.name = string.Format(
                        "D_L{0}_{1}_{2}_{3}",
                        pData.Level,
                        vX,
                        vY,
                        vZ
                    );
                    vSphere.transform.SetParent(mCubesRoot, false);
                    vSphere.transform.localPosition = vPos;

                    float vSphereScale = vStep * 0.25f;
                    vSphere.transform.localScale = new Vector3(
                        vSphereScale,
                        vSphereScale,
                        vSphereScale
                    );

                    MeshRenderer vRenderer = vSphere.GetComponent<MeshRenderer>();
                    vRenderer.sharedMaterial = mDensityMaterial;
                }
            }
        }
    }

    #endregion

    #region FÓRMULAS DEL PAPER (3D)

    /// <summary>
    /// Paper: scale = 2^l, base = snapped − 2×tile_size, tile_bl = base + (tx,ty,tz)×tile_size.
    /// Cáscara: omitir centro 2×2×2 → frontera interior = 1 cubo de grosor.
    /// Ratio 1:4 entre niveles (scale 1, 2, 4).
    /// </summary>
    bool GetTileFromIndices(int pLevel, int pTx, int pTy, int pTz, out ClipMap3dTileData pData)
    {
        pData = default(ClipMap3dTileData);

        int vScale = 1 << pLevel;
        float vTileSize = TILE_RES * vScale * BASE_SIZE;
        float vSnapped = 0f;
        float vBase = vSnapped - 2f * vTileSize;

        float vBlX = vBase + pTx * vTileSize;
        float vBlY = vBase + pTy * vTileSize;
        float vBlZ = vBase + pTz * vTileSize;

        pData.Position = new Vector3(
            vBlX + vTileSize * 0.5f,
            vBlY + vTileSize * 0.5f,
            vBlZ + vTileSize * 0.5f
        );
        pData.Level = pLevel;
        pData.Size = vTileSize;

        // Cáscara: omitir centro 2×2×2. Frontera interior = 1 tile de grosor.
        bool vInShell = (pTx * (3 - pTx) == 0) || (pTy * (3 - pTy) == 0) || (pTz * (3 - pTz) == 0);
        return vInShell;
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
            case 3: return Color.blue;
            case 4: return new Color(1f, 0.5f, 0f);   // Naranja
        }
        return Color.white;
    }

    #endregion

    #region ESTRUCTURA

    struct ClipMap3dTileData
    {
        public Vector3 Position;
        public int Level;
        public float Size;
    }

    #endregion
}
