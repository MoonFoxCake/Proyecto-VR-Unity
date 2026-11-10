using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Interactable de la hoja de la puerta. Igual que XRSimpleInteractable, pero su punto
/// de agarre se coloca donde toco el rayo (o la mano) y queda pegado a la hoja.
/// Asi, al agarrar de lejos, el rayo termina en ese punto y sigue a la puerta mientras gira,
/// en vez de doblarse hacia el pivote de la hoja (que esta en el piso).
/// Las puertas convertidas antes de que existiera se actualizan con Tools > VR > Puertas > 4.
/// (No se cambia en tiempo de ejecucion: quitar interactables mientras carga la escena rompe
/// el registro de XRI para TODOS los objetos.)
/// </summary>
public class AgarrePuerta : XRSimpleInteractable
{
    Transform punto;

    protected override void Awake()
    {
        base.Awake();
        punto = new GameObject("PuntoAgarre").transform;
        punto.SetParent(transform, false);
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        return punto != null ? punto : transform;
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        // El interactor ya movio su propio punto de agarre al impacto del rayo (o esta en la mano).
        var t = args.interactorObject.GetAttachTransform(this);
        if (punto != null && t != null) punto.SetPositionAndRotation(t.position, t.rotation);
        base.OnSelectEntering(args);
    }
}
