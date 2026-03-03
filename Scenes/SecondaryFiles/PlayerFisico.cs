using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerFisicoPro : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private World mWorld; // Referencia al script World
    [SerializeField] private Camera mCam;   // Referencia manual a la cámara (opcional)
    
    [Header("Ajustes de Movimiento")]
    public float velocidadCaminar = 8f;
    public float velocidadVuelo = 15f;
    public float fuerzaSalto = 6f;
    public float sensibilidadMouse = 2f;

    [Header("Ajustes de Interacción")]
    public float rangoInteraccion = 50f;
    public Color colorRayoVigilancia = Color.cyan;

    private Rigidbody rb;
    private float rotVertical = 0f;
    private bool fisicaActivada = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Intentamos buscar la cámara si no se asignó en el inspector
        if (mCam == null) mCam = GetComponentInChildren<Camera>();
        if (mCam == null) mCam = Camera.main;

        CargarEstado();
    }

    void Start()
    {
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        DesactivarFisicas();
        Cursor.lockState = CursorLockMode.Locked;

        if (mWorld == null)
        {
            mWorld = Object.FindFirstObjectByType<World>();
        }
    }

    void Update()
    {
        ManejarRotacion();
        ManejarVueloFisica();

        // Dibujo constante del rayo en la Scene View (y Game View si Gizmos está ON)
        VisualizarRayoDebug();

        // Acción de destrucción al pulsar el botón izquierdo
        if (Input.GetMouseButtonDown(0))
        {
            EjecutarAccionTerreno(0); // 0 = Aire / Vacío
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Saltar();
        }
    }

    /// <summary>
    /// Dibuja una línea de depuración usando la cámara activa.
    /// </summary>
    private void VisualizarRayoDebug()
    {
        Camera camActiva = (mCam != null) ? mCam : Camera.main;
        if (camActiva == null) return;

        Vector3 inicio = camActiva.transform.position;
        Vector3 direccion = camActiva.transform.forward;
        
        Debug.DrawLine(inicio, inicio + direccion * rangoInteraccion, colorRayoVigilancia);
    }

    /// <summary>
    /// Dibuja el rayo y la esfera en el editor.
    /// </summary>
    private void OnDrawGizmos()
    {
        Camera camActiva = (mCam != null) ? mCam : GetComponentInChildren<Camera>();
        if (camActiva == null) camActiva = Camera.main;
        if (camActiva == null) return;

        Gizmos.color = colorRayoVigilancia;
        Vector3 inicio = camActiva.transform.position;
        Vector3 direccion = camActiva.transform.forward;
        Vector3 destino = inicio + direccion * rangoInteraccion;

        // Dibujamos la línea y la esfera para que sea visible
        Gizmos.DrawLine(inicio, destino);
        Gizmos.DrawWireSphere(destino, 0.3f);
    }

    private void ManejarRotacion()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensibilidadMouse;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadMouse;

        transform.Rotate(Vector3.up * mouseX);

        rotVertical -= mouseY;
        rotVertical = Mathf.Clamp(rotVertical, -90f, 90f);

        if (mCam != null)
        {
            mCam.transform.localEulerAngles = new Vector3(rotVertical, 0, 0);
        }
    }

    private void ManejarVueloFisica()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (fisicaActivada) DesactivarFisicas(); else ActivarFisicas();
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = (transform.forward * v + transform.right * h).normalized;

        if (fisicaActivada)
            rb.MovePosition(rb.position + dir * velocidadCaminar * Time.fixedDeltaTime);
        else
            transform.Translate(dir * velocidadVuelo * Time.deltaTime, Space.World);
    }

    private void EjecutarAccionTerreno(byte tipo)
    {
        if (mWorld == null) return;

        Camera camActiva = (mCam != null) ? mCam : Camera.main;
        if (camActiva == null) return;

        Ray ray = new Ray(camActiva.transform.position, camActiva.transform.forward);
        RaycastHit hit;

        //if (Physics.Raycast(ray, out hit, rangoInteraccion))
        //{
        //    mWorld.ExecuteModification(hit.point, hit.normal, tipo);
        //}
    }

    private void ActivarFisicas() { fisicaActivada = true; rb.isKinematic = false; rb.useGravity = true; }
    private void DesactivarFisicas() { fisicaActivada = false; rb.isKinematic = true; rb.useGravity = false; rb.linearVelocity = Vector3.zero; }
    private void Saltar() { if (Physics.Raycast(transform.position, Vector3.down, 1.3f)) rb.AddForce(Vector3.up * fuerzaSalto, ForceMode.Impulse); }

    public void GuardarEstado() {
        PlayerPrefs.SetFloat("pX", transform.position.x);
        PlayerPrefs.SetFloat("pY", transform.position.y);
        PlayerPrefs.SetFloat("pZ", transform.position.z);
        PlayerPrefs.Save();
    }
    private void CargarEstado() {
        if (PlayerPrefs.HasKey("pX")) transform.position = new Vector3(PlayerPrefs.GetFloat("pX"), PlayerPrefs.GetFloat("pY"), PlayerPrefs.GetFloat("pZ"));
    }
    private void OnDisable() => GuardarEstado();
    private void OnApplicationQuit() => GuardarEstado();
}
