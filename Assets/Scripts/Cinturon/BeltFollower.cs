using UnityEngine;

/// <summary>
/// Hace que el cinturón siga la cintura del jugador.
/// - Posición: la de la cabeza, pero más abajo (heightBelowHead).
/// - Rotación: solo el giro horizontal (yaw) de la cabeza, suavizado y con
///   zona muerta, para que el cinturón no se bambolee al mirar a los lados.
/// No lo cuelgues de la cámara: se inclinaría al mirar arriba o abajo.
/// </summary>
public class BeltFollower : MonoBehaviour
{
    [Tooltip("Cabeza del jugador. Si queda vacío usa Camera.main.")]
    public Transform head;

    [Tooltip("Cuántos metros por debajo de la cabeza queda el cinturón.")]
    public float heightBelowHead = 0.6f;

    [Tooltip("Velocidad con la que el cinturón se gira para alcanzar la mirada.")]
    public float yawFollowSpeed = 4f;

    [Tooltip("Grados que puedes girar la cabeza antes de que el cinturón empiece a seguirte.")]
    public float yawDeadZone = 25f;

    float yaw;
    bool initialized;
    bool following;

    void LateUpdate()
    {
        if (head == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            head = cam.transform;
        }

        Vector3 hp = head.position;
        transform.position = new Vector3(hp.x, hp.y - heightBelowHead, hp.z);

        // Dirección horizontal hacia donde mira la cabeza.
        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.04f)
        {
            // Mirando casi recto arriba o abajo: usa el "arriba" de la cabeza.
            Vector3 alt = head.forward.y < 0f ? head.up : -head.up;
            fwd = Vector3.ProjectOnPlane(alt, Vector3.up);
        }
        if (fwd.sqrMagnitude < 0.0001f) return;

        float target = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        if (!initialized)
        {
            yaw = target;
            initialized = true;
        }

        float diff = Mathf.Abs(Mathf.DeltaAngle(yaw, target));
        if (diff > yawDeadZone) following = true;
        if (diff < 2f) following = false;

        if (following)
        {
            float t = 1f - Mathf.Exp(-yawFollowSpeed * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, target, t);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
