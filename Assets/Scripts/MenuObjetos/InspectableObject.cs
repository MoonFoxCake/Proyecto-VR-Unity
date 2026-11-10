using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct InfoEntry
{
    public string label;   // Ej: "Material"
    public string value;   // Ej: "Acero inoxidable"
}

/// <summary>
/// Pon este componente en CUALQUIER asset que quieras poder inspeccionar
/// con la InspectorTool. Los que no lo tengan, la herramienta los ignora.
/// Se puede agregar a varios objetos a la vez: selecciónalos en la Jerarquía
/// y usa Add Component. Si el objeto no tiene Collider, se le crea uno.
/// Al apuntarlo con el rayo, el objeto brilla (emisión).
/// </summary>
public class InspectableObject : MonoBehaviour
{
    [Header("Información")]
    public string title = "Objeto";
    [TextArea(2, 8)] public string description;
    public List<InfoEntry> characteristics = new List<InfoEntry>();

    [Header("Brillo al apuntar")]
    [ColorUsage(false, true)] public Color glowColor = new Color(0.2f, 0.7f, 1f);
    public float glowIntensity = 1.5f;
    [Tooltip("Velocidad del pulso. 0 = brillo constante.")]
    public float pulseSpeed = 3f;
    [Range(0f, 1f)] public float pulseDepth = 0.4f;

    [Header("Colisión")]
    [Tooltip("Si el objeto (o sus hijos) no tiene Collider, crea un BoxCollider ajustado.")]
    public bool addColliderIfMissing = true;

    [Header("Eventos")]
    public UnityEvent onHoverEnter;
    public UnityEvent onHoverExit;
    public UnityEvent onInfoShown;
    public UnityEvent onInfoHidden;

    class GlowEntry
    {
        public Renderer renderer;
        public Material[] originals;
    }

    readonly List<GlowEntry> entries = new List<GlowEntry>();
    readonly List<Material> created = new List<Material>();
    bool glowing;

    void Awake()
    {
        if (addColliderIfMissing) EnsureCollider();
    }

    void Update()
    {
        if (glowing) ApplyGlow();
    }

    void OnDisable() { SetHighlight(false); }
    void OnDestroy() { SetHighlight(false); }

    public void HoverEnter()
    {
        SetHighlight(true);
        onHoverEnter.Invoke();
    }

    public void HoverExit()
    {
        SetHighlight(false);
        onHoverExit.Invoke();
    }

    public void SetHighlight(bool on)
    {
        if (on == glowing) return;
        glowing = on;

        if (on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                Material[] orig = r.sharedMaterials;
                Material[] inst = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    if (orig[i] != null && orig[i].HasProperty("_EmissionColor"))
                    {
                        var m = new Material(orig[i]);
                        m.EnableKeyword("_EMISSION");
                        created.Add(m);
                        inst[i] = m;
                    }
                    else inst[i] = orig[i];
                }
                entries.Add(new GlowEntry { renderer = r, originals = orig });
                r.sharedMaterials = inst;
            }
            ApplyGlow();
        }
        else
        {
            foreach (var e in entries)
                if (e.renderer != null) e.renderer.sharedMaterials = e.originals;
            entries.Clear();
            foreach (var m in created)
                if (m != null) Destroy(m);
            created.Clear();
        }
    }

    void ApplyGlow()
    {
        float k = glowIntensity;
        if (pulseSpeed > 0f)
            k *= Mathf.Lerp(1f - pulseDepth, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        Color c = glowColor * k;
        foreach (var m in created)
            if (m != null) m.SetColor("_EmissionColor", c);
    }

    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;

        bool has = false;
        Bounds b = new Bounds();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            Bounds lb = r.localBounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 p = lb.center + Vector3.Scale(lb.extents, new Vector3(x, y, z));
                        Vector3 local = transform.InverseTransformPoint(r.transform.TransformPoint(p));
                        if (!has) { b = new Bounds(local, Vector3.zero); has = true; }
                        else b.Encapsulate(local);
                    }
        }
        if (!has) return;

        var bc = gameObject.AddComponent<BoxCollider>();
        bc.center = b.center;
        bc.size = b.size;
    }
}