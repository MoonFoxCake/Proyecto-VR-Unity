using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Sonido de la escena, todo desde un solo objeto:
///   - Ambiente: viento suave siempre; pajaros de dia (mas al amanecer); grillos de noche;
///     lluvia cuando llueve (y los pajaros se callan). Sigue al Ciclo Dia Noche y al Control Lluvia.
///   - Puertas: sonido al abrir y al cerrar, en la posicion de cada puerta.
///   - Interruptores de luz: "tic" al presionarlos.
/// Se conecta solo al iniciar con todas las PuertaInteractiva y LampSwitch de la escena:
/// no hay que tocar cada puerta ni cada lampara.
/// Los sonidos se generan por codigo; cualquiera se puede reemplazar por una grabacion
/// arrastrandola a su campo en "Clips propios".
/// </summary>
public class SonidosEscena : MonoBehaviour
{
    [Header("Volumen")]
    [Range(0f, 1f)] public float volumenGeneral = 1f;
    [Range(0f, 1f)] public float volumenViento = 0.25f;
    [Range(0f, 1f)] public float volumenPajaros = 0.35f;
    [Range(0f, 1f)] public float volumenGrillos = 0.18f;
    [Range(0f, 1f)] public float volumenLluvia = 0.45f;
    [Range(0f, 1f)] public float volumenPuertas = 0.7f;
    [Range(0f, 1f)] public float volumenInterruptores = 0.6f;

    [Header("Pajaros")]
    [Tooltip("Segundos entre un canto y otro (minimo, maximo). Al amanecer cantan el doble.")]
    public Vector2 esperaPajaros = new Vector2(2f, 6f);
    [Tooltip("Distancia al jugador a la que cantan (minima, maxima), en metros.")]
    public Vector2 distanciaPajaros = new Vector2(8f, 25f);

    [Header("Clips propios (opcional: reemplazan a los generados)")]
    public AudioClip viento;
    public AudioClip lluvia;
    public AudioClip grillos;
    public AudioClip[] pajaros = new AudioClip[0];
    public AudioClip puertaAbrir;
    public AudioClip puertaCerrar;
    public AudioClip interruptor;

    const float Fundido = 0.6f; // volumen por segundo al cambiar de momento o al llover

    AudioSource fuenteViento, fuenteLluvia, fuenteGrillos;
    readonly List<AudioSource> pool = new List<AudioSource>();
    int siguientePool;
    Transform oyente;
    float proximoPajaro;

    DayNightCycle ciclo;
    LluviaControl controlLluvia;
    bool lloviendo;
    DayNightCycle.Momento momento = DayNightCycle.Momento.Dia;

    // Para desconectarse al destruir.
    readonly List<(UnityEvent evento, UnityAction accion)> conexiones = new List<(UnityEvent, UnityAction)>();
    readonly List<(LampSwitch sw, UnityAction<SelectEnterEventArgs> accion)> conexionesInterruptores =
        new List<(LampSwitch, UnityAction<SelectEnterEventArgs>)>();

    void Start()
    {
        GenerarClipsQueFalten();

        fuenteViento = CrearFuente2D("Viento", viento);
        fuenteLluvia = CrearFuente2D("Lluvia", lluvia);
        fuenteGrillos = CrearFuente2D("Grillos", grillos);
        for (int i = 0; i < 10; i++) pool.Add(CrearFuente3D("Efecto " + i));

        ciclo = FindAnyObjectByType<DayNightCycle>();
        if (ciclo != null)
        {
            momento = ciclo.MomentoActual;
            ciclo.MomentoCambiado += OnMomento;
        }
        controlLluvia = FindAnyObjectByType<LluviaControl>();
        if (controlLluvia != null)
        {
            lloviendo = controlLluvia.Lloviendo;
            controlLluvia.LluviaCambiada += OnLluvia;
        }

        int puertas = ConectarPuertas();
        int interruptores = ConectarInterruptores();
        Debug.Log($"[Sonidos] Ambiente listo; conectado a {puertas} puertas y {interruptores} interruptores.", this);

        // Arrancar ya con el volumen correcto (sin fundido inicial).
        fuenteViento.volume = volumenViento * volumenGeneral;
        fuenteLluvia.volume = VolLluvia();
        fuenteGrillos.volume = VolGrillos();
        proximoPajaro = Time.time + 1f;
    }

    void OnDestroy()
    {
        if (ciclo != null) ciclo.MomentoCambiado -= OnMomento;
        if (controlLluvia != null) controlLluvia.LluviaCambiada -= OnLluvia;
        foreach (var (evento, accion) in conexiones) evento.RemoveListener(accion);
        foreach (var (sw, accion) in conexionesInterruptores)
            if (sw != null) sw.selectEntered.RemoveListener(accion);
    }

    void OnMomento(DayNightCycle.Momento m) => momento = m;
    void OnLluvia(bool valor) => lloviendo = valor;

    // ------------------------------------------------------------------ ambiente

    void Update()
    {
        if (oyente == null) oyente = Camera.main != null ? Camera.main.transform : null;

        float paso = Fundido * Time.deltaTime;
        fuenteViento.volume = Mathf.MoveTowards(fuenteViento.volume, volumenViento * volumenGeneral, paso);
        fuenteLluvia.volume = Mathf.MoveTowards(fuenteLluvia.volume, VolLluvia(), paso);
        fuenteGrillos.volume = Mathf.MoveTowards(fuenteGrillos.volume, VolGrillos(), paso);

        if (PajarosActivos() && Time.time >= proximoPajaro && oyente != null && pajaros.Length > 0)
        {
            CantarPajaro();
            float espera = Random.Range(esperaPajaros.x, esperaPajaros.y);
            if (momento == DayNightCycle.Momento.Amanecer) espera *= 0.5f; // coro del amanecer
            proximoPajaro = Time.time + espera;
        }
    }

    bool PajarosActivos() => !lloviendo && momento != DayNightCycle.Momento.Noche;
    float VolLluvia() => lloviendo ? volumenLluvia * volumenGeneral : 0f;
    float VolGrillos()
    {
        float v = momento == DayNightCycle.Momento.Noche ? 1f : momento == DayNightCycle.Momento.Atardecer ? 0.35f : 0f;
        if (lloviendo) v *= 0.3f;
        return v * volumenGrillos * volumenGeneral;
    }

    void CantarPajaro()
    {
        // En algun punto alrededor y por encima del jugador, como en un arbol o un techo.
        Vector2 dir = Random.insideUnitCircle.normalized;
        float dist = Random.Range(distanciaPajaros.x, distanciaPajaros.y);
        Vector3 pos = oyente.position + new Vector3(dir.x, 0f, dir.y) * dist + Vector3.up * Random.Range(3f, 8f);
        var clip = pajaros[Random.Range(0, pajaros.Length)];
        Reproducir(clip, pos, volumenPajaros, Random.Range(0.9f, 1.15f), 5f, 60f);
    }

    // ------------------------------------------------------------------ puertas e interruptores

    int ConectarPuertas()
    {
        int total = 0;
        foreach (var p in FindObjectsByType<PuertaInteractiva>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Si la puerta ya trae sus propios sonidos, no se duplica.
            if (p.sonidoAbrir != null || p.sonidoCerrar != null) continue;
            var puerta = p;
            UnityAction abrir = () => Reproducir(puertaAbrir, PosPuerta(puerta), volumenPuertas, Random.Range(0.92f, 1.08f), 1f, 18f);
            UnityAction cerrar = () => Reproducir(puertaCerrar, PosPuerta(puerta), volumenPuertas, Random.Range(0.92f, 1.08f), 1f, 18f);
            puerta.alAbrir.AddListener(abrir);
            puerta.alCerrar.AddListener(cerrar);
            conexiones.Add((puerta.alAbrir, abrir));
            conexiones.Add((puerta.alCerrar, cerrar));
            total++;
        }
        return total;
    }

    static Vector3 PosPuerta(PuertaInteractiva p) =>
        p.hoja != null ? p.hoja.position : p.bisagra != null ? p.bisagra.position : p.transform.position;

    int ConectarInterruptores()
    {
        int total = 0;
        foreach (var s in FindObjectsByType<LampSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var sw = s;
            UnityAction<SelectEnterEventArgs> clic = _ =>
                Reproducir(interruptor, sw.transform.position, volumenInterruptores, Random.Range(0.95f, 1.05f), 0.5f, 10f);
            sw.selectEntered.AddListener(clic);
            conexionesInterruptores.Add((sw, clic));
            total++;
        }
        return total;
    }

    /// <summary>Reproduce un sonido en un punto del mundo (usa un grupo de fuentes reciclables).</summary>
    public void Reproducir(AudioClip clip, Vector3 posicion, float volumen, float tono = 1f, float distMin = 1f, float distMax = 20f)
    {
        if (clip == null || volumen <= 0f) return;
        var f = pool[siguientePool];
        siguientePool = (siguientePool + 1) % pool.Count;
        f.transform.position = posicion;
        f.pitch = tono;
        f.minDistance = distMin;
        f.maxDistance = distMax;
        f.PlayOneShot(clip, volumen * volumenGeneral);
    }

    // ------------------------------------------------------------------ preparacion

    void GenerarClipsQueFalten()
    {
        if (viento == null) viento = SintetizadorSonidos.Viento();
        if (lluvia == null) lluvia = SintetizadorSonidos.Lluvia();
        if (grillos == null) grillos = SintetizadorSonidos.Grillos();
        if (pajaros == null || pajaros.Length == 0)
        {
            pajaros = new AudioClip[5];
            for (int i = 0; i < pajaros.Length; i++) pajaros[i] = SintetizadorSonidos.Pajaro(i);
        }
        if (puertaAbrir == null) puertaAbrir = SintetizadorSonidos.PuertaAbrir();
        if (puertaCerrar == null) puertaCerrar = SintetizadorSonidos.PuertaCerrar();
        if (interruptor == null) interruptor = SintetizadorSonidos.Interruptor();
    }

    // Ambientes: suenan "en la cabeza" (2D), sin direccion.
    AudioSource CrearFuente2D(string nombre, AudioClip clip)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var f = go.AddComponent<AudioSource>();
        f.clip = clip;
        f.loop = true;
        f.spatialBlend = 0f;
        f.volume = 0f;
        f.playOnAwake = false;
        if (clip != null) f.Play();
        return f;
    }

    // Efectos: suenan desde su lugar (3D), mas bajo mientras mas lejos.
    AudioSource CrearFuente3D(string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var f = go.AddComponent<AudioSource>();
        f.spatialBlend = 1f;
        f.rolloffMode = AudioRolloffMode.Logarithmic;
        f.dopplerLevel = 0f;
        f.playOnAwake = false;
        return f;
    }
}
