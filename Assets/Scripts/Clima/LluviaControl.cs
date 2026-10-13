using System;
using UnityEngine;

/// <summary>
/// Activa / desactiva la lluvia (el objeto con las particulas, p. ej. Particulas_Lluvia).
/// Metodos publicos para botones: AlternarLluvia(), ActivarLluvia(), DesactivarLluvia().
/// </summary>
public class LluviaControl : MonoBehaviour
{
    [Tooltip("Objeto padre de las particulas de lluvia (y su sonido, si tiene).")]
    public GameObject lluvia;
    public bool lloviendoAlIniciar = false;

    public bool Lloviendo => lluvia != null && lluvia.activeSelf;

    /// <summary>Se dispara al activar (true) o desactivar (false) la lluvia.</summary>
    public event Action<bool> LluviaCambiada;

    void Reset()
    {
        var go = GameObject.Find("Particulas_Lluvia");
        if (go != null) lluvia = go;
    }

    void Start() => SetLluvia(lloviendoAlIniciar);

    public void AlternarLluvia() => SetLluvia(!Lloviendo);
    public void ActivarLluvia() => SetLluvia(true);
    public void DesactivarLluvia() => SetLluvia(false);

    public void SetLluvia(bool valor)
    {
        if (lluvia == null) return;
        lluvia.SetActive(valor);
        LluviaCambiada?.Invoke(valor);
    }
}
