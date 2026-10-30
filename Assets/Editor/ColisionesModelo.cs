using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Agrega MeshColliders a las partes "caminables" del modelo (suelos, muros,
/// escaleras, techos...) para que el jugador pueda pisarlos y no atravesarlos.
/// Uso: selecciona en la Hierarchy la RAIZ del modelo (el objeto del GLB) y usa
///   Tools > VR > Colisiones > Agregar al modelo seleccionado
/// No toca puertas, ventanas, barandas ni muebles (por eso se puede pasar por las puertas).
/// </summary>
public static class ColisionesModelo
{
    // Edita este patron si tu modelo usa otros nombres. No distingue mayusculas.
    // Coincide con el nombre del objeto o el de cualquiera de sus padres dentro del modelo.
    const string PatronSolidos = "muro|pilar|suelo|escalera|tramo|cubierta|techo|topogr|piso|losa|acera";

    [MenuItem("Tools/VR/Colisiones/Agregar al modelo seleccionado")]
    static void Agregar()
    {
        var raiz = Seleccion();
        if (raiz == null) return;

        var rx = new Regex(PatronSolidos, RegexOptions.IgnoreCase);
        int agregados = 0, yaTenian = 0, sinMalla = 0;

        foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!Coincide(rx, mf.transform, raiz.transform)) continue;
            if (mf.sharedMesh == null) { sinMalla++; continue; }
            if (mf.GetComponent<Collider>() != null) { yaTenian++; continue; }

            var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            agregados++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        string msg = "Colliders agregados: " + agregados + "\nYa tenian collider: " + yaTenian +
                     "\nSin malla (ignorados): " + sinMalla;
        Debug.Log("[Colisiones] " + msg.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Colisiones", msg + "\n\nGuarda la escena (Ctrl+S).", "Ok");
    }

    [MenuItem("Tools/VR/Colisiones/Quitar del modelo seleccionado")]
    static void Quitar()
    {
        var raiz = Seleccion();
        if (raiz == null) return;

        var rx = new Regex(PatronSolidos, RegexOptions.IgnoreCase);
        int quitados = 0;
        foreach (var mc in raiz.GetComponentsInChildren<MeshCollider>(true))
        {
            if (!Coincide(rx, mc.transform, raiz.transform)) continue;
            Undo.DestroyObjectImmediate(mc);
            quitados++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Colisiones] MeshColliders quitados: " + quitados);
    }

    static GameObject Seleccion()
    {
        var go = Selection.activeGameObject;
        if (go == null)
            EditorUtility.DisplayDialog("Colisiones",
                "Selecciona primero en la Hierarchy la raiz del modelo (el objeto del GLB).", "Ok");
        return go;
    }

    static bool Coincide(Regex rx, Transform t, Transform raiz)
    {
        for (; t != null; t = t.parent)
        {
            if (rx.IsMatch(t.name)) return true;
            if (t == raiz) break;
        }
        return false;
    }
}
