using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menu: Tools > VR > Menu radial > Crear menu radial (Dia / Noche)
/// Monta en la escena abierta: el Ciclo Dia Noche (usa la Directional Light
/// como sol), un cielo procedural, el control de la lluvia (Particulas_Lluvia)
/// y el Menu Radial con todo conectado. Se puede ejecutar de nuevo para
/// reemplazar el menu.
/// </summary>
public static class MenuRadialSetup
{
    const string MenuName = "Menu Radial";
    const string CicloName = "Ciclo Dia Noche";
    const string LluviaControlName = "Control Lluvia";
    const string LluviaName = "Particulas_Lluvia";
    const string PaginaDiaNoche = "Dia y Noche";
    const string CieloPath = "Assets/Materials/Cielo_Procedural.mat";

    [MenuItem("Tools/VR/Menu radial/Crear menu radial (Dia - Noche)")]
    static void Crear()
    {
        var ciclo = CrearCiclo();
        if (ciclo == null) return;
        AsignarCieloProcedural();

        var viejo = GameObject.Find(MenuName);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        var go = new GameObject(MenuName);
        Undo.RegisterCreatedObjectUndo(go, "Crear menu radial");
        var menu = go.AddComponent<RadialMenu>();

        var principal = new PaginaMenu { nombre = "Menu" };
        principal.opciones.Add(new OpcionMenu { texto = "Dia / Noche", icono = IconoMenu.Sol, abrirPagina = PaginaDiaNoche });
        var lluvia = CrearControlLluvia();
        if (lluvia != null) principal.opciones.Add(Opcion("Lluvia", IconoMenu.Lluvia, lluvia.AlternarLluvia));

        var diaNoche = new PaginaMenu { nombre = PaginaDiaNoche };
        diaNoche.opciones.Add(Opcion("Amanecer", IconoMenu.Amanecer, ciclo.IrAAmanecer));
        diaNoche.opciones.Add(Opcion("Dia", IconoMenu.Sol, ciclo.IrADia));
        diaNoche.opciones.Add(Opcion("Atardecer", IconoMenu.Atardecer, ciclo.IrAAtardecer));
        diaNoche.opciones.Add(Opcion("Noche", IconoMenu.Luna, ciclo.IrANoche));

        menu.paginas.Clear();
        menu.paginas.Add(principal);
        menu.paginas.Add(diaNoche);
        EditorUtility.SetDirty(menu);

        if (Object.FindAnyObjectByType<XRUIInputModule>() == null)
            Debug.LogWarning("[Menu radial] Falta un EventSystem con XR UI Input Module: " +
                             "GameObject > XR > UI Event System. Sin el, los botones no responden.");

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[Menu radial] Listo. Dale Play y aprieta A (B en el simulador). Guarda la escena con Ctrl+S.");
    }

    static OpcionMenu Opcion(string texto, IconoMenu icono, UnityAction accion)
    {
        var op = new OpcionMenu { texto = texto, icono = icono };
        UnityEventTools.AddPersistentListener(op.alElegir, accion);
        return op;
    }

    static DayNightCycle CrearCiclo()
    {
        var ciclo = Object.FindAnyObjectByType<DayNightCycle>();
        if (ciclo != null) return ciclo;

        var go = new GameObject(CicloName);
        Undo.RegisterCreatedObjectUndo(go, "Crear ciclo dia noche");
        ciclo = go.AddComponent<DayNightCycle>();
        ciclo.ValoresPorDefecto();
        if (ciclo.sol == null)
        {
            Undo.DestroyObjectImmediate(go);
            EditorUtility.DisplayDialog("Menu radial",
                "No encontre una Directional Light en la escena para usarla como sol. Agrega una y vuelve a intentar.", "Ok");
            return null;
        }
        EditorUtility.SetDirty(ciclo);
        return ciclo;
    }

    static LluviaControl CrearControlLluvia()
    {
        var control = Object.FindAnyObjectByType<LluviaControl>();
        if (control != null) return control;

        var particulas = GameObject.Find(LluviaName);
        if (particulas == null)
        {
            Debug.LogWarning($"[Menu radial] No encontre '{LluviaName}' (activo) en la escena; el menu se crea sin la opcion de lluvia.");
            return null;
        }
        var go = new GameObject(LluviaControlName);
        Undo.RegisterCreatedObjectUndo(go, "Crear control de lluvia");
        control = go.AddComponent<LluviaControl>();
        control.lluvia = particulas;
        control.lloviendoAlIniciar = false;
        EditorUtility.SetDirty(control);
        return control;
    }

    // El cielo de cubemap del template no cambia con el sol; el procedural si.
    static void AsignarCieloProcedural()
    {
        var actual = RenderSettings.skybox;
        if (actual != null && actual.shader != null && actual.shader.name == "Skybox/Procedural") return;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(CieloPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Procedural"));
            mat.SetFloat("_AtmosphereThickness", 1f);
            AssetDatabase.CreateAsset(mat, CieloPath);
        }
        RenderSettings.skybox = mat;
        Debug.Log($"[Menu radial] Cielo cambiado a Skybox/Procedural (antes: {(actual != null ? actual.name : "ninguno")}). " +
                  "Para volver, arrastra el material anterior en Window > Rendering > Lighting > Environment.");
    }
}
