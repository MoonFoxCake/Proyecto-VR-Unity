using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Panel de información en World Space que aparece SIEMPRE frente al jugador.
/// Un solo panel para todos los objetos. Si no asignas la UI, se crea uno
/// por defecto. Si hay una pared u objeto entre tú y el panel, el panel se
/// acerca y se encoge (mismo tamaño aparente) para no atravesarlo.
/// </summary>
public class InfoPanel : MonoBehaviour
{
    [Header("UI (opcional: si quedan vacíos se crea un panel por defecto)")]
    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text characteristicsText;

    [Header("Posición frente al jugador")]
    [Tooltip("Cabeza del jugador. Si queda vacío usa Camera.main.")]
    public Transform head;
    public float distance = 1.2f;
    [Tooltip("Altura respecto a los ojos (negativo = más abajo).")]
    public float heightOffset = -0.1f;
    [Tooltip("Desplazamiento lateral (+ derecha, - izquierda).")]
    public float sideOffset = 0f;

    [Header("Seguimiento")]
    [Tooltip("Si está activo, el panel sigue suavemente al jugador mientras está abierto.")]
    public bool followPlayer = false;
    public float followSpeed = 4f;

    [Header("Evitar atravesar objetos")]
    public bool avoidClipping = true;
    [Tooltip("Capas que cuentan como obstáculo. Quita aquí las que no quieras (manos, etc.).")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Separación (m) que queda entre el panel y el obstáculo.")]
    public float margin = 0.1f;
    [Tooltip("Cuánto puede acercarse y encogerse el panel (0.4 = hasta el 40% de su distancia y tamaño).")]
    [Range(0.2f, 1f)] public float minScaleFactor = 0.4f;
    [Tooltip("Ignora los objetos que se pueden agarrar (herramientas, props).")]
    public bool ignoreGrabbables = true;

    [Header("Apariencia")]
    public float scale = 0.002f;

    public InspectableObject Current { get; private set; }

    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    void Awake()
    {
        if (titleText == null || descriptionText == null || characteristicsText == null || root == null)
            BuildDefault();

        transform.localScale = Vector3.one * scale;
        root.SetActive(false);
    }

    public void Show(InspectableObject obj)
    {
        if (obj == null) return;
        if (Current != null) Current.onInfoHidden.Invoke();
        Current = obj;

        titleText.text = obj.title;
        descriptionText.text = obj.description;

        var sb = new StringBuilder();
        foreach (var e in obj.characteristics)
            sb.AppendLine("<b>" + e.label + ":</b> " + e.value);

        // Medidas reales calculadas de la geometría (mismo criterio que TapeMeasure).
        if (obj.mostrarMedidas)
        {
            string medidas = MedidasReales.Texto(obj.transform);
            if (medidas.Length > 0) sb.AppendLine(medidas);
        }
        characteristicsText.text = sb.ToString();

        ComputePlacement(out Vector3 pos, out float factor);
        transform.position = pos;
        transform.localScale = Vector3.one * (scale * factor);
        FaceHead();

        root.SetActive(true);
        obj.onInfoShown.Invoke();
    }

    public void Hide()
    {
        if (Current != null) Current.onInfoHidden.Invoke();
        Current = null;
        if (root != null) root.SetActive(false);
    }

    void LateUpdate()
    {
        if (Current == null) return;
        if (!ResolveHead()) return;

        if (followPlayer)
        {
            ComputePlacement(out Vector3 pos, out float factor);
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, t);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * (scale * factor), t);
        }

        FaceHead();
    }

    bool ResolveHead()
    {
        if (head != null) return true;
        var cam = Camera.main;
        if (cam == null) return false;
        head = cam.transform;
        return true;
    }

    // Posición ideal frente al jugador, sin tener en cuenta obstáculos.
    Vector3 GetTargetPosition()
    {
        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.04f)
        {
            Vector3 alt = head.forward.y < 0f ? head.up : -head.up;
            fwd = Vector3.ProjectOnPlane(alt, Vector3.up);
        }
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        return head.position + fwd * distance + right * sideOffset + Vector3.up * heightOffset;
    }

    // Calcula posición final y factor de reducción (1 = sin obstáculos).
    void ComputePlacement(out Vector3 pos, out float factor)
    {
        factor = 1f;
        pos = transform.position;
        if (!ResolveHead()) return;

        Vector3 eye = head.position;
        Vector3 target = GetTargetPosition();
        pos = target;
        if (!avoidClipping) return;

        Vector3 flat = target - eye;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;

        // Tamaño del panel en metros, y ejes para ubicar sus esquinas.
        var rt = transform as RectTransform;
        Vector2 size = (rt != null ? rt.rect.size : new Vector2(600f, 480f)) * scale;
        Quaternion face = Quaternion.LookRotation(flat);
        Vector3 halfRight = face * Vector3.right * (size.x * 0.5f);
        Vector3 halfUp = Vector3.up * (size.y * 0.5f);

        // 9 rayos desde los ojos hacia el panel (centro, esquinas y bordes).
        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iy = -1; iy <= 1; iy++)
            {
                Vector3 point = target + halfRight * ix + halfUp * iy;
                Vector3 dir = point - eye;
                float len = dir.magnitude;
                if (len < 0.001f) continue;
                dir /= len;

                float hit = NearestObstacle(eye, dir, len);
                if (hit < len)
                    factor = Mathf.Min(factor, (hit - margin) / len);
            }
        }

        factor = Mathf.Clamp(factor, minScaleFactor, 1f);
        pos = eye + (target - eye) * factor;   // acercar al jugador
    }

    float NearestObstacle(Vector3 origin, Vector3 dir, float maxDist)
    {
        int n = Physics.RaycastNonAlloc(origin, dir, hitBuffer, maxDist,
                                        obstacleMask, QueryTriggerInteraction.Ignore);
        float best = maxDist;
        for (int i = 0; i < n; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (ignoreGrabbables && c.GetComponentInParent<XRGrabInteractable>() != null) continue;
            if (hitBuffer[i].distance < best) best = hitBuffer[i].distance;
        }
        return best;
    }

    void FaceHead()
    {
        if (!ResolveHead()) return;
        Vector3 dir = transform.position - head.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    // ---------- Panel por defecto ----------

    void BuildDefault()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)transform).sizeDelta = new Vector2(600f, 480f);

        var bg = new GameObject("Panel", typeof(RectTransform));
        bg.transform.SetParent(transform, false);
        var rt = (RectTransform)bg.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = bg.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.06f, 0.08f, 0.12f, 0.93f);
        img.raycastTarget = false;
        root = bg;

        titleText = CreateText("Titulo", bg.transform,
            new Vector2(0f, 0.8f), new Vector2(1f, 1f), 44, FontStyles.Bold, new Color(0.4f, 0.85f, 1f));
        descriptionText = CreateText("Descripcion", bg.transform,
            new Vector2(0f, 0.45f), new Vector2(1f, 0.8f), 28, FontStyles.Normal, Color.white);
        characteristicsText = CreateText("Caracteristicas", bg.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0.45f), 28, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f));
    }

    static TMP_Text CreateText(string name, Transform parent, Vector2 aMin, Vector2 aMax,
                               float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(24f, 10f);
        rt.offsetMax = new Vector2(-24f, -10f);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.richText = true;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }
}