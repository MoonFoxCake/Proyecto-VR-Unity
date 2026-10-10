using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reinicia la escena actual. Todo (jugador, herramientas, objetos) vuelve
/// a su estado inicial. Úsalo desde el OnClick de un botón de la tablet
/// o desde el On Activated de una herramienta (Tool Events).
/// </summary>
public class SceneResetter : MonoBehaviour
{
    [Tooltip("Segundos de espera antes de recargar (útil para un fade).")]
    public float delay = 0f;

    bool resetting;

    public void ResetScene()
    {
        if (resetting) return;          // evita doble clic
        resetting = true;
        StartCoroutine(ResetRoutine());
    }

    IEnumerator ResetRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}