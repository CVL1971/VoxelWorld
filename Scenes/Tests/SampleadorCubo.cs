using UnityEngine;

/// <summary>
/// Cubo 10×10×10 en (0,0,0) semitransparente naranja y muestreo en vértices (9 por dimensión, 8+1).
/// Segundo cubo verde 5×5×5 apilado sobre el naranja (1/8 del volumen), compartiendo frontera y esquina.
/// Ejecuta en tiempo de diseño (ExecuteInEditMode). Estructura tipo clipmap: un solo root bajo el cual cubos y samples.
/// </summary>
[ExecuteInEditMode]
public class SampleadorCubo : MonoBehaviour
{
    const float CUBE_SIZE = 10f;
    const float GREEN_CUBE_SIZE = 5f;   // Volumen 1/8 del naranja (10³/8 = 125 = 5³)
    const int CELDAS_POR_EJE = 8;
    const int SAMPLES_PER_AXIS = CELDAS_POR_EJE + 1;   // 8 celdas + 1 (vértices en los bordes)
    const float TRANSPARENCY = 0.5f;

    const string CONTENIDO_ROOT_NAME = "ContenidoCubo";
    const string CUBE_NAME = "CuboCentral";
    const string CUBE_VERDE_NAME = "CuboVerde";
    const string SPHERES_ROOT_NAME = "Samples";

    Transform mContenidoRoot;
    Transform mCubeTransform;
    Transform mCubeVerdeTransform;
    Transform mSpheresRoot;
    Material mCubeMaterial;
    Material mCubeVerdeMaterial;
    Material mSphereMaterial;

    #region LIFECYCLE

    void OnEnable()
    {
        DestruirTodo();
        mContenidoRoot = new GameObject(CONTENIDO_ROOT_NAME).transform;
        mContenidoRoot.SetParent(transform, false);
        CrearCubo();
        CrearCuboVerde();
        mSpheresRoot = new GameObject(SPHERES_ROOT_NAME).transform;
        mSpheresRoot.SetParent(mContenidoRoot, false);
        SamplearCuboEnBordes();
        SamplearCuboVerdeEnBordes();
    }

    void OnDisable()
    {
        DestruirTodo();
    }

    #endregion

    #region CUBO

    void CrearCubo()
    {
        GameObject vCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(vCube.GetComponent<Collider>());
        vCube.name = CUBE_NAME;
        vCube.transform.SetParent(mContenidoRoot, false);
        vCube.transform.localPosition = Vector3.zero;
        vCube.transform.localScale = new Vector3(CUBE_SIZE, CUBE_SIZE, CUBE_SIZE);
        vCube.transform.localRotation = Quaternion.identity;
        mCubeTransform = vCube.transform;

        mCubeMaterial = CrearMaterialNaranja();
        MeshRenderer vRenderer = vCube.GetComponent<MeshRenderer>();
        vRenderer.sharedMaterial = mCubeMaterial;
    }

    Material CrearMaterialNaranja()
    {
        Shader vShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        Material vMat = new Material(vShader);
        Color vColor = new Color(1f, 0.5f, 0f);
        vColor.a = TRANSPARENCY;

        if (vMat.HasProperty("_BaseColor"))
            vMat.SetColor("_BaseColor", vColor);
        else
            vMat.SetColor("_Color", vColor);

        vMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        vMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        vMat.SetInt("_ZWrite", 0);
        vMat.renderQueue = 3000;
        if (vMat.HasProperty("_Surface"))
            vMat.SetFloat("_Surface", 1f);
        return vMat;
    }

    void CrearCuboVerde()
    {
        GameObject vCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(vCube.GetComponent<Collider>());
        vCube.name = CUBE_VERDE_NAME;
        vCube.transform.SetParent(mContenidoRoot, false);
        float vMitadNaranja = CUBE_SIZE * 0.5f;
        float vMitadVerde = GREEN_CUBE_SIZE * 0.5f;
        vCube.transform.localPosition = new Vector3(vMitadVerde, vMitadNaranja + vMitadVerde, vMitadVerde);
        vCube.transform.localScale = new Vector3(GREEN_CUBE_SIZE, GREEN_CUBE_SIZE, GREEN_CUBE_SIZE);
        vCube.transform.localRotation = Quaternion.identity;
        mCubeVerdeTransform = vCube.transform;

        mCubeVerdeMaterial = CrearMaterialVerde();
        MeshRenderer vRenderer = vCube.GetComponent<MeshRenderer>();
        vRenderer.sharedMaterial = mCubeVerdeMaterial;
    }

    Material CrearMaterialVerde()
    {
        Shader vShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        Material vMat = new Material(vShader);
        Color vColor = Color.green;
        vColor.a = TRANSPARENCY;

        if (vMat.HasProperty("_BaseColor"))
            vMat.SetColor("_BaseColor", vColor);
        else
            vMat.SetColor("_Color", vColor);

        vMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        vMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        vMat.SetInt("_ZWrite", 0);
        vMat.renderQueue = 3000;
        if (vMat.HasProperty("_Surface"))
            vMat.SetFloat("_Surface", 1f);
        return vMat;
    }

    #endregion

    #region MUESTREO

    /// <summary>
    /// Crea una esfera blanca en las coordenadas de entrada (espacio local del objeto).
    /// </summary>
    public void CrearEsferaEnCoordenadas(Vector3 pCoordenadas)
    {
        GameObject vSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(vSphere.GetComponent<Collider>());
        vSphere.name = string.Format("S_{0:F2}_{1:F2}_{2:F2}", pCoordenadas.x, pCoordenadas.y, pCoordenadas.z);
        vSphere.transform.SetParent(mSpheresRoot, false);
        vSphere.transform.localPosition = pCoordenadas;

        float vRadio = (CUBE_SIZE / CELDAS_POR_EJE) * 0.2f;
        vSphere.transform.localScale = new Vector3(vRadio, vRadio, vRadio);

        if (mSphereMaterial == null)
            mSphereMaterial = CrearMaterialEsfera();
        MeshRenderer vRenderer = vSphere.GetComponent<MeshRenderer>();
        vRenderer.sharedMaterial = mSphereMaterial;
    }

    Material CrearMaterialEsfera()
    {
        Shader vShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        Material vMat = new Material(vShader);
        Color vColor = Color.white;
        vColor.a = TRANSPARENCY;

        if (vMat.HasProperty("_BaseColor"))
            vMat.SetColor("_BaseColor", vColor);
        else
            vMat.SetColor("_Color", vColor);

        vMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        vMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        vMat.SetInt("_ZWrite", 0);
        vMat.renderQueue = 3000;
        if (vMat.HasProperty("_Surface"))
            vMat.SetFloat("_Surface", 1f);
        return vMat;
    }

    /// <summary>
    /// Muestrea un cubo en los bordes: 9 puntos por dimensión (8 celdas + 1), alineados con las caras.
    /// pMin: esquina mínima del cubo; pSize: arista del cubo.
    /// </summary>
    void SamplearCuboConMinYSize(Vector3 pMin, float pSize)
    {
        float vPaso = pSize / CELDAS_POR_EJE;
        for (int vX = 0; vX < SAMPLES_PER_AXIS; vX++)
        {
            for (int vY = 0; vY < SAMPLES_PER_AXIS; vY++)
            {
                for (int vZ = 0; vZ < SAMPLES_PER_AXIS; vZ++)
                {
                    Vector3 vCoords = new Vector3(
                        pMin.x + vX * vPaso,
                        pMin.y + vY * vPaso,
                        pMin.z + vZ * vPaso
                    );
                    CrearEsferaEnCoordenadas(vCoords);
                }
            }
        }
    }

    void SamplearCuboEnBordes()
    {
        float vMitad = CUBE_SIZE * 0.5f;
        Vector3 vMinNaranja = new Vector3(-vMitad, -vMitad, -vMitad);
        SamplearCuboConMinYSize(vMinNaranja, CUBE_SIZE);
    }

    void SamplearCuboVerdeEnBordes()
    {
        float vMitadNaranja = CUBE_SIZE * 0.5f;
        Vector3 vMinVerde = new Vector3(0f, vMitadNaranja, 0f);
        SamplearCuboConMinYSize(vMinVerde, GREEN_CUBE_SIZE);
    }

    #endregion

    #region LIMPIEZA

    void DestruirTodo()
    {
        if (mContenidoRoot != null)
        {
            Object.DestroyImmediate(mContenidoRoot.gameObject);
            mContenidoRoot = null;
            mCubeTransform = null;
            mCubeVerdeTransform = null;
            mSpheresRoot = null;
        }

        if (mCubeMaterial != null)
        {
            Object.DestroyImmediate(mCubeMaterial);
            mCubeMaterial = null;
        }

        if (mCubeVerdeMaterial != null)
        {
            Object.DestroyImmediate(mCubeVerdeMaterial);
            mCubeVerdeMaterial = null;
        }

        if (mSphereMaterial != null)
        {
            Object.DestroyImmediate(mSphereMaterial);
            mSphereMaterial = null;
        }
    }

    #endregion
}
