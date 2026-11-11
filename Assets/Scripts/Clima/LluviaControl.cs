using System;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Activa / desactiva la lluvia (el objeto con las particulas, p. ej. Particulas_Lluvia).
/// Metodos publicos para botones: AlternarLluvia(), ActivarLluvia(), DesactivarLluvia().
///
/// La lluvia sigue al jugador: una caja de gotas siempre encima de el, por arriba de los techos,
/// asi llueve en todo el modelo sin tener que llenarlo entero de particulas (el Quest no aguanta).
/// Las gotas mueren al chocar con techos, muros o el piso: no llueve adentro de las casas.
/// Todo se configura aqui al iniciar (no hace falta tocar el sistema de particulas a mano).
/// </summary>
public class LluviaControl : MonoBehaviour
{
    [Tooltip("Objeto padre de las particulas de lluvia (y su sonido, si tiene).")]
    public GameObject lluvia;
    public bool lloviendoAlIniciar = false;

    [Header("Area y cantidad")]
    [Tooltip("La caja de lluvia se mueve con el jugador (recomendado). Si no, queda donde esta en la escena.")]
    public bool seguirJugador = true;
    [Tooltip("Lado de la caja de lluvia, en metros.")]
    public float area = 24f;
    [Tooltip("Altura de la caja sobre el piso del jugador, en metros (por encima de los techos).")]
    public float altura = 10f;
    [Tooltip("Gotas que nacen por segundo en toda la caja.")]
    public float gotasPorSegundo = 1500f;
    public int maximoGotas = 3000;

    [Header("Choques")]
    [Tooltip("Las gotas mueren al chocar (techos, muros, piso): no llueve adentro.")]
    public bool chocar = true;

    public bool Lloviendo => lluvia != null && lluvia.activeSelf;

    /// <summary>Se dispara al activar (true) o desactivar (false) la lluvia.</summary>
    public event Action<bool> LluviaCambiada;

    ParticleSystem[] sistemas = new ParticleSystem[0];
    XROrigin origen;

    void Reset()
    {
        var go = GameObject.Find("Particulas_Lluvia");
        if (go != null) lluvia = go;
    }

    void Start()
    {
        origen = FindAnyObjectByType<XROrigin>();
        if (lluvia != null)
        {
            sistemas = lluvia.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in sistemas) Configurar(ps);
        }
        SetLluvia(lloviendoAlIniciar);
    }

    public void AlternarLluvia() => SetLluvia(!Lloviendo);
    public void ActivarLluvia() => SetLluvia(true);
    public void DesactivarLluvia() => SetLluvia(false);

    public void SetLluvia(bool valor)
    {
        if (lluvia == null) return;
        if (valor) Seguir(); // que empiece encima del jugador, no donde estaba
        lluvia.SetActive(valor);
        LluviaCambiada?.Invoke(valor);
    }

    void LateUpdate()
    {
        if (Lloviendo) Seguir();
    }

    void Seguir()
    {
        if (!seguirJugador) return;
        var cam = origen != null && origen.Camera != null ? origen.Camera : Camera.main;
        if (cam == null) return;
        float piso = origen != null ? origen.Origin.transform.position.y : cam.transform.position.y - 1.6f;
        Vector3 p = new Vector3(cam.transform.position.x, piso + altura, cam.transform.position.z);
        foreach (var ps in sistemas) ps.transform.position = p;
    }

    void Configurar(ParticleSystem ps)
    {
        var main = ps.main;
        // En el mundo: al mover la caja con el jugador, las gotas que ya caen no se arrastran.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maximoGotas;

        // Que vivan lo justo para llegar al piso (y un poco mas, por si el jugador esta mas abajo).
        float velocidad = VelocidadDeCaida(ps);
        main.startLifetime = (altura + 4f) / velocidad;

        var shape = ps.shape;
        shape.scale = new Vector3(area, 1f, area);

        var emision = ps.emission;
        emision.rateOverTime = gotasPorSegundo;

        var col = ps.collision;
        col.enabled = chocar;
        if (chocar)
        {
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Medium; // mas barato que High; suficiente para lluvia
            col.lifetimeLoss = 1f;                                 // la gota muere al chocar
            col.bounce = 0f;
            col.sendCollisionMessages = false;
        }
    }

    // La caida viene del modulo "Velocity over Lifetime" (o de la velocidad inicial).
    static float VelocidadDeCaida(ParticleSystem ps)
    {
        float v = 0f;
        var vol = ps.velocityOverLifetime;
        if (vol.enabled) v = Mathf.Abs(vol.y.constant);
        if (v < 0.5f) v = Mathf.Abs(ps.main.startSpeed.constant);
        return Mathf.Max(v, 5f);
    }
}
