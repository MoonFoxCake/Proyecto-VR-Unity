using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Puerta con bisagra que se mueve como una puerta real: se agarra la hoja (mano o rayo)
/// y se empuja o se jala. La hoja sigue a la mano alrededor de la bisagra, nunca pasa
/// del tope del marco, y al soltarla sigue con el impulso hasta frenarse.
/// Si llega casi cerrada y lenta, el pestillo la cierra.
///
/// El lado hacia donde abre (aperturaLocal) sale de la geometria del modelo:
///   - Puertas interiores: el modelo las dibuja abiertas; abre hacia ese lado.
///   - Aluprof y otras: abre hacia la cara donde sobresalen las bisagras
///     (si no hay bisagras: hacia la cara donde la hoja queda a ras del marco).
/// Si queda en cero se detecta solo al iniciar. Para hornearlo en la escena:
/// Tools > VR > Puertas > 4. Si queda al reves: Tools > VR > Puertas > 5.
///
/// La herramienta Tools > VR > Puertas arma la estructura (Marco, Bisagra, Hoja).
/// Metodos publicos para botones o el menu radial: Abrir(), Cerrar(), Alternar().
/// </summary>
public class PuertaInteractiva : MonoBehaviour
{
    [Header("Estructura (la llena la herramienta)")]
    public Transform bisagra;
    public Transform hoja;
    [Tooltip("Si queda vacio se busca el hijo llamado 'Marco'.")]
    public Transform marco;
    public XRSimpleInteractable interactable;
    [Tooltip("Direccion de la bisagra hacia el borde libre de la hoja (puerta cerrada), en el espacio local de este objeto.")]
    public Vector3 ladoLibreLocal = Vector3.right;
    [Tooltip("Eje del grosor de la hoja (perpendicular a ella), en el espacio local de este objeto.")]
    public Vector3 normalLocal = Vector3.forward;
    [Tooltip("Lado del marco hacia el que abre la puerta (+/- Normal Local). En cero = se detecta solo al iniciar.")]
    public Vector3 aperturaLocal = Vector3.zero;
    [Tooltip("El lado lo eligio una herramienta (ventanas, portones) o se invirtio a mano: la deteccion automatica no lo cambia.")]
    public bool ladoManual = false;
    [HideInInspector] public Vector3 centroHojaLocal;

    [Header("Apertura")]
    [Tooltip("Angulo maximo de apertura, en grados.")]
    public float anguloApertura = 90f;
    [Tooltip("Puerta de vaiven: abre hacia los dos lados.")]
    public bool abreAmbosLados = false;
    public bool bloqueada = false;

    [Header("Sensacion al agarrarla")]
    [Tooltip("Que tan pegada sigue la hoja a la mano. Mas alto = mas rigida, mas bajo = mas pesada.")]
    public float seguimiento = 18f;
    [Tooltip("Grados por segundo maximos de la hoja.")]
    public float velocidadMaxima = 400f;
    [Tooltip("Al soltarla sigue girando con el impulso que le diste.")]
    public bool inercia = true;
    [Tooltip("Grados por segundo que pierde cada segundo cuando gira sola (friccion de la bisagra).")]
    public float frenado = 220f;
    [Tooltip("Cuanto rebota al chocar con el tope (0 = nada, 1 = todo).")]
    [Range(0f, 1f)] public float rebote = 0.15f;
    [Tooltip("Si queda a menos de estos grados y va lenta, el pestillo la cierra.")]
    public float anguloPestillo = 4f;

    [Header("Abrir / Cerrar por boton o menu")]
    [Tooltip("Grados por segundo al abrirse o cerrarse sola con Abrir()/Cerrar().")]
    public float velocidad = 110f;

    [Header("Sonido (opcional)")]
    public AudioSource fuente;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;

    [Header("Eventos")]
    public UnityEvent alAbrir = new UnityEvent();
    public UnityEvent alCerrar = new UnityEvent();

    public bool EstaAbierta { get; private set; }
    public float Angulo => angulo;

    float angulo;          // grados, 0 = cerrada
    float velocidadAng;    // grados por segundo
    float? objetivoAuto;   // Abrir()/Cerrar()

    IXRSelectInteractor mano;
    float anguloManoPrevio;
    bool tieneManoPrevia;
    float objetivoMano;

    // ------------------------------------------------------------ ciclo de vida

    void Awake()
    {
        if (marco == null) marco = transform.Find("Marco");
        if (interactable != null && !(interactable is AgarrePuerta))
            Debug.LogWarning("[Puertas] " + name + " usa el interactable viejo: el rayo no seguira a la puerta. Corre Tools > VR > Puertas > 4 y guarda la escena.", this);
        if (aperturaLocal.sqrMagnitude < 0.5f)
        {
            if (!DetectarLadoApertura(out string como))
                Debug.LogWarning("[Puertas] " + name + ": " + como, this);
        }
        if (bisagra != null)
        {
            angulo = Mathf.DeltaAngle(0f, bisagra.localEulerAngles.y);
            EstaAbierta = Mathf.Abs(angulo) > 1f;
        }
    }

    void OnEnable()
    {
        if (interactable == null) return;
        interactable.selectEntered.AddListener(OnAgarrar);
        interactable.selectExited.AddListener(OnSoltar);
    }

    void OnDisable()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnAgarrar);
        interactable.selectExited.RemoveListener(OnSoltar);
        mano = null;
    }

    void OnAgarrar(SelectEnterEventArgs args)
    {
        if (bloqueada) return;
        mano = args.interactorObject;
        objetivoAuto = null;
        objetivoMano = angulo;
        tieneManoPrevia = TryAnguloMano(out anguloManoPrevio);
    }

    void OnSoltar(SelectExitEventArgs args)
    {
        if (args.interactorObject != mano) return;
        mano = null;
        velocidadAng = inercia ? Mathf.Clamp(velocidadAng, -velocidadMaxima, velocidadMaxima) : 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (bisagra == null || dt <= 0f) return;

        ObtenerLimites(out float min, out float max);
        float previo = angulo;

        if (mano != null)
        {
            // Sigue el giro de la mano alrededor de la bisagra (cuadro a cuadro, sin saltos de 360).
            if (TryAnguloMano(out float am))
            {
                if (tieneManoPrevia) objetivoMano += Mathf.DeltaAngle(anguloManoPrevio, am);
                anguloManoPrevio = am;
                tieneManoPrevia = true;
            }
            else tieneManoPrevia = false;

            // Sin "cuerda": si empujas contra el tope, al regresar la puerta responde de inmediato.
            objetivoMano = Mathf.Clamp(objetivoMano, min, max);

            float t = 1f - Mathf.Exp(-seguimiento * dt);
            angulo = Mathf.MoveTowards(angulo, Mathf.Lerp(angulo, objetivoMano, t), velocidadMaxima * dt);
            velocidadAng = Mathf.Lerp(velocidadAng, (angulo - previo) / dt, 0.5f);
        }
        else if (objetivoAuto.HasValue)
        {
            angulo = Mathf.MoveTowards(angulo, objetivoAuto.Value, velocidad * dt);
            velocidadAng = 0f;
            if (Mathf.Approximately(angulo, objetivoAuto.Value)) objetivoAuto = null;
        }
        else if (velocidadAng != 0f)
        {
            angulo += velocidadAng * dt;
            velocidadAng = Mathf.MoveTowards(velocidadAng, 0f, frenado * dt);
        }

        // Topes: el marco (0) y la apertura maxima.
        if (angulo > max) { angulo = max; if (velocidadAng > 0f) velocidadAng = mano == null ? -velocidadAng * rebote : 0f; }
        if (angulo < min) { angulo = min; if (velocidadAng < 0f) velocidadAng = mano == null ? -velocidadAng * rebote : 0f; }

        // Pestillo: casi cerrada, suelta y lenta -> cerrada.
        if (mano == null && !objetivoAuto.HasValue && angulo != 0f
            && Mathf.Abs(angulo) < anguloPestillo && Mathf.Abs(velocidadAng) < 60f)
        {
            angulo = 0f;
            velocidadAng = 0f;
        }

        if (angulo != previo) Aplicar();
        ActualizarEstado();
    }

    // ------------------------------------------------------------ API publica

    public void Alternar()
    {
        if (EstaAbierta) Cerrar();
        else Abrir();
    }

    public void Abrir()
    {
        if (bloqueada || mano != null || bisagra == null) return;
        objetivoAuto = SignoApertura() * anguloApertura;
        velocidadAng = 0f;
    }

    public void Cerrar()
    {
        if (mano != null) return;
        objetivoAuto = 0f;
        velocidadAng = 0f;
    }

    /// <summary>
    /// Decide hacia que lado abre segun la geometria y lo guarda en aperturaLocal.
    /// Devuelve false (y deja +Normal Local) si no se pudo decidir.
    /// </summary>
    public bool DetectarLadoApertura(out string como)
    {
        Vector3 N = normalLocal.normalized;
        if (hoja == null || bisagra == null || N == Vector3.zero) { como = "puerta sin estructura."; return false; }
        if (marco == null) marco = transform.Find("Marco");

        // 1) Hoja modelada abierta (puertas interiores): la herramienta la giro theta para cerrarla.
        float theta = Mathf.DeltaAngle(0f, hoja.localEulerAngles.y);
        if (Mathf.Abs(theta) > 1f)
        {
            Vector3 libreAbierta = Quaternion.Euler(0f, -theta, 0f) * ladoLibreLocal.normalized;
            float lado = Vector3.Dot(libreAbierta, N);
            if (Mathf.Abs(lado) > 0.05f)
            {
                aperturaLocal = N * Mathf.Sign(lado);
                como = "por la pose abierta del modelo.";
                return true;
            }
        }

        // 2) Puerta modelada cerrada: mirar la hoja contra el marco.
        var mfH = hoja.GetComponent<MeshFilter>();
        var mfM = marco != null ? marco.GetComponent<MeshFilter>() : null;
        if (mfH == null || mfM == null || mfH.sharedMesh == null || mfM.sharedMesh == null
            || !mfH.sharedMesh.isReadable || !mfM.sharedMesh.isReadable)
        {
            aperturaLocal = N;
            como = "no pude leer los meshes de la hoja y el marco; asumi un lado. Si queda al reves usa 'Invertir lado de apertura'.";
            return false;
        }

        float escala = Mathf.Max(1e-5f, Mathf.Abs(transform.lossyScale.x));

        // Marco: profundidad a lo largo de N.
        Matrix4x4 aRaizMarco = transform.worldToLocalMatrix * marco.localToWorldMatrix;
        float mMin = float.MaxValue, mMax = float.MinValue;
        foreach (var v in mfM.sharedMesh.vertices)
        {
            float d = Vector3.Dot(aRaizMarco.MultiplyPoint3x4(v), N);
            mMin = Mathf.Min(mMin, d); mMax = Mathf.Max(mMax, d);
        }

        // Hoja (pose cerrada): sus caras grandes perpendiculares a N. Las manijas pesan poco.
        Matrix4x4 aRaizHoja = Matrix4x4.TRS(bisagra.localPosition, Quaternion.identity, bisagra.localScale)
                            * Matrix4x4.TRS(hoja.localPosition, hoja.localRotation, hoja.localScale);
        var mesh = mfH.sharedMesh;
        var vs = mesh.vertices;
        var tris = mesh.triangles;
        float paso = 0.002f / escala;
        var areaPorPlano = new Dictionary<int, float>();
        for (int k = 0; k + 2 < tris.Length; k += 3)
        {
            Vector3 a = aRaizHoja.MultiplyPoint3x4(vs[tris[k]]);
            Vector3 b = aRaizHoja.MultiplyPoint3x4(vs[tris[k + 1]]);
            Vector3 c = aRaizHoja.MultiplyPoint3x4(vs[tris[k + 2]]);
            Vector3 n = Vector3.Cross(b - a, c - a);
            float doble = n.magnitude;
            if (doble < 1e-12f || Mathf.Abs(Vector3.Dot(n / doble, N)) < 0.95f) continue;
            int clave = Mathf.RoundToInt(Vector3.Dot(a, N) / paso);
            areaPorPlano.TryGetValue(clave, out float acc);
            areaPorPlano[clave] = acc + doble * 0.5f;
        }
        if (areaPorPlano.Count == 0)
        {
            aperturaLocal = N;
            como = "la hoja no tiene caras planas; asumi un lado. Si queda al reves usa 'Invertir lado de apertura'.";
            return false;
        }
        float mayor = 0f;
        foreach (var kv in areaPorPlano) mayor = Mathf.Max(mayor, kv.Value);
        float hMin = float.MaxValue, hMax = float.MinValue;
        foreach (var kv in areaPorPlano)
        {
            if (kv.Value < mayor * 0.25f) continue;
            float d = kv.Key * paso;
            hMin = Mathf.Min(hMin, d); hMax = Mathf.Max(hMax, d);
        }

        // 2a) Bisagras: los nudillos sobresalen de la cara hacia donde abre la puerta
        //     (si no, la hoja chocaria con el marco al girar). Solo cuento el borde de la bisagra,
        //     asi las manijas del otro borde no influyen.
        Vector3 L = ladoLibreLocal.normalized;
        Vector3 h = bisagra.localPosition;
        Vector3 aCentro = centroHojaLocal - h; aCentro.y = 0f;
        float ancho = Mathf.Max(Mathf.Abs(Vector3.Dot(aCentro, L)) * 2f, 0.3f / escala);
        float eps = Mathf.Max(0.05f * (hMax - hMin), 0.001f / escala);
        int sobreMas = 0, sobreMenos = 0;
        foreach (var v in vs)
        {
            Vector3 p = aRaizHoja.MultiplyPoint3x4(v);
            if (Vector3.Dot(p - h, L) > 0.12f * ancho) continue;
            float d = Vector3.Dot(p, N);
            if (d > hMax + eps) sobreMas++;
            else if (d < hMin - eps) sobreMenos++;
        }
        int total = sobreMas + sobreMenos;
        if (total >= 8 && Mathf.Max(sobreMas, sobreMenos) >= 0.8f * total)
        {
            aperturaLocal = sobreMas > sobreMenos ? N : -N;
            como = "por el lado donde sobresalen las bisagras.";
            return true;
        }

        // 2b) Hoja a ras de una cara del marco (el tope queda del otro lado).
        float holguraMas = mMax - hMax;    // hoja -> cara + del marco
        float holguraMenos = hMin - mMin;  // hoja -> cara - del marco
        if (Mathf.Abs(holguraMas - holguraMenos) < 0.01f / escala)
        {
            aperturaLocal = N;
            como = "no vi bisagras y la hoja esta centrada en el marco; asumi un lado. Si queda al reves usa 'Invertir lado de apertura'.";
            return false;
        }
        aperturaLocal = holguraMas < holguraMenos ? N : -N;
        como = "por el lado donde la hoja queda a ras del marco.";
        return true;
    }

    // ------------------------------------------------------------ internos

    // +1 si un angulo positivo de la bisagra abre hacia aperturaLocal, -1 si no.
    float SignoApertura()
    {
        Vector3 L = ladoLibreLocal, A = aperturaLocal.sqrMagnitude > 0.5f ? aperturaLocal : normalLocal;
        // Girar +a en Y mueve el borde libre en la direccion (L.z, 0, -L.x).
        float cruz = L.z * A.x - L.x * A.z;
        return cruz >= 0f ? 1f : -1f;
    }

    void ObtenerLimites(out float min, out float max)
    {
        if (abreAmbosLados) { min = -anguloApertura; max = anguloApertura; return; }
        if (SignoApertura() > 0f) { min = 0f; max = anguloApertura; }
        else { min = -anguloApertura; max = 0f; }
    }

    // Rumbo de la mano (punto de agarre) alrededor de la bisagra, en el mismo sentido que el giro en Y.
    bool TryAnguloMano(out float ang)
    {
        ang = 0f;
        if (mano == null || interactable == null) return false;
        Transform attach = mano.GetAttachTransform(interactable);
        if (attach == null) return false;
        Vector3 p = transform.InverseTransformPoint(attach.position) - bisagra.localPosition;
        p.y = 0f;
        if (transform.TransformVector(p).sqrMagnitude < 0.08f * 0.08f) return false; // muy cerca del eje
        ang = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
        return true;
    }

    void ActualizarEstado()
    {
        if (!EstaAbierta && Mathf.Abs(angulo) > 1f)
        {
            EstaAbierta = true;
            Sonar(sonidoAbrir);
            alAbrir.Invoke();
        }
        else if (EstaAbierta && Mathf.Abs(angulo) < 0.01f)
        {
            EstaAbierta = false;
            Sonar(sonidoCerrar);
            alCerrar.Invoke();
        }
    }

    void Aplicar()
    {
        bisagra.localRotation = Quaternion.Euler(0f, angulo, 0f);
    }

    void Sonar(AudioClip clip)
    {
        if (fuente != null && clip != null) fuente.PlayOneShot(clip);
    }

    // ---- Vista previa en el editor: clic derecho en el componente.
    [ContextMenu("Vista previa: abierta")]
    void PreviaAbierta() { if (bisagra != null) { angulo = SignoApertura() * anguloApertura; Aplicar(); } }

    [ContextMenu("Vista previa: cerrada")]
    void PreviaCerrada() { if (bisagra != null) { angulo = 0f; Aplicar(); } }

    // Dibuja el arco que recorre el borde libre: amarillo = cerrada, verde = abierta al maximo.
    void OnDrawGizmosSelected()
    {
        if (bisagra == null) return;
        Vector3 h = bisagra.localPosition;
        Vector3 c = centroHojaLocal - h; c.y = 0f;
        float r = c.magnitude * 2f;
        if (r < 1e-4f) r = 0.9f / Mathf.Max(1e-5f, Mathf.Abs(transform.lossyScale.x));
        Vector3 L = ladoLibreLocal.normalized;

        float min, max;
        if (aperturaLocal.sqrMagnitude < 0.5f && !abreAmbosLados) { min = -anguloApertura; max = anguloApertura; Gizmos.color = Color.gray; }
        else { ObtenerLimites(out min, out max); Gizmos.color = Color.cyan; }

        Vector3 Punto(float a) => transform.TransformPoint(h + Quaternion.Euler(0f, a, 0f) * L * r);
        const int pasos = 32;
        Vector3 prev = Punto(min);
        for (int i = 1; i <= pasos; i++)
        {
            Vector3 p = Punto(Mathf.Lerp(min, max, i / (float)pasos));
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(bisagra.position, 0.05f);
        Gizmos.DrawLine(bisagra.position, Punto(0f));
        Gizmos.color = Color.green;
        float abierta = abreAmbosLados ? max : SignoApertura() * anguloApertura;
        Gizmos.DrawLine(bisagra.position, Punto(abierta));
    }
}
