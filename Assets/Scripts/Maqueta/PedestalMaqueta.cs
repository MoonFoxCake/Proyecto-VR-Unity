using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Base de exhibicion de una maqueta: disco de luz, anillo que respira, placa con el nombre
/// y una pista ("Agarra para explorar") cuando el jugador se acerca.
///   - Al apuntar a la maqueta el anillo se ilumina.
///   - Mientras la maqueta regresa, el anillo late; al llegar, destella.
/// Va en el objeto "Exhibicion - ..." (no se mueve); la maqueta es su hija y es la que se agarra.
/// Lo arma Tools > VR > Maqueta. Todo lo visual se crea al iniciar.
/// </summary>
public class PedestalMaqueta : MonoBehaviour
{
    [Header("Maqueta")]
    public MaquetaExhibicion maqueta;

    [Header("Base")]
    [Tooltip("Radio del disco, en metros.")]
    public float radio = 0.4f;
    public Material materialDisco;
    public Material materialAnillo;
    public Color colorLuz = new Color(0.45f, 0.8f, 1f);

    [Header("Placa")]
    public string titulo = "Maqueta";
    public string subtitulo = "";
    public Color colorTexto = Color.white;

    [Header("Pista")]
    [Tooltip("A cuantos metros del jugador aparece la pista.")]
    public float distanciaPista = 1.8f;
    [TextArea] public string textoPista = "Agarra para explorar";

    Transform disco;
    LineRenderer anillo;
    TextMeshPro placa, pista;
    Material matDisco, matAnillo;
    int apuntando;
    float destello, alturaPista;

    void Start()
    {
        Construir();
        if (maqueta == null) return;
        var grab = maqueta.Grab;
        grab.hoverEntered.AddListener(OnApuntar);
        grab.hoverExited.AddListener(OnDejarDeApuntar);
        maqueta.alVolverAExhibicion.AddListener(OnLlego);

        var rs = maqueta.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            alturaPista = b.max.y - transform.position.y + 0.06f;
        }
        else alturaPista = 0.4f;
    }

    void OnDestroy()
    {
        if (maqueta == null) return;
        var grab = maqueta.Grab;
        if (grab == null) return;
        grab.hoverEntered.RemoveListener(OnApuntar);
        grab.hoverExited.RemoveListener(OnDejarDeApuntar);
        maqueta.alVolverAExhibicion.RemoveListener(OnLlego);
    }

    void OnApuntar(HoverEnterEventArgs args) { if (!(args.interactorObject is XRSocketInteractor)) apuntando++; }
    void OnDejarDeApuntar(HoverExitEventArgs args) { if (!(args.interactorObject is XRSocketInteractor)) apuntando = Mathf.Max(0, apuntando - 1); }
    void OnLlego() => destello = 1f;

    void Update()
    {
        if (anillo == null) return;
        float t = Time.time;

        // Brillo del anillo: respira suave; mas fuerte al apuntar; late mientras la maqueta vuelve.
        float brillo = 0.45f + 0.12f * Mathf.Sin(t * 1.6f);
        if (apuntando > 0) brillo = 0.95f;
        if (maqueta != null && maqueta.Regresando) brillo = 0.55f + 0.4f * Mathf.Abs(Mathf.Sin(t * 7f));
        destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 1.5f);
        brillo = Mathf.Clamp01(brillo + destello);

        Color c = colorLuz; c.a = brillo;
        if (matAnillo != null) SetColor(matAnillo, c); // URP Unlit no usa el color por vertice de la linea
        else anillo.startColor = anillo.endColor = c;
        anillo.widthMultiplier = 0.006f * (1f + destello * 1.5f + (apuntando > 0 ? 0.4f : 0f));
        if (matDisco != null) { Color d = colorLuz; d.a = 0.10f + 0.12f * brillo; SetColor(matDisco, d); }

        // Placa y pista miran al jugador (solo giran en Y).
        var cam = Camera.main;
        if (cam == null) return;
        Mirar(placa.transform, cam.transform.position);

        bool cerca = Vector3.Distance(cam.transform.position, transform.position) < distanciaPista;
        bool mostrar = cerca && maqueta != null && maqueta.EnExhibicion;
        if (pista.gameObject.activeSelf != mostrar) pista.gameObject.SetActive(mostrar);
        if (mostrar)
        {
            pista.transform.position = transform.position + Vector3.up * (alturaPista + 0.01f * Mathf.Sin(t * 2f));
            Mirar(pista.transform, cam.transform.position);
            Color p = colorTexto; p.a = apuntando > 0 ? 1f : 0.8f;
            pista.color = p;
        }
    }

    static void Mirar(Transform tr, Vector3 ojo)
    {
        Vector3 dir = tr.position - ojo; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) tr.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else m.color = c;
    }

    // ------------------------------------------------------------ armado

    void Construir()
    {
        // Disco: un cilindro muy plano, sin colision (no estorba al agarrar la maqueta).
        var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        d.name = "Disco";
        Destroy(d.GetComponent<Collider>());
        disco = d.transform;
        disco.SetParent(transform, false);
        disco.localScale = new Vector3(radio * 2f, 0.004f, radio * 2f);
        var rd = d.GetComponent<MeshRenderer>();
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (materialDisco != null) { matDisco = new Material(materialDisco); rd.sharedMaterial = matDisco; }

        // Anillo de luz en el borde.
        var a = new GameObject("Anillo");
        a.transform.SetParent(transform, false);
        anillo = a.AddComponent<LineRenderer>();
        anillo.useWorldSpace = false;
        anillo.loop = true;
        const int pasos = 72;
        anillo.positionCount = pasos;
        for (int i = 0; i < pasos; i++)
        {
            float ang = i * Mathf.PI * 2f / pasos;
            anillo.SetPosition(i, new Vector3(Mathf.Cos(ang) * radio, 0.004f, Mathf.Sin(ang) * radio));
        }
        anillo.widthMultiplier = 0.006f;
        anillo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (materialAnillo != null) { matAnillo = new Material(materialAnillo); anillo.sharedMaterial = matAnillo; }

        // Placa con nombre y escala, al borde del disco.
        placa = Texto("Placa", "<b>" + titulo + "</b>" + (string.IsNullOrEmpty(subtitulo) ? "" : "\n<size=65%>" + subtitulo + "</size>"), 0.0028f);
        placa.transform.localPosition = new Vector3(0f, 0.035f, -radio - 0.02f);

        // Pista que flota sobre la maqueta.
        pista = Texto("Pista", textoPista, 0.0022f);
        pista.gameObject.SetActive(false);
    }

    TextMeshPro Texto(string nombre, string contenido, float escala)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * escala;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = contenido;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36;
        tmp.color = colorTexto;
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        return tmp;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = colorLuz;
        const int pasos = 48;
        Vector3 prev = transform.position + transform.right * radio;
        for (int i = 1; i <= pasos; i++)
        {
            float ang = i * Mathf.PI * 2f / pasos;
            Vector3 p = transform.position + (transform.right * Mathf.Cos(ang) + transform.forward * Mathf.Sin(ang)) * radio;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
