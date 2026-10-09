using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menus para ampliar el cinturon sin escribir codigo:
///   Tools > VR > Cinturon > 1. Agregar ranura
///   Tools > VR > Cinturon > 2. Agregar herramienta generica a la ranura seleccionada
///   Tools > VR > Cinturon > 3. Agregar tablet de menu a la ranura seleccionada
/// Primero se crea el cinturon con: Tools > VR > Crear cinturon con herramientas.
/// </summary>
public static class CinturonAgregar
{
    const float Radius = 0.28f;
    // Angulos alrededor de la cintura (0 = frente, 90 = derecha). Los traseros van al final.
    static readonly float[] Angles = { 90f, -90f, 40f, -40f, 140f, -140f, 15f, -15f };

    // ---------------------------------------------------------------- 1. ranura

    [MenuItem("Tools/VR/Cinturon/1. Agregar ranura")]
    static void AddSlot()
    {
        var belt = GameObject.Find("Cinturon");
        if (belt == null)
        {
            EditorUtility.DisplayDialog("Cinturon",
                "No hay un Cinturon en la escena. Primero usa Tools > VR > Crear cinturon con herramientas.", "Ok");
            return;
        }

        Vector3 pos = FindFreePosition(belt.transform, out bool found);
        var mat = CinturonSetup.MakeMat("Cinturon_Base", new Color(0.12f, 0.12f, 0.14f), false);
        string name = GameObjectUtility.GetUniqueNameForSibling(belt.transform, "Slot_Nuevo");
        var slot = CinturonSetup.CreateSlot(belt.transform, name, pos, mat);
        Undo.RegisterCreatedObjectUndo(slot.gameObject, "Agregar ranura");

        if (!found)
            Debug.LogWarning("[Cinturon] No quedaban posiciones libres predefinidas. Mueve la ranura a mano (Position local).");

        Selection.activeGameObject = slot.gameObject;
        MarkDirty();
        Debug.Log("[Cinturon] Ranura '" + name + "' creada. Renombrala y muevela si quieres; luego usa el menu 2 o 3 con ella seleccionada.");
    }

    static Vector3 FindFreePosition(Transform belt, out bool found)
    {
        foreach (float a in Angles)
        {
            float r = a * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(r) * Radius, 0f, Mathf.Cos(r) * Radius);
            bool taken = false;
            foreach (var s in belt.GetComponentsInChildren<XRSocketInteractor>())
            {
                if (Vector3.Distance(belt.InverseTransformPoint(s.transform.position), p) < 0.12f) { taken = true; break; }
            }
            if (!taken) { found = true; return p; }
        }
        found = false;
        return new Vector3(0f, 0f, Radius + 0.05f);
    }

    // ------------------------------------------------------ 2. herramienta generica

    [MenuItem("Tools/VR/Cinturon/2. Agregar herramienta generica a la ranura seleccionada")]
    static void AddGenericTool()
    {
        var slot = GetSelectedFreeSlot();
        if (slot == null) return;

        var tools = GetOrCreateToolsRoot();
        string name = GameObjectUtility.GetUniqueNameForSibling(tools, "Herramienta_Nueva");
        var grab = CinturonSetup.CreateToolRoot(tools, name, slot.transform.position);
        Undo.RegisterCreatedObjectUndo(grab.gameObject, "Agregar herramienta");

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cuerpo";
        cube.transform.SetParent(grab.transform, false);
        cube.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        cube.transform.localScale = new Vector3(0.05f, 0.05f, 0.09f);
        cube.GetComponent<MeshRenderer>().sharedMaterial =
            CinturonSetup.MakeMat("Herramienta_Generica", new Color(0.2f, 0.6f, 0.9f), false);

        grab.gameObject.AddComponent<ToolEvents>();
        CinturonSetup.BindToSlot(slot, grab);

        Selection.activeGameObject = grab.gameObject;
        MarkDirty();
        Debug.Log("[Cinturon] Herramienta '" + name + "' creada y conectada a '" + slot.name + "'. Configura el gatillo en su componente Tool Events.");
    }

    // -------------------------------------------------------------- 3. tablet menu

    [MenuItem("Tools/VR/Cinturon/3. Agregar tablet de menu a la ranura seleccionada")]
    static void AddMenuTablet()
    {
        var slot = GetSelectedFreeSlot();
        if (slot == null) return;

        var tools = GetOrCreateToolsRoot();
        string name = GameObjectUtility.GetUniqueNameForSibling(tools, "Menu_Tablet");
        var grab = CinturonSetup.CreateToolRoot(tools, name, slot.transform.position);
        Undo.RegisterCreatedObjectUndo(grab.gameObject, "Agregar tablet");

        // Cuerpo de la tablet: plana, extendida hacia adelante desde el agarre.
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Cuerpo";
        body.transform.SetParent(grab.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0.07f);
        body.transform.localScale = new Vector3(0.2f, 0.01f, 0.14f);
        body.GetComponent<MeshRenderer>().sharedMaterial =
            CinturonSetup.MakeMat("Menu_Tablet_Cuerpo", new Color(0.15f, 0.16f, 0.2f), false);

        // Panel: Canvas en World Space sobre la cara superior (0.2 x 0.14 m).
        var canvasGo = new GameObject("Panel");
        canvasGo.transform.SetParent(grab.transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0.0056f, 0.07f);
        canvasGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        canvasGo.transform.localScale = Vector3.one * 0.0005f;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400f, 280f);
        var bg = canvasGo.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

        MakeButton(canvasGo.transform, "Boton 1", new Vector2(0f, 55f));
        MakeButton(canvasGo.transform, "Boton 2", new Vector2(0f, -55f));

        var tablet = grab.gameObject.AddComponent<MenuTablet>();
        tablet.panel = canvasGo;
        CinturonSetup.BindToSlot(slot, grab);
        EditorUtility.SetDirty(tablet);

        if (Object.FindAnyObjectByType<EventSystem>() == null)
            Debug.LogWarning("[Cinturon] No hay EventSystem en la escena. Crealo con GameObject > XR > UI Event System, o los botones no responderan.");
        else if (Object.FindAnyObjectByType<XRUIInputModule>() == null)
            Debug.LogWarning("[Cinturon] El EventSystem no tiene XR UI Input Module. Reemplaza el Input Module por XR UI Input Module, o los botones no responderan.");

        Selection.activeGameObject = grab.gameObject;
        MarkDirty();
        Debug.Log("[Cinturon] Tablet '" + name + "' creada en '" + slot.name + "'. Configura cada boton en su OnClick (Hierarchy > " + name + " > Panel).");
    }

    static void MakeButton(Transform parent, string label, Vector2 pos)
    {
        var b = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        b.name = "Boton_" + label.Replace(' ', '_');
        b.transform.SetParent(parent, false);
        var rt = b.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(320f, 80f);
        var txt = b.GetComponentInChildren<TMP_Text>();
        txt.text = label;
        txt.fontSize = 30f;
    }

    // ----------------------------------------------------------------- utilidades

    static XRSocketInteractor GetSelectedFreeSlot()
    {
        XRSocketInteractor slot = null;
        var go = Selection.activeGameObject;
        if (go != null) slot = go.GetComponentInParent<XRSocketInteractor>();

        if (slot == null)
        {
            EditorUtility.DisplayDialog("Cinturon",
                "Selecciona primero una ranura (un objeto Slot_...) en la Hierarchy. Si no tienes una, usa el menu 1. Agregar ranura.",
                "Ok");
            return null;
        }
        if (slot.startingSelectedInteractable != null)
        {
            EditorUtility.DisplayDialog("Cinturon",
                "La ranura '" + slot.name + "' ya tiene la herramienta '" + slot.startingSelectedInteractable.name +
                "'. Agrega otra ranura (menu 1) o elige una ranura vacia.", "Ok");
            return null;
        }
        return slot;
    }

    static Transform GetOrCreateToolsRoot()
    {
        var go = GameObject.Find("Herramientas");
        if (go == null)
        {
            go = new GameObject("Herramientas");
            Undo.RegisterCreatedObjectUndo(go, "Crear Herramientas");
        }
        return go.transform;
    }

    static void MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
