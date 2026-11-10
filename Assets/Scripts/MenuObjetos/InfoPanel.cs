using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel de información en World Space que aparece SIEMPRE frente al jugador.
/// Un solo panel para todos los objetos. Si no asignas la UI, se crea uno
/// por defecto automáticamente (fondo oscuro + título, descripción y datos).
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

    [Header("Apariencia")]
    public float scale = 0.002f;

    public InspectableObject Current { get; private set; }

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
        characteristicsText.text = sb.ToString();

        transform.position = GetTargetPosition();
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
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, GetTargetPosition(), t);
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

    Vector3 GetTargetPosition()
    {
        if (!ResolveHead()) return transform.position;

        // Dirección horizontal hacia donde mira el jugador (ignora inclinar la cabeza).
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
        ((RectTransform)transform).sizeDelta = new Vector2(600f, 400f);

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