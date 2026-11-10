using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Medidor de distancias REALES. Agarralo y usa el gatillo (Activate):
///   1er gatillo: marca el punto A.
///   2do gatillo: marca el punto B y fija la medicion.
///   3er gatillo: borra todo.
/// Dos modos; se cambian con el boton B/Y del control que lo sostiene (L en el teclado):
///   - Laser: un laser sale de la punta y los puntos se marcan en la superficie que toca
///     (pared, piso, mueble), asi puedes medir de lejos. Si no apunta a nada, el punto queda en la punta.
///   - Punta: como antes, el punto se marca exactamente donde esta la punta.
/// Los puntos se pegan a lo que tocan: si marcas una puerta o una maqueta y se mueve o gira,
/// la medicion la sigue.
/// Maquetas: si A y B estan en la misma maqueta, muestra la medida real de la casa
/// (y abajo, lo que mide en la maqueta). En cualquier otro caso, mide en metros reales.
/// Tambien muestra cuanto es horizontal y cuanto vertical.
/// Los marcadores, las lineas y el texto se crean solos al usarlo.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TapeMeasure : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punta del medidor; el laser sale hacia su eje Z. Si queda vacio usa este mismo objeto.")]
    public Transform tip;
    public Material lineMaterial;
    public Material markerMaterial;

    [Header("Laser")]
    [Tooltip("Modo con el que empieza. Se cambia en juego con el Boton Modo.")]
    public bool usarLaser = true;
    [Tooltip("Cambia entre modo laser y modo punta. Por defecto: B/Y del control que lo sostiene, o L.")]
    public InputActionProperty botonModo = new InputActionProperty(AccionModoPorDefecto());
    [Tooltip("Metros maximos del laser.")]
    public float alcanceLaser = 10f;
    public LayerMask capasLaser = ~0;

    [Header("Apariencia")]
    public float lineWidth = 0.004f;
    public float markerSize = 0.012f;
    public float labelScale = 0.01f;
    public int labelFontSize = 36;
    public Color labelColor = new Color(1f, 0.92f, 0.2f);
    [Tooltip("El texto crece con la distancia para que se lea de lejos.")]
    public bool textoSeAgrandaDeLejos = true;
    [Tooltip("Cuanto se adelanta el texto hacia el jugador, en metros, para no meterse en la pared que mide.")]
    public float separacionTexto = 0.1f;

    [Header("Vibracion al marcar")]
    [Range(0f, 1f)] public float vibracion = 0.35f;

    // Un punto marcado: pegado a un objeto que se mueve (puerta, maqueta) o fijo en el mundo.
    struct Punto
    {
        public Transform ancla;
        public Vector3 local;
        public MaquetaExhibicion maqueta;
        public Vector3 Mundo => ancla != null ? ancla.TransformPoint(local) : local;
    }

    enum State { Idle, PlacedA, PlacedB }
    State state = State.Idle;

    XRGrabInteractable grab;
    Collider[] propios;
    readonly RaycastHit[] golpes = new RaycastHit[16];
    bool sostenido;
    IXRSelectInteractor mano;

    GameObject root;
    Transform markerA, markerB, puntoLaser;
    LineRenderer line, laser;
    TextMeshPro label, avisoModo;
    float avisoHasta;
    Punto puntoA, puntoB;

    static readonly System.Collections.Generic.List<TapeMeasure> activos = new System.Collections.Generic.List<TapeMeasure>();

    /// <summary>True si algun medidor esta en esa mano (su B/Y es del medidor, no del menu radial).</summary>
    public static bool SostenidoConMano(bool derecha)
    {
        var buscada = derecha ? InteractorHandedness.Right : InteractorHandedness.Left;
        foreach (var t in activos)
            if (t.sostenido && t.mano is XRBaseInteractor i && i.handedness == buscada) return true;
        return false;
    }

    static InputAction AccionModoPorDefecto()
    {
        var accion = new InputAction("Modo medidor", InputActionType.Button);
        accion.AddBinding("<XRController>{RightHand}/secondaryButton");
        accion.AddBinding("<XRController>{LeftHand}/secondaryButton");
        accion.AddBinding("<Keyboard>/l");
        return accion;
    }

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (tip == null) tip = transform;
        propios = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        grab.activated.AddListener(OnActivated);
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
        activos.Add(this);
        var accion = botonModo.action;
        if (accion != null)
        {
            accion.performed += OnBotonModo;
            if (botonModo.reference == null) accion.Enable();
        }
    }

    void OnDisable()
    {
        grab.activated.RemoveListener(OnActivated);
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        activos.Remove(this);
        var accion = botonModo.action;
        if (accion != null)
        {
            accion.performed -= OnBotonModo;
            if (botonModo.reference == null) accion.Disable();
        }
    }

    // B/Y: solo cuenta el boton del control que sostiene el medidor (el otro queda libre).
    void OnBotonModo(InputAction.CallbackContext ctx)
    {
        if (!sostenido) return;
        if (mano is XRBaseInteractor interactor && ctx.control != null)
        {
            var usos = ctx.control.device.usages;
            bool izq = usos.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand);
            bool der = usos.Contains(UnityEngine.InputSystem.CommonUsages.RightHand);
            if ((izq && interactor.handedness == InteractorHandedness.Right) ||
                (der && interactor.handedness == InteractorHandedness.Left)) return;
        }
        usarLaser = !usarLaser;
        EnsureObjects();
        avisoModo.text = usarLaser ? "Modo laser" : "Modo punta";
        avisoHasta = Time.time + 1.2f;
        Vibrar(mano, 0.6f);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }

    // ------------------------------------------------------------ entrada

    void OnActivated(ActivateEventArgs args)
    {
        switch (state)
        {
            case State.Idle:
                EnsureObjects();
                puntoA = Marcar();
                markerB.gameObject.SetActive(false);
                markerA.gameObject.SetActive(true);
                line.enabled = true;
                label.gameObject.SetActive(true);
                state = State.PlacedA;
                Vibrar(args.interactorObject);
                break;

            case State.PlacedA:
                puntoB = Marcar();
                markerB.gameObject.SetActive(true);
                state = State.PlacedB;
                Vibrar(args.interactorObject);
                break;

            case State.PlacedB:
                Clear();
                Vibrar(args.interactorObject, 0.5f);
                break;
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            // Si guardas el medidor en el cinturon a mitad de una medicion, se cancela.
            if (state == State.PlacedA) Clear();
            sostenido = false;
            mano = null;
        }
        else
        {
            sostenido = true;
            mano = args.interactorObject;
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        sostenido = false;
        mano = null;
        foreach (var i in grab.interactorsSelecting)
            if (i != args.interactorObject && !(i is XRSocketInteractor)) { sostenido = true; mano = i; }
    }

    // ------------------------------------------------------------ cada cuadro

    void LateUpdate()
    {
        bool mostrarLaser = usarLaser && sostenido;
        Apuntar(out Vector3 apuntado, out Collider tocado);

        if (mostrarLaser || state != State.Idle) EnsureObjects();
        if (root == null) return;

        laser.enabled = mostrarLaser;
        puntoLaser.gameObject.SetActive(mostrarLaser && tocado != null);
        if (mostrarLaser)
        {
            laser.SetPosition(0, tip.position);
            // Sin superficie el punto es la punta, pero el laser se dibuja corto para ver hacia donde apunta.
            laser.SetPosition(1, tocado != null ? apuntado : tip.position + tip.forward * Mathf.Min(alcanceLaser, 1f));
            puntoLaser.position = apuntado;
        }

        if (state == State.PlacedA)
        {
            var vivo = CrearPunto(apuntado, tocado);
            Dibujar(puntoA, vivo);
        }
        else if (state == State.PlacedB) Dibujar(puntoA, puntoB);

        // Aviso de modo, sobre la punta, por un momento.
        bool aviso = Time.time < avisoHasta;
        if (avisoModo.gameObject.activeSelf != aviso) avisoModo.gameObject.SetActive(aviso);
        if (aviso)
        {
            avisoModo.transform.position = tip.position + Vector3.up * 0.06f;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = avisoModo.transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f) avisoModo.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }

    void Dibujar(Punto a, Punto b)
    {
        Vector3 pa = a.Mundo, pb = b.Mundo;
        markerA.position = pa;
        if (state == State.PlacedB) markerB.position = pb;
        line.SetPosition(0, pa);
        line.SetPosition(1, pb);

        label.text = Texto(a, b, pa, pb);
        Vector3 medio = (pa + pb) * 0.5f;

        var cam = Camera.main;
        if (cam == null)
        {
            label.transform.position = medio + Vector3.up * 0.04f;
            return;
        }
        Vector3 haciaJugador = cam.transform.position - medio;
        float distancia = haciaJugador.magnitude;
        float k = textoSeAgrandaDeLejos ? Mathf.Clamp(distancia / 1.2f, 1f, 6f) : 1f;
        // Un poco arriba y adelantado hacia el jugador (sin pasarlo), asi no se mete en la pared que mide.
        float adelante = Mathf.Min(separacionTexto * k, distancia * 0.5f);
        label.transform.position = medio + Vector3.up * (0.04f * k)
                                 + (distancia > 0.001f ? haciaJugador / distancia : Vector3.zero) * adelante;
        label.transform.localScale = Vector3.one * labelScale * k;
        Vector3 dir = label.transform.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f) label.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // ------------------------------------------------------------ medicion

    string Texto(Punto a, Punto b, Vector3 pa, Vector3 pb)
    {
        // Ambos puntos en la misma maqueta: su espacio local esta en metros reales de la casa
        // (asi la medida es correcta aunque la maqueta este agrandada o girando).
        if (a.maqueta != null && a.maqueta == b.maqueta)
        {
            var t = a.maqueta.transform;
            Vector3 d = t.InverseTransformPoint(pb) - t.InverseTransformPoint(pa);
            return FormatLength(d.magnitude) + " <size=55%>reales</size>" + Componentes(d) +
                   "\n<size=50%>en la maqueta: " + FormatLength(Vector3.Distance(pa, pb)) + "</size>";
        }
        Vector3 dm = pb - pa;
        return FormatLength(dm.magnitude) + Componentes(dm);
    }

    static string Componentes(Vector3 d)
    {
        float vert = Mathf.Abs(d.y);
        float horiz = new Vector2(d.x, d.z).magnitude;
        if (vert < 0.01f || horiz < 0.01f) return "";
        return "\n<size=50%>horizontal " + FormatLength(horiz) + "  ·  vertical " + FormatLength(vert) + "</size>";
    }

    static string FormatLength(float meters)
    {
        return meters >= 1f ? meters.ToString("F2") + " m" : (meters * 100f).ToString("F1") + " cm";
    }

    Punto Marcar()
    {
        Apuntar(out Vector3 p, out Collider c);
        return CrearPunto(p, c);
    }

    static Punto CrearPunto(Vector3 mundo, Collider c)
    {
        var punto = new Punto { local = mundo };
        if (c == null) return punto;
        punto.maqueta = c.GetComponentInParent<MaquetaExhibicion>();
        // Pegado a lo que se mueve: la maqueta entera, o el cuerpo (ej. la bisagra de una puerta).
        Transform ancla = punto.maqueta != null ? punto.maqueta.transform
                        : c.attachedRigidbody != null ? c.attachedRigidbody.transform : null;
        if (ancla != null)
        {
            punto.ancla = ancla;
            punto.local = ancla.InverseTransformPoint(mundo);
        }
        return punto;
    }

    // Donde cae el punto: en la superficie que toca el laser, o en la punta si no toca nada.
    void Apuntar(out Vector3 punto, out Collider tocado)
    {
        punto = tip.position;
        tocado = null;
        if (!usarLaser) return;

        // Arranca un poco detras de la punta: si la punta esta pegada a (o dentro de) una pared,
        // igual detecta esa pared y no la de atras.
        const float atras = 0.05f;
        Vector3 dir = tip.forward;
        int n = Physics.RaycastNonAlloc(tip.position - dir * atras, dir, golpes, alcanceLaser + atras, capasLaser, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = golpes[i].collider;
            if (EsPropio(c) || c is CharacterController) continue;
            if (golpes[i].distance < mejor)
            {
                mejor = golpes[i].distance;
                punto = golpes[i].point;
                tocado = c;
            }
        }
    }

    bool EsPropio(Collider c)
    {
        for (int i = 0; i < propios.Length; i++) if (propios[i] == c) return true;
        return false;
    }

    void Vibrar(IXRInteractor interactor, float factor = 1f)
    {
        if (vibracion > 0f && interactor is XRBaseInputInteractor entrada)
            entrada.SendHapticImpulse(vibracion * factor, 0.04f);
    }

    void Clear()
    {
        state = State.Idle;
        if (root == null) return;
        line.enabled = false;
        markerA.gameObject.SetActive(false);
        markerB.gameObject.SetActive(false);
        label.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ objetos visuales

    void EnsureObjects()
    {
        if (root != null) return;

        root = new GameObject("Medidor_Marcas");
        var lineMat = lineMaterial != null ? lineMaterial : FallbackMaterial(Color.yellow);
        var markMat = markerMaterial != null ? markerMaterial : FallbackMaterial(Color.red);

        line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = line.endWidth = lineWidth;
        line.material = lineMat;
        line.enabled = false;

        var goLaser = new GameObject("Medidor_Laser");
        goLaser.transform.SetParent(root.transform, false);
        laser = goLaser.AddComponent<LineRenderer>();
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.startWidth = laser.endWidth = lineWidth * 0.35f;
        laser.material = markMat;
        laser.enabled = false;

        markerA = CreateMarker("Marca_A", markerSize, markMat);
        markerB = CreateMarker("Marca_B", markerSize, markMat);
        puntoLaser = CreateMarker("Punto_Laser", markerSize * 0.6f, markMat);

        var go = new GameObject("Medidor_Texto");
        go.transform.SetParent(root.transform, false);
        go.transform.localScale = Vector3.one * labelScale;
        label = go.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = labelFontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = labelColor;
        label.richText = true;
        label.outlineWidth = 0.25f;               // borde oscuro: se lee sobre paredes claras
        label.outlineColor = new Color32(0, 0, 0, 255);
        go.SetActive(false);

        var goAviso = new GameObject("Medidor_Modo");
        goAviso.transform.SetParent(root.transform, false);
        goAviso.transform.localScale = Vector3.one * labelScale * 0.6f;
        avisoModo = goAviso.AddComponent<TextMeshPro>();
        avisoModo.alignment = TextAlignmentOptions.Center;
        avisoModo.fontSize = labelFontSize;
        avisoModo.fontStyle = FontStyles.Bold;
        avisoModo.color = Color.white;
        avisoModo.outlineWidth = 0.25f;
        avisoModo.outlineColor = new Color32(0, 0, 0, 255);
        goAviso.SetActive(false);
    }

    Transform CreateMarker(string markerName, float size, Material mat)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = markerName;
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(root.transform, false);
        s.transform.localScale = Vector3.one * size;
        s.GetComponent<MeshRenderer>().sharedMaterial = mat;
        s.SetActive(false);
        return s.transform;
    }

    static Material FallbackMaterial(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.color = c;
        return m;
    }
}
