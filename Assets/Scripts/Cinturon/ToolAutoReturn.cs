using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Si sueltas la herramienta y nadie la sostiene (ni el cinturón) durante
/// returnDelay segundos, vuelve sola a su ranura. Evita perderla en el vacío.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ToolAutoReturn : MonoBehaviour
{
    public XRSocketInteractor homeSocket;
    public float returnDelay = 5f;
    public float fallLimitY = -5f;

    XRGrabInteractable grab;
    Rigidbody rb;
    float freeTime;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (homeSocket == null) return;

        if (grab.isSelected) { freeTime = 0f; return; }

        freeTime += Time.deltaTime;
        if (freeTime >= returnDelay || transform.position.y < fallLimitY)
        {
            freeTime = 0f;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Transform attach = homeSocket.GetAttachTransform(grab);
            transform.SetPositionAndRotation(attach.position, attach.rotation);
        }
    }
}
