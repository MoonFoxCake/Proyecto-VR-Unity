using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Maqueta en exhibicion: flota (y si "Girar" esta activo, gira) en su lugar. Se puede agarrar (mano o rayo)
/// para verla de cerca; con las dos manos tambien se puede agrandar o achicar.
/// Al soltarla se queda flotando donde quedo y, despues de "Tiempo Regreso" segundos,
/// vuelve sola a la exhibicion (posicion, giro y tamano originales).
/// La crea Tools > VR > Maqueta (conjunto completo, quieto; o una sola casa, girando).
/// Metodo publico para botones: RegresarAhora().
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class MaquetaExhibicion : MonoBehaviour
{
    [Header("Exhibicion")]
    public bool girar = true;
    [Tooltip("Grados por segundo.")]
    public float velocidadGiro = 12f;
    [Tooltip("Cuanto sube y baja al flotar, en metros.")]
    public float flotacion = 0.02f;
    [Tooltip("Segundos de una subida y bajada completa.")]
    public float periodoFlotacion = 4f;

    [Header("Regreso")]
    [Tooltip("Segundos que espera despues de soltarla antes de volver a la exhibicion.")]
    public float tiempoRegreso = 8f;
    [Tooltip("Segundos que tarda en volver.")]
    public float duracionRegreso = 1.5f;

    [Tooltip("De que se genero (lo usa la herramienta para reemplazar la maqueta correcta).")]
    [HideInInspector] public string origen = "";
    [Tooltip("Modelo real del que salio la maqueta (para ubicar al jugador en ella).")]
    [HideInInspector] public Transform fuente;
    [Tooltip("Punto del modelo real (alineado, en metros) que quedo en el origen de la maqueta.")]
    [HideInInspector] public Vector3 origenLocal;

    [Header("Eventos")]
    public UnityEvent alAgarrar = new UnityEvent();
    public UnityEvent alEmpezarRegreso = new UnityEvent();
    public UnityEvent alVolverAExhibicion = new UnityEvent();

    public bool EnExhibicion => estado == Estado.Exhibicion;
    public bool Sostenida => estado == Estado.Agarrada;
    public bool Regresando => estado == Estado.Regresando;
    public XRGrabInteractable Grab => grab != null ? grab : (grab = GetComponent<XRGrabInteractable>());

    /// <summary>Convierte un punto del mundo real (la casa de verdad) a coordenadas locales de la maqueta.</summary>
    public bool RealALocal(Vector3 mundo, out Vector3 local)
    {
        local = Vector3.zero;
        if (fuente == null) return false;
        local = Quaternion.Inverse(fuente.rotation) * (mundo - fuente.position) - origenLocal;
        return true;
    }

    enum Estado { Exhibicion, Agarrada, Suelta, Regresando }

    XRGrabInteractable grab;
    Estado estado = Estado.Exhibicion;
    Vector3 posExhibicion, escalaExhibicion;
    Quaternion rotBase;
    float giro, fase, soltadaEn, progreso;
    Vector3 posDesde, escalaDesde;
    Quaternion rotDesde;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        posExhibicion = transform.position;
        rotBase = transform.rotation;
        escalaExhibicion = transform.localScale;
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnAgarrar);
        grab.selectExited.AddListener(OnSoltar);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnAgarrar);
        grab.selectExited.RemoveListener(OnSoltar);
    }

    void OnAgarrar(SelectEnterEventArgs args)
    {
        if (estado != Estado.Agarrada) alAgarrar.Invoke();
        estado = Estado.Agarrada;
    }

    void OnSoltar(SelectExitEventArgs args)
    {
        if (grab.isSelected) return; // la sigue sosteniendo la otra mano
        estado = Estado.Suelta;
        soltadaEn = Time.time;
    }

    /// <summary>Vuelve a la exhibicion sin esperar.</summary>
    public void RegresarAhora()
    {
        if (estado == Estado.Agarrada || estado == Estado.Regresando) return;
        EmpezarRegreso();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        switch (estado)
        {
            case Estado.Exhibicion:
                if (girar) giro = Mathf.Repeat(giro + velocidadGiro * dt, 360f);
                fase += dt;
                transform.SetPositionAndRotation(PosicionExhibicion(), RotacionExhibicion());
                break;

            case Estado.Suelta:
                if (Time.time - soltadaEn >= tiempoRegreso) EmpezarRegreso();
                break;

            case Estado.Regresando:
                progreso += duracionRegreso > 0f ? dt / duracionRegreso : 1f;
                float s = Mathf.SmoothStep(0f, 1f, progreso);
                transform.SetPositionAndRotation(Vector3.Lerp(posDesde, posExhibicion, s),
                                                 Quaternion.Slerp(rotDesde, RotacionExhibicion(), s));
                transform.localScale = Vector3.Lerp(escalaDesde, escalaExhibicion, s);
                if (progreso >= 1f)
                {
                    fase = 0f; // la flotacion arranca desde la posicion de exhibicion, sin salto
                    estado = Estado.Exhibicion;
                    alVolverAExhibicion.Invoke();
                }
                break;
        }
    }

    void EmpezarRegreso()
    {
        posDesde = transform.position;
        rotDesde = transform.rotation;
        escalaDesde = transform.localScale;
        progreso = 0f;
        estado = Estado.Regresando;
        alEmpezarRegreso.Invoke();
    }

    Vector3 PosicionExhibicion()
    {
        float periodo = Mathf.Max(0.1f, periodoFlotacion);
        return posExhibicion + Vector3.up * (Mathf.Sin(fase * 2f * Mathf.PI / periodo) * flotacion);
    }

    Quaternion RotacionExhibicion() => Quaternion.AngleAxis(giro, Vector3.up) * rotBase;
}
