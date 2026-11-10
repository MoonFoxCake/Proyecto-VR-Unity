using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Vista por pisos: con la maqueta en la mano, el gatillo separa los pisos hacia arriba
/// (1er piso, 2do piso, techo) para ver por dentro; otro gatillo los junta.
/// Al volver a la exhibicion se juntan solos.
/// La herramienta Tools > VR > Maqueta llena "Niveles" (de abajo hacia arriba).
/// Metodos publicos para botones: Alternar(), Separar(), Juntar().
/// </summary>
[RequireComponent(typeof(MaquetaExhibicion))]
public class PisosMaqueta : MonoBehaviour
{
    [Tooltip("Partes de la maqueta, de abajo hacia arriba.")]
    public List<Transform> niveles = new List<Transform>();
    [Tooltip("Cuanto sube cada nivel respecto al de abajo, en metros reales de la casa.")]
    public float separacion = 2.5f;
    [Tooltip("Segundos que tarda en separar o juntar.")]
    public float duracion = 0.6f;

    public UnityEvent alSeparar = new UnityEvent();
    public UnityEvent alJuntar = new UnityEvent();

    public bool Separada { get; private set; }

    MaquetaExhibicion maqueta;
    float t;

    void Awake() => maqueta = GetComponent<MaquetaExhibicion>();

    void OnEnable()
    {
        maqueta.Grab.activated.AddListener(OnGatillo);
        maqueta.alEmpezarRegreso.AddListener(Juntar);
    }

    void OnDisable()
    {
        maqueta.Grab.activated.RemoveListener(OnGatillo);
        maqueta.alEmpezarRegreso.RemoveListener(Juntar);
    }

    void OnGatillo(ActivateEventArgs args) => Alternar();

    public void Alternar() { if (Separada) Juntar(); else Separar(); }

    public void Separar()
    {
        if (Separada) return;
        Separada = true;
        alSeparar.Invoke();
    }

    public void Juntar()
    {
        if (!Separada) return;
        Separada = false;
        alJuntar.Invoke();
    }

    void Update()
    {
        float objetivo = Separada ? 1f : 0f;
        if (Mathf.Approximately(t, objetivo)) return;
        t = Mathf.MoveTowards(t, objetivo, Time.deltaTime / Mathf.Max(0.01f, duracion));
        float s = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < niveles.Count; i++)
            if (niveles[i] != null) niveles[i].localPosition = Vector3.up * (i * separacion * s);
    }
}
