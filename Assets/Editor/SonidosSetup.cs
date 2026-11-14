using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > VR > Sonido > Agregar sonidos a la escena
/// Crea el objeto "Sonidos" con SonidosEscena (ambiente, puertas e interruptores).
/// Si ya existe, solo lo selecciona.
/// </summary>
public static class SonidosSetup
{
    const string Nombre = "Sonidos";

    [MenuItem("Tools/VR/Sonido/Agregar sonidos a la escena")]
    static void Agregar()
    {
        var existente = Object.FindAnyObjectByType<SonidosEscena>();
        if (existente != null)
        {
            Selection.activeGameObject = existente.gameObject;
            Debug.Log("[Sonidos] Ya hay un objeto de sonidos en la escena; lo seleccione.");
            return;
        }

        var go = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(go, "Agregar sonidos");
        go.AddComponent<SonidosEscena>();
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[Sonidos] Listo. Guarda la escena (Ctrl+S) y dale Play.");
    }
}
