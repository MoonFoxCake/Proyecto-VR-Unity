using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Mantiene la herramienta en su ranura del cinturon:
///   - Si se suelta del cinturon sin que una mano la tome (un golpe, un tiron al girar),
///     y sigue cerca de su ranura, se vuelve a enganchar al instante.
///   - Si la sueltas en otro lado y nadie la sostiene durante returnDelay segundos
///     (o cae por debajo de fallLimitY), vuelve sola a su ranura y queda enganchada.
/// El enganche se hace directo con el Interaction Manager (no depende de que el
/// trigger de la ranura la detecte justo ese cuadro).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ToolAutoReturn : MonoBehaviour
{
    public XRSocketInteractor homeSocket;
    public float returnDelay = 5f;
    public float fallLimitY = -5f;
    [Tooltip("Metros: si queda suelta a esta distancia de su ranura, se engancha al instante.")]
    public float distanciaEnganche = 0.2f;

    XRGrabInteractable grab;
    Rigidbody rb;
    float freeTime;
    bool salioDelCinturon;
    float salioEn;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable() => grab.selectExited.AddListener(OnSoltada);
    void OnDisable() => grab.selectExited.RemoveListener(OnSoltada);

    void OnSoltada(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor && !args.isCanceled)
        {
            salioDelCinturon = true;
            salioEn = Time.time;
        }
    }

    void Update()
    {
        if (homeSocket == null) return;

        if (grab.isSelected)
        {
            freeTime = 0f;
            salioDelCinturon = false;
            return;
        }

        // Se solto del cinturon y ninguna mano la tomo: se cayo.
        if (salioDelCinturon && Time.time - salioEn > 0.1f)
        {
            salioDelCinturon = false;
            Debug.Log("[Cinturon] " + name + " se solto del cinturon sin que una mano la tomara; se vuelve a enganchar.", this);
        }

        Transform attach = homeSocket.GetAttachTransform(grab);
        bool cerca = Vector3.Distance(transform.position, attach.position) <= distanciaEnganche;

        freeTime += Time.deltaTime;
        if (cerca || freeTime >= returnDelay || transform.position.y < fallLimitY)
            Enganchar(attach);
    }

    void Enganchar(Transform attach)
    {
        freeTime = 0f;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(attach.position, attach.rotation);

        var manager = homeSocket.interactionManager;
        if (manager != null && homeSocket.isActiveAndEnabled && !homeSocket.hasSelection)
            manager.SelectEnter((IXRSelectInteractor)homeSocket, (IXRSelectInteractable)grab);
    }
}
