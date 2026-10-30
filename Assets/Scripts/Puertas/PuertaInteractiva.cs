using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puerta con bisagra. La herramienta Tools > VR > Puertas arma la estructura
/// (Marco, Bisagra, Hoja) y llena los campos de "Estructura".
/// Se abre y se cierra al seleccionar la hoja (rayo o mano), igual que la lampara.
/// La puerta se abre alejandose del jugador para no atraparlo.
/// Metodos publicos para botones o el menu radial: Abrir(), Cerrar(), Alternar().
/// </summary>
public class PuertaInteractiva : MonoBehaviour
{
    [Header("Estructura (la llena la herramienta)")]
    public Transform bisagra;
    public Transform hoja;
    public XRSimpleInteractable interactable;
    [Tooltip("Direccion de la bisagra hacia el borde libre de la hoja, en el espacio local de este objeto.")]
    public Vector3 ladoLibreLocal = Vector3.right;
    [Tooltip("Eje del grosor de la hoja (perpendicular a ella), en el espacio local de este objeto.")]
    public Vector3 normalLocal = Vector3.forward;
    [HideInInspector] public Vector3 centroHojaLocal;

    [Header("Movimiento")]
    public float anguloApertura = 90f;
    [Tooltip("Grados por segundo.")]
    public float velocidad = 110f;
    [Tooltip("Si esta activo, la puerta siempre se abre hacia el lado contrario al jugador.")]
    public bool abrirAlejandoseDelJugador = true;
    public bool bloqueada = false;

    [Header("Sonido (opcional)")]
    public AudioSource fuente;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;

    [Header("Eventos")]
    public UnityEvent alAbrir = new UnityEvent();
    public UnityEvent alCerrar = new UnityEvent();

    public bool EstaAbierta { get; private set; }

    float angulo;
    float objetivo;

    void OnEnable()
    {
        if (interactable != null) interactable.selectEntered.AddListener(OnSeleccion);
    }

    void OnDisable()
    {
        if (interactable != null) interactable.selectEntered.RemoveListener(OnSeleccion);
    }

    void OnSeleccion(SelectEnterEventArgs args) => Alternar();

    void Update()
    {
        if (bisagra == null || Mathf.Approximately(angulo, objetivo)) return;
        angulo = Mathf.MoveTowards(angulo, objetivo, velocidad * Time.deltaTime);
        Aplicar();
    }

    public void Alternar()
    {
        if (EstaAbierta) Cerrar();
        else Abrir();
    }

    public void Abrir()
    {
        if (bloqueada || EstaAbierta || bisagra == null) return;
        objetivo = CalcularSigno() * anguloApertura;
        EstaAbierta = true;
        Sonar(sonidoAbrir);
        alAbrir.Invoke();
    }

    public void Cerrar()
    {
        if (!EstaAbierta) return;
        objetivo = 0f;
        EstaAbierta = false;
        Sonar(sonidoCerrar);
        alCerrar.Invoke();
    }

    void Aplicar()
    {
        bisagra.localRotation = Quaternion.Euler(0f, angulo, 0f);
    }

    void Sonar(AudioClip clip)
    {
        if (fuente != null && clip != null) fuente.PlayOneShot(clip);
    }

    // Decide hacia que lado girar: el borde libre debe alejarse del jugador.
    int CalcularSigno()
    {
        if (!abrirAlejandoseDelJugador) return 1;
        var cam = Camera.main;
        if (cam == null) return 1;

        Vector3 cabeza = transform.InverseTransformPoint(cam.transform.position);
        float lado = Vector3.Dot(cabeza - bisagra.localPosition, normalLocal);
        float cruz = ladoLibreLocal.z * normalLocal.x - ladoLibreLocal.x * normalLocal.z;
        if (Mathf.Abs(lado) < 0.0001f || Mathf.Abs(cruz) < 0.0001f) return 1;
        return (int)(-Mathf.Sign(lado) * Mathf.Sign(cruz));
    }

    // ---- Vista previa en el editor: clic derecho en el componente. Cierra la puerta al terminar.
    [ContextMenu("Vista previa: abierta")]
    void PreviaAbierta() { if (bisagra != null) { angulo = objetivo = anguloApertura; Aplicar(); } }

    [ContextMenu("Vista previa: cerrada")]
    void PreviaCerrada() { if (bisagra != null) { angulo = objetivo = 0f; Aplicar(); } }

    void OnDrawGizmosSelected()
    {
        if (bisagra == null) return;
        Gizmos.color = Color.yellow;
        Vector3 p = bisagra.position;
        Gizmos.DrawWireSphere(p, 0.05f);
        Gizmos.DrawLine(p, p + transform.TransformDirection(ladoLibreLocal.normalized) * 0.9f);
    }
}
