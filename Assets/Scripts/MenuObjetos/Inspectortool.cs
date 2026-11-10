using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Herramienta inspectora. Agárrala: sale un rayo de la punta.
///   - El objeto apuntado (si tiene InspectableObject) brilla.
///   - Gatillo sobre un objeto: muestra su panel de información frente a ti.
///   - Gatillo sobre el mismo objeto, o al vacío: cierra el panel.
/// Solo funciona mientras la sostienes en la mano (no en el cinturón).
/// El rayo sale hacia el eje Z (azul, forward) de la punta.
/// Para elegir sobre qué assets aplica: ponles InspectableObject, y/o usa
/// layerMask para limitar qué capas toca el rayo.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class InspectorTool : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punta de la herramienta (su forward es la dirección del rayo). Si queda vacío usa este objeto.")]
    public Transform tip;
    [Tooltip("Panel de información. Si queda vacío busca uno en la escena o crea uno por defecto.")]
    public InfoPanel panel;
    public Material lineMaterial;

    [Header("Rayo")]
    public float maxDistance = 10f;
    public LayerMask layerMask = ~0;
    public float lineWidth = 0.004f;
    public Color idleColor = new Color(1f, 1f, 1f, 0.6f);
    public Color hoverColor = new Color(0.3f, 1f, 0.4f, 1f);

    [Header("Comportamiento")]
    [Tooltip("Cierra el panel al soltar o guardar la herramienta.")]
    public bool hideWhenReleased = true;

    XRGrabInteractable grab;
    LineRenderer line;
    InspectableObject hovered;
    bool held;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (tip == null) tip = transform;
        CreateLine();
    }

    void OnEnable()
    {
        grab.activated.AddListener(OnActivated);
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.activated.RemoveListener(OnActivated);
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        StopUsing();
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Solo cuenta cuando la sostiene una mano, no el socket del cinturón.
        if (args.interactorObject is XRSocketInteractor) StopUsing();
        else held = true;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        StopUsing();
    }

    void StopUsing()
    {
        held = false;
        SetHovered(null);
        if (line != null) line.enabled = false;
        if (hideWhenReleased && panel != null) panel.Hide();
    }

    void Update()
    {
        if (!held) return;

        Vector3 origin = tip.position;
        Vector3 end = origin + tip.forward * maxDistance;
        InspectableObject found = null;

        if (Physics.Raycast(origin, tip.forward, out RaycastHit hit, maxDistance,
                            layerMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            found = hit.collider.GetComponentInParent<InspectableObject>();
        }

        SetHovered(found);

        line.enabled = true;
        line.SetPosition(0, origin);
        line.SetPosition(1, end);
        SetLineColor(hovered != null ? hoverColor : idleColor);
    }

    void OnActivated(ActivateEventArgs args)
    {
        if (!held) return;
        if (!EnsurePanel()) return;

        if (hovered == null) panel.Hide();
        else if (panel.Current == hovered) panel.Hide();
        else panel.Show(hovered);
    }

    void SetHovered(InspectableObject obj)
    {
        if (obj == hovered) return;
        if (hovered != null) hovered.HoverExit();
        hovered = obj;
        if (hovered != null) hovered.HoverEnter();
    }

    bool EnsurePanel()
    {
        if (panel != null) return true;
        panel = FindFirstObjectByType<InfoPanel>();
        if (panel == null)
        {
            var go = new GameObject("InfoPanel");
            panel = go.AddComponent<InfoPanel>();
        }
        return panel != null;
    }

    // ---------- Línea del rayo ----------

    void CreateLine()
    {
        var go = new GameObject("Inspector_Rayo");
        go.transform.SetParent(transform, false);
        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = lineMaterial != null ? new Material(lineMaterial) : FallbackMaterial();
        line.enabled = false;
    }

    void SetLineColor(Color c)
    {
        line.startColor = c;
        line.endColor = c;
        line.material.color = c;
    }

    static Material FallbackMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        return new Material(sh);
    }
}