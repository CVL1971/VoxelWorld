using UnityEngine;

public class FPSCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float mMoveSpeed = 10f;
    [SerializeField] float mFastMultiplier = 3f;

    [Header("Mouse")]
    [SerializeField] float mMouseSensitivity = 3f;
    [SerializeField] float mMaxLookAngle = 85f;

    [Header("Terrain Interaction")]
    [SerializeField] private World mWorld;
    [SerializeField] private float mInteractionRange = 100f;
    [SerializeField] private Color mRayColor = Color.green;

    [Header("Visual Reticle (Exact Point)")]
    [SerializeField] private float mReticleScale = 0.2f;     // Tamaño del punto de mira 3D
    [SerializeField] private Material mReticleMaterial;      // Material para la esfera

    private GameObject mReticleSphere;
    private string mLastHitName = "";
    private Vector3 mLastHitPos = Vector3.zero; // Para evitar spam de coordenadas

    float mYaw;
    float mPitch;

    bool mNavigationMode = true;

    // Persistencia
    const string PosXKey = "FPSCam_PosX";
    const string PosYKey = "FPSCam_PosY";
    const string PosZKey = "FPSCam_PosZ";
    const string YawKey = "FPSCam_Yaw";
    const string PitchKey = "FPSCam_Pitch";
    const string HasSavedKey = "FPSCam_HasSaved";

    void Start()
    {
        //LoadView();
        EnterNavigationMode();

        if (mWorld == null)
        {
            mWorld = Object.FindFirstObjectByType<World>();
        }

        CreateReticle();
    }

    void Update()
    {
        HandleModeSwitch();

        // Actualizamos la retícula y el rayo de depuración
        UpdateInteractionVisuals();

        if (!mNavigationMode)
            return;

        ForceCursorLocked();

        HandleMouseLook();
        HandleMovement();

        if (Input.GetMouseButtonDown(0))
        {
            SaveView();
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryModifyTerrain();
        }
    }

    /// <summary>
    /// Crea la esfera única que actúa como puntero 3D.
    /// </summary>
    private void CreateReticle()
    {
        mReticleSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mReticleSphere.name = "Voxel_Reticle_Cursor";

        // Eliminamos el colisionador para que no interfiera con los Raycasts
        Destroy(mReticleSphere.GetComponent<SphereCollider>());

        mReticleSphere.transform.localScale = Vector3.one * mReticleScale;

        if (mReticleMaterial != null)
        {
            mReticleSphere.GetComponent<MeshRenderer>().material = mReticleMaterial;
        }
        else
        {
            // Color por defecto si no hay material
            mReticleSphere.GetComponent<MeshRenderer>().material.color = mRayColor;
        }
    }

    /// <summary>
    /// Gestiona el Raycast constante, posiciona la esfera en el punto exacto de impacto
    /// y loguea las coordenadas globales sin saturar el sistema.
    /// </summary>
    private void UpdateInteractionVisuals()
    {
        if (mReticleSphere == null) return;

        // Rayo perfectamente paralelo a la cámara
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        bool hasHit = Physics.Raycast(ray, out hit, mInteractionRange);
        string currentHitName = hasHit ? hit.collider.name : "Aire/Vacio";

        // 1. Dibujar Rayo Debug (Parallel)
        Debug.DrawRay(transform.position, transform.forward * mInteractionRange, mRayColor);

        // 2. Posicionar Retícula en el punto exacto
        if (hasHit)
        {
            mReticleSphere.SetActive(true);
            mReticleSphere.transform.position = hit.point;

            // 3. Sistema de Log de Coordenadas Globales
            // Solo imprimimos si cambiamos de objeto o si la posición ha cambiado significativamente (> 0.5 unidades)
            if (currentHitName != mLastHitName || Vector3.Distance(hit.point, mLastHitPos) > 0.5f)
            {
                //Debug.Log($"<color=white>[HIT]</color> Posición Global: <b>{hit.point.ToString("F2")}</b> | Objeto: {currentHitName}");

                mLastHitName = currentHitName;
                mLastHitPos = hit.point;
            }
        }
        else
        {
            if (mReticleSphere.activeSelf)
            {
                mReticleSphere.SetActive(false);
                //Debug.Log("<color=grey>[OUT]</color> Fuera de rango / Aire.");
                mLastHitName = "Aire/Vacio";
            }
        }
    }

    private void TryModifyTerrain()
    {
        if (mWorld == null) return;

        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, mInteractionRange))
        {
            //Debug.Log($"<color=red>[ACCIÓN]</color> Modificando voxel en coordenadas: {hit.point}");
            mWorld.ExecuteModification(hit.point, hit.normal, 0);
        }
    }

    // =========================
    // Modos de cursor y Control
    // =========================

    void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) ExitNavigationMode();
        if (Input.GetMouseButtonDown(1) && !mNavigationMode) EnterNavigationMode();
    }

    void EnterNavigationMode()
    {
        mNavigationMode = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (mReticleSphere != null) mReticleSphere.SetActive(true);
    }

    void ExitNavigationMode()
    {
        mNavigationMode = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (mReticleSphere != null) mReticleSphere.SetActive(false);
    }

    void ForceCursorLocked()
    {
        if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
        if (Cursor.visible) Cursor.visible = false;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mMouseSensitivity * 100f * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mMouseSensitivity * 100f * Time.deltaTime;
        mYaw += mouseX;
        mPitch -= mouseY;
        mPitch = Mathf.Clamp(mPitch, -mMaxLookAngle, mMaxLookAngle);
        transform.rotation = Quaternion.Euler(mPitch, mYaw, 0f);
    }

    void HandleMovement()
    {
        float speed = mMoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? mFastMultiplier : 1f);
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl)) move += Vector3.down;
        transform.position += move * speed * Time.deltaTime;
    }

    // =========================
    // Persistencia
    // =========================

    void SaveView()
    {
        PlayerPrefs.SetFloat(PosXKey, transform.position.x);
        PlayerPrefs.SetFloat(PosYKey, transform.position.y);
        PlayerPrefs.SetFloat(PosZKey, transform.position.z);
        PlayerPrefs.SetFloat(YawKey, mYaw);
        PlayerPrefs.SetFloat(PitchKey, mPitch);
        PlayerPrefs.SetInt(HasSavedKey, 1);
        PlayerPrefs.Save();
        Debug.Log("FPSCamera: vista guardada");
    }

    void LoadView()
    {
        if (PlayerPrefs.GetInt(HasSavedKey, 0) == 0) return;
        transform.position = new Vector3(PlayerPrefs.GetFloat(PosXKey), PlayerPrefs.GetFloat(PosYKey), PlayerPrefs.GetFloat(PosZKey));
        mYaw = PlayerPrefs.GetFloat(YawKey);
        mPitch = PlayerPrefs.GetFloat(PitchKey);
        transform.rotation = Quaternion.Euler(mPitch, mYaw, 0f);
    }
}


