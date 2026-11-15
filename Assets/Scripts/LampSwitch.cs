using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class LampSwitch : XRSimpleInteractable
{
    [SerializeField] private Lamp lamp;

    [Tooltip("Grados de giro de muñeca para ir de 0 a 100% de intensidad.")]
    [SerializeField] private float degreesForFullRange = 90f;

    [Tooltip("Si giras menos que esto, se considera un toque y hace toggle.")]
    [SerializeField] private float tapThreshold = 10f;

    [Tooltip("Activado: girar en sentido horario sube la luz. Desactivado: antihorario.")]
    [SerializeField] private bool invert = true;

    private IXRSelectInteractor holder;
    private Quaternion startRot;
    private float startNormalized;
    private float dialOffset;
    private bool dialed;

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        if (lamp == null) return;

        holder = args.interactorObject;
        startRot = holder.GetAttachTransform(this).rotation;
        startNormalized = lamp.Normalized;
        dialed = false;
        dialOffset = 0f;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (lamp != null && holder != null && !dialed) lamp.Toggle();
        holder = null;
    }

    private void Update()
    {
        if (holder == null || lamp == null) return;

        Quaternion current = holder.GetAttachTransform(this).rotation;
        Quaternion delta = Quaternion.Inverse(startRot) * current;

        // Giro de muñeca = roll alrededor del eje forward del controlador
        float roll = Twist(delta, Vector3.forward);
        if (invert) roll = -roll;

        if (!dialed && Mathf.Abs(roll) > tapThreshold)
        {
            dialed = true;
            // Guardamos el umbral para que el brillo arranque suave, sin salto.
            dialOffset = Mathf.Sign(roll) * tapThreshold;
        }

        if (dialed)
            lamp.SetNormalized(startNormalized + (roll - dialOffset) / degreesForFullRange);
    }

    private static float Twist(Quaternion q, Vector3 axis)
    {
        float proj = Vector3.Dot(new Vector3(q.x, q.y, q.z), axis);
        float angle = 2f * Mathf.Atan2(proj, q.w) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(0f, angle);
    }
}
