using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Herramienta de ejemplo: el gatillo enciende y apaga una luz.</summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class ToggleLightTool : MonoBehaviour
{
    public Light lightSource;
    XRGrabInteractable grab;

    void Awake() { grab = GetComponent<XRGrabInteractable>(); }
    void OnEnable() { grab.activated.AddListener(OnActivated); }
    void OnDisable() { grab.activated.RemoveListener(OnActivated); }

    void OnActivated(ActivateEventArgs args)
    {
        if (lightSource != null) lightSource.enabled = !lightSource.enabled;
    }
}
