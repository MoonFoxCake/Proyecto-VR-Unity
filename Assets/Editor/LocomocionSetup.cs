using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Ajustes de comodidad del XR Origin:
///   - Giro suave con el joystick (en vez de giro por saltos).
///   - Sin "tunel" (vignette) al caminar o girar.
/// Se guarda en el PREFAB del XR Origin que usa la escena (no en la escena),
/// asi no genera conflictos en SampleScene.unity.
/// </summary>
public static class LocomocionSetup
{
    [MenuItem("Tools/VR/Locomocion/Giro suave y sin tunel")]
    static void GiroSuaveSinTunel() => Aplicar(giroSuave: true, tunel: false);

    [MenuItem("Tools/VR/Locomocion/Volver a giro por saltos y con tunel")]
    static void Original() => Aplicar(giroSuave: false, tunel: true);

    static void Aplicar(bool giroSuave, bool tunel)
    {
        var managers = Object.FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include);
        var vinetas = Object.FindObjectsByType<TunnelingVignetteController>(FindObjectsInactive.Include);
        Debug.Log("[Locomocion] Encontre " + managers.Length + " ControllerInputActionManager y " + vinetas.Length + " TunnelingVignetteController en la escena.");
        if (managers.Length == 0 && vinetas.Length == 0)
        {
            Debug.LogWarning("[Locomocion] No encontre el XR Origin en la escena abierta.");
            EditorUtility.DisplayDialog("Locomocion", "No encontre el XR Origin en la escena abierta.", "OK");
            return;
        }

        // Prefabs (el mas externo) de donde vienen esos objetos.
        var rutas = new HashSet<string>();
        foreach (var c in managers.Cast<Component>().Concat(vinetas))
        {
            var raiz = PrefabUtility.GetOutermostPrefabInstanceRoot(c.gameObject);
            if (raiz == null) continue;
            string ruta = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(raiz);
            if (!string.IsNullOrEmpty(ruta)) rutas.Add(ruta);
        }

        int cambiosPrefab = 0;
        foreach (var ruta in rutas)
        {
            var contenido = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                cambiosPrefab += Ajustar(contenido.GetComponentsInChildren<ControllerInputActionManager>(true),
                                         contenido.GetComponentsInChildren<TunnelingVignetteController>(true),
                                         giroSuave, tunel, false);
                PrefabUtility.SaveAsPrefabAsset(contenido, ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(contenido); }
        }

        // Si la escena tiene overrides propios (o el rig no es prefab), tambien se ajusta ahi.
        managers = Object.FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include);
        vinetas = Object.FindObjectsByType<TunnelingVignetteController>(FindObjectsInactive.Include);
        int cambiosEscena = Ajustar(managers, vinetas, giroSuave, tunel, true);
        if (cambiosEscena > 0) EditorSceneManager.MarkSceneDirty(managers.Length > 0 ? managers[0].gameObject.scene : vinetas[0].gameObject.scene);

        string texto = (giroSuave ? "Giro suave" : "Giro por saltos") + " y " + (tunel ? "con tunel." : "sin tunel.") +
                       "\n\nPrefab(s) editado(s):\n" + (rutas.Count > 0 ? string.Join("\n", rutas) : "(ninguno)") +
                       "\n\nCambios en el prefab: " + cambiosPrefab + "\nCambios directos en la escena: " + cambiosEscena +
                       (cambiosEscena > 0 ? " (la escena tenia overrides; guarda la escena)" : "");
        Debug.Log("[Locomocion] " + texto.Replace("\n", " "));
        EditorUtility.DisplayDialog("Locomocion", texto, "OK");
    }

    static int Ajustar(ControllerInputActionManager[] managers, TunnelingVignetteController[] vinetas,
                       bool giroSuave, bool tunel, bool conUndo)
    {
        int cambios = 0;
        foreach (var m in managers)
        {
            var so = new SerializedObject(m);
            var p = so.FindProperty("m_SmoothTurnEnabled");
            if (p == null || p.boolValue == giroSuave) continue;
            p.boolValue = giroSuave;
            if (conUndo) so.ApplyModifiedProperties(); else so.ApplyModifiedPropertiesWithoutUndo();
            cambios++;
        }
        foreach (var v in vinetas)
        {
            if (v.gameObject.activeSelf == tunel) continue;
            if (conUndo) Undo.RecordObject(v.gameObject, "Tunel");
            v.gameObject.SetActive(tunel);
            cambios++;
        }
        return cambios;
    }
}
