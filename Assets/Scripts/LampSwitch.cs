using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class LampSwitch : UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable
{
    [SerializeField] private Lamp lamp;

    [Tooltip("On: each press cycles Off→Low→Med→High→Off. Off: just toggles on/off.")]
    [SerializeField] private bool cycleBrightness = true;

    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener(HandleSelect);
    }

    protected override void OnDisable()
    {
        selectEntered.RemoveListener(HandleSelect); 
        base.OnDisable();
    }

    private void HandleSelect(SelectEnterEventArgs args)
    {
        if (lamp == null) return;

        if (cycleBrightness) lamp.CycleBrightness();
        else                 lamp.Toggle();
    }
}