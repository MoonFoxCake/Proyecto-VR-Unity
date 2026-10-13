using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Como se ve la escena en un momento del dia. Todo se puede ajustar en el Inspector.</summary>
[Serializable]
public class AspectoMomento
{
    [Header("Luz (sol o luna)")]
    [Tooltip("Grados sobre el horizonte.")]
    public float elevacion = 50f;
    [Tooltip("Direccion (rotacion Y) de la luz.")]
    public float azimut = -30f;
    public Color colorLuz = new Color(1f, 0.957f, 0.839f);
    public float intensidadLuz = 1f;

    [Header("Cielo (Skybox/Procedural)")]
    public Color tinteCielo = new Color(0.5f, 0.5f, 0.5f);
    public Color colorSuelo = new Color(0.369f, 0.349f, 0.341f);
    public float exposicion = 1.3f;
    public float grosorAtmosfera = 1f;
    [Tooltip("Tamano del disco del sol (de noche hace de luna).")]
    public float tamanoDisco = 0.04f;

    [Header("Luz ambiental")]
    [Tooltip("Activado = la luz ambiental sale del cielo (como el default de Unity). " +
             "Desactivado = usa los tres colores de abajo.")]
    public bool ambienteDesdeCielo = true;
    public Color ambienteArriba = new Color(0.5f, 0.5f, 0.5f);
    public Color ambienteHorizonte = new Color(0.4f, 0.4f, 0.4f);
    public Color ambienteAbajo = new Color(0.2f, 0.2f, 0.2f);
}

/// <summary>
/// Cambia al instante entre Amanecer, Dia, Atardecer y Noche: mueve y colorea
/// la Directional Light, ajusta el cielo procedural y la luz ambiental.
/// De noche la misma luz hace de luna (luz azulada y el disco del cielo es la luna).
/// Metodos publicos para botones: IrAAmanecer(), IrADia(), IrAAtardecer(),
/// IrANoche(), AlternarDiaNoche().
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public enum Momento { Amanecer, Dia, Atardecer, Noche }

    [Tooltip("La Directional Light de la escena.")]
    public Light sol;
    public Momento momentoInicial = Momento.Dia;

    [Tooltip("Frio, morado, crepusculo.")]
    public AspectoMomento amanecer = new AspectoMomento
    {
        // Atmosfera delgada + tinte rojo/azul (poco verde) = cielo lila en vez de horizonte naranja.
        elevacion = 10f, azimut = 130f,
        colorLuz = new Color(0.8f, 0.68f, 1f), intensidadLuz = 0.5f,
        tinteCielo = new Color(0.72f, 0.3f, 0.85f), colorSuelo = new Color(0.32f, 0.25f, 0.38f),
        exposicion = 0.85f, grosorAtmosfera = 0.55f, tamanoDisco = 0f,
        ambienteDesdeCielo = false,
        ambienteArriba = new Color(0.58f, 0.47f, 0.78f),
        ambienteHorizonte = new Color(0.66f, 0.5f, 0.74f),
        ambienteAbajo = new Color(0.22f, 0.17f, 0.28f),
    };

    [Tooltip("Claro, como el default de Unity.")]
    public AspectoMomento dia = new AspectoMomento();

    [Tooltip("Calido, anaranjado.")]
    public AspectoMomento atardecer = new AspectoMomento
    {
        elevacion = 7f, azimut = 310f,
        colorLuz = new Color(1f, 0.55f, 0.25f), intensidadLuz = 0.9f,
        tinteCielo = new Color(0.62f, 0.42f, 0.3f), colorSuelo = new Color(0.35f, 0.25f, 0.2f),
        exposicion = 1.1f, grosorAtmosfera = 1.7f, tamanoDisco = 0.05f,
        ambienteDesdeCielo = false,
        ambienteArriba = new Color(0.55f, 0.4f, 0.32f),
        ambienteHorizonte = new Color(0.65f, 0.4f, 0.22f),
        ambienteAbajo = new Color(0.22f, 0.15f, 0.1f),
    };

    [Tooltip("Oscuro pero visible, con luna.")]
    public AspectoMomento noche = new AspectoMomento
    {
        elevacion = 4f,
        azimut = 130f,
        colorLuz = new Color(0.72f, 0.62f, 1f),intensidadLuz = 0.45f,
        tinteCielo = new Color(0.45f, 0.33f, 0.62f), colorSuelo = new Color(0.22f, 0.2f, 0.3f),
        exposicion = 0.9f, grosorAtmosfera = 1.3f, tamanoDisco = 0.04f,
        ambienteDesdeCielo = false,
        ambienteArriba = new Color(0.42f, 0.36f, 0.62f),
        ambienteHorizonte = new Color(0.48f, 0.38f, 0.6f),
        ambienteAbajo = new Color(0.16f, 0.14f, 0.22f),
    };

    public Momento MomentoActual { get; private set; }

    /// <summary>Se dispara cada vez que cambia el momento del dia.</summary>
    public event Action<Momento> MomentoCambiado;

    static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    static readonly int GroundId = Shader.PropertyToID("_GroundColor");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");
    static readonly int SunSizeId = Shader.PropertyToID("_SunSize");

    void Reset() => ValoresPorDefecto();

    /// <summary>Busca la Directional Light de la escena para usarla como sol.</summary>
    public void ValoresPorDefecto()
    {
        sol = RenderSettings.sun;
        if (sol != null) return;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sol = l; break; }
    }

    void Awake()
    {
        // Copia del cielo para no modificar el material del proyecto al jugar.
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty(SkyTintId))
            RenderSettings.skybox = new Material(RenderSettings.skybox);
    }

    void Start() => SetMomento(momentoInicial);

    public void IrAAmanecer() => SetMomento(Momento.Amanecer);
    public void IrADia() => SetMomento(Momento.Dia);
    public void IrAAtardecer() => SetMomento(Momento.Atardecer);
    public void IrANoche() => SetMomento(Momento.Noche);
    public void AlternarDiaNoche() => SetMomento(MomentoActual == Momento.Noche ? Momento.Dia : Momento.Noche);

    public void SetMomento(Momento m)
    {
        MomentoActual = m;
        Aplicar(Aspecto(m));
        MomentoCambiado?.Invoke(m);
    }

    AspectoMomento Aspecto(Momento m)
    {
        switch (m)
        {
            case Momento.Amanecer: return amanecer;
            case Momento.Atardecer: return atardecer;
            case Momento.Noche: return noche;
            default: return dia;
        }
    }

    void Aplicar(AspectoMomento a)
    {
        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(a.elevacion, a.azimut, 0f);
            sol.color = a.colorLuz;
            sol.intensity = a.intensidadLuz;
            sol.enabled = true;
            RenderSettings.sun = sol;
        }

        var cielo = RenderSettings.skybox;
        if (cielo != null && cielo.HasProperty(SkyTintId))
        {
            cielo.SetColor(SkyTintId, a.tinteCielo);
            cielo.SetColor(GroundId, a.colorSuelo);
            cielo.SetFloat(ExposureId, a.exposicion);
            cielo.SetFloat(AtmosphereId, a.grosorAtmosfera);
            cielo.SetFloat(SunSizeId, a.tamanoDisco);
        }

        if (a.ambienteDesdeCielo)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = a.ambienteArriba;
            RenderSettings.ambientEquatorColor = a.ambienteHorizonte;
            RenderSettings.ambientGroundColor = a.ambienteAbajo;
        }

        // Recalcula la luz ambiental/reflejos del cielo nuevo (una sola vez, no cada frame).
        DynamicGI.UpdateEnvironment();
    }

    // Vista previa en el editor: clic derecho en el componente (o en los 3 puntos).
    [ContextMenu("Vista previa: Amanecer")] void PreviaAmanecer() => SetMomento(Momento.Amanecer);
    [ContextMenu("Vista previa: Dia")] void PreviaDia() => SetMomento(Momento.Dia);
    [ContextMenu("Vista previa: Atardecer")] void PreviaAtardecer() => SetMomento(Momento.Atardecer);
    [ContextMenu("Vista previa: Noche")] void PreviaNoche() => SetMomento(Momento.Noche);
}
