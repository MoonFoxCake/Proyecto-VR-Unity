using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Tablet de menú: agárrala del cinturón y aprieta el gatillo para
/// mostrar u ocultar el panel (un Canvas en World Space). Los botones del
/// panel se configuran en su evento OnClick, desde el Inspector.
/// Métodos públicos útiles para botones: Toggle(), Show(), Hide().
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class MenuTablet : MonoBehaviour
{
    public GameObject panel;
    public bool startVisible = false;
    [Tooltip("Oculta el panel al guardar la tablet en el cinturón.")]
    public bool hideWhenStored = true;

    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (panel != null) panel.SetActive(startVisible);
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

    void OnActivated(ActivateEventArgs args) { Toggle(); }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (hideWhenStored && args.interactorObject is XRSocketInteractor) Hide();
    }

    public void Toggle() { if (panel != null) panel.SetActive(!panel.activeSelf); }
    public void Show() { if (panel != null) panel.SetActive(true); }
    public void Hide() { if (panel != null) panel.SetActive(false); }
}
