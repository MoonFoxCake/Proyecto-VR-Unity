using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Herramienta configurable desde el Inspector, sin escribir código.
/// Arrastra objetos a los eventos de abajo y elige qué método ejecutar.
///   On Activated : gatillo apretado con la herramienta en la mano.
///   On Deactivated: gatillo soltado.
///   On Grabbed   : alguien agarró la herramienta (no cuenta el cinturón).
///   On Released  : la soltaron (no cuenta guardarla en el cinturón).
/// Ejemplo: On Activated -> arrastra un panel -> GameObject.SetActive.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class ToolEvents : MonoBehaviour
{
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;

    XRGrabInteractable grab;

    void Awake() { grab = GetComponent<XRGrabInteractable>(); }

    void OnEnable()
    {
        grab.activated.AddListener(OnActivated);
        grab.deactivated.AddListener(OnDeactivated);
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.activated.RemoveListener(OnActivated);
        grab.deactivated.RemoveListener(OnDeactivated);
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnActivated(ActivateEventArgs args) { onActivated.Invoke(); }
    void OnDeactivated(DeactivateEventArgs args) { onDeactivated.Invoke(); }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (!(args.interactorObject is XRSocketInteractor)) onGrabbed.Invoke();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (!(args.interactorObject is XRSocketInteractor)) onReleased.Invoke();
    }
}
