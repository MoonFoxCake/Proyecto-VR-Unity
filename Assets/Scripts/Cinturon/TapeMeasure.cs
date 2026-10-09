using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Medidor de distancias. Agárralo y usa el gatillo (Activate):
///   1er gatillo: marca el punto A en la punta.
///   2do gatillo: marca el punto B y fija la medición.
///   3er gatillo: borra todo.
/// Si asignas scaleReference (ej. Casa_Modelo), muestra también la medida
/// "real" de la casa: distancia medida / escala de la maqueta.
/// Los marcadores, la línea y el texto se crean solos al usarlo.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TapeMeasure : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punta del medidor. Si queda vacío usa este mismo objeto.")]
    public Transform tip;
    [Tooltip("Objeto cuya escala define la maqueta (ej. Casa_Modelo). Opcional.")]
    public Transform scaleReference;
    public Material lineMaterial;
    public Material markerMaterial;

    [Header("Apariencia")]
    public float lineWidth = 0.004f;
    public float markerSize = 0.012f;
    public float labelScale = 0.01f;
    public int labelFontSize = 36;
    public Color labelColor = new Color(1f, 0.92f, 0.2f);

    enum State { Idle, PlacedA, PlacedB }
    State state = State.Idle;

    XRGrabInteractable grab;
    GameObject root;
    Transform markerA, markerB;
    LineRenderer line;
    TextMeshPro label;
    Vector3 pointA, pointB;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (tip == null) tip = transform;
    }

    void OnEnable()
    {
        grab.activated.AddListener(OnActivated);
        grab.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        grab.activated.RemoveListener(OnActivated);
        grab.selectEntered.RemoveListener(OnSelectEntered);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }

    void OnActivated(ActivateEventArgs args)
    {
        switch (state)
        {
            case State.Idle:
                EnsureObjects();
                pointA = tip.position;
                PlaceMarker(markerA, pointA);
                markerB.gameObject.SetActive(false);
                line.enabled = true;
                label.gameObject.SetActive(true);
                state = State.PlacedA;
                UpdateVisual(pointA, tip.position);
                break;

            case State.PlacedA:
                pointB = tip.position;
                PlaceMarker(markerB, pointB);
                state = State.PlacedB;
                UpdateVisual(pointA, pointB);
                break;

            case State.PlacedB:
                Clear();
                break;
        }
    }

    // Si guardas el medidor en el cinturón a mitad de una medición, se cancela.
    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor && state == State.PlacedA)
            Clear();
    }

    void Update()
    {
        if (state == State.PlacedA)
            UpdateVisual(pointA, tip.position);

        if (label != null && label.gameObject.activeSelf)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = label.transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                    label.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
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

    void UpdateVisual(Vector3 p, Vector3 q)
    {
        line.SetPosition(0, p);
        line.SetPosition(1, q);
        label.transform.position = (p + q) * 0.5f + Vector3.up * 0.04f;
        label.text = Format(Vector3.Distance(p, q));
    }

    string Format(float d)
    {
        if (scaleReference != null)
        {
            float s = Mathf.Abs(scaleReference.lossyScale.x);
            if (s > 0.0001f && Mathf.Abs(s - 1f) > 0.01f)
                return FormatLength(d / s) + "\n<size=60%>maqueta: " + FormatLength(d) + "</size>";
        }
        return FormatLength(d);
    }

    static string FormatLength(float meters)
    {
        return meters >= 1f ? meters.ToString("F2") + " m" : (meters * 100f).ToString("F1") + " cm";
    }

    void PlaceMarker(Transform marker, Vector3 position)
    {
        marker.position = position;
        marker.gameObject.SetActive(true);
    }

    void EnsureObjects()
    {
        if (root != null) return;

        root = new GameObject("Medidor_Marcas");

        line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.material = lineMaterial != null ? lineMaterial : FallbackMaterial(Color.yellow);

        markerA = CreateMarker("Marca_A");
        markerB = CreateMarker("Marca_B");

        var go = new GameObject("Medidor_Texto");
        go.transform.SetParent(root.transform, false);
        go.transform.localScale = Vector3.one * labelScale;
        label = go.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = labelFontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = labelColor;
        label.richText = true;
    }

    Transform CreateMarker(string markerName)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = markerName;
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(root.transform, false);
        s.transform.localScale = Vector3.one * markerSize;
        var r = s.GetComponent<MeshRenderer>();
        r.sharedMaterial = markerMaterial != null ? markerMaterial : FallbackMaterial(Color.red);
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
