using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// "Estas aqui": un punto que late sobre la maqueta en la posicion real del jugador,
/// con una aguja vertical para verlo aunque este bajo un techo.
/// Se oculta si el jugador esta fuera del area que cubre la maqueta.
/// Va en la maqueta (junto a MaquetaExhibicion); lo agrega Tools > VR > Maqueta.
/// </summary>
[RequireComponent(typeof(MaquetaExhibicion))]
public class PuntoEstasAqui : MonoBehaviour
{
    public Material material;
    [Tooltip("Tamano del punto en el mundo, en metros (no cambia con la escala de la maqueta).")]
    public float tamano = 0.014f;
    [Tooltip("Cuanto sobresale la aguja por encima del techo mas alto, en metros reales.")]
    public float sobreTecho = 1.5f;

    MaquetaExhibicion maqueta;
    XROrigin origen;
    Transform punto;
    LineRenderer aguja;
    Bounds area; // en coordenadas locales de la maqueta (= metros reales)

    void Start()
    {
        maqueta = GetComponent<MaquetaExhibicion>();
        origen = FindAnyObjectByType<XROrigin>();

        // Area cubierta por la maqueta, en su espacio local.
        bool hay = false;
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds b = mf.sharedMesh.bounds;
            Vector3 c = m.MultiplyPoint3x4(b.center);
            if (!hay) { area = new Bounds(c, Vector3.zero); hay = true; }
            area.Encapsulate(m.MultiplyPoint3x4(b.min));
            area.Encapsulate(m.MultiplyPoint3x4(b.max));
        }

        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "EstasAqui";
        Destroy(s.GetComponent<Collider>());
        punto = s.transform;
        punto.SetParent(transform, false);
        var r = s.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (material != null) r.sharedMaterial = material;

        aguja = s.AddComponent<LineRenderer>();
        aguja.useWorldSpace = true;
        aguja.positionCount = 2;
        aguja.widthMultiplier = 0.0025f;
        aguja.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (material != null) aguja.sharedMaterial = material;
        s.SetActive(false);
    }

    void LateUpdate()
    {
        if (punto == null || maqueta == null) return;
        var cam = origen != null && origen.Camera != null ? origen.Camera : Camera.main;
        if (cam == null) return;

        // Pies del jugador: la cabeza en planta, a la altura del piso del XR Origin.
        Vector3 pies = cam.transform.position;
        if (origen != null) pies.y = origen.Origin.transform.position.y;

        bool dentro = maqueta.RealALocal(pies, out Vector3 local)
                      && local.x >= area.min.x && local.x <= area.max.x
                      && local.z >= area.min.z && local.z <= area.max.z;
        if (punto.gameObject.activeSelf != dentro) punto.gameObject.SetActive(dentro);
        if (!dentro) return;

        float escala = Mathf.Max(1e-5f, transform.lossyScale.x);
        float late = 1f + 0.35f * Mathf.Sin(Time.time * 5f);
        punto.localPosition = local + Vector3.up * (tamano * 0.5f / escala);
        punto.localScale = Vector3.one * (tamano * late / escala);

        Vector3 abajo = punto.position;
        Vector3 arriba = transform.TransformPoint(new Vector3(local.x, area.max.y + sobreTecho, local.z));
        aguja.SetPosition(0, abajo);
        aguja.SetPosition(1, arriba);
    }
}
