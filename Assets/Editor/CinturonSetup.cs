using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Menú: Tools > VR > Crear cinturon con herramientas
/// Monta en la escena abierta: el cinturon (cuelga del XR Origin), dos ranuras
/// (XR Socket Interactor) y dos herramientas agarrables: Medidor y Linterna.
/// Se puede ejecutar de nuevo para reemplazar lo creado.
/// </summary>
public static class CinturonSetup
{
    const string BeltName = "Cinturon";
    const string ToolsName = "Herramientas";
    const string MatDir = "Assets/Materials";

    [MenuItem("Tools/VR/Crear cinturon con herramientas")]
    static void Create()
    {
        var origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin == null)
        {
            EditorUtility.DisplayDialog("Cinturon",
                "No encontre un XR Origin en la escena abierta. Abre la escena donde esta tu rig VR y vuelve a intentarlo.",
                "Ok");
            return;
        }

        var oldBelt = GameObject.Find(BeltName);
        var oldTools = GameObject.Find(ToolsName);
        if (oldBelt != null || oldTools != null)
        {
            if (!EditorUtility.DisplayDialog("Cinturon",
                    "Ya existe un Cinturon o unas Herramientas en la escena. Quieres reemplazarlos?",
                    "Reemplazar", "Cancelar"))
                return;
            if (oldBelt != null) Undo.DestroyObjectImmediate(oldBelt);
            if (oldTools != null) Undo.DestroyObjectImmediate(oldTools);
        }

        // Referencia de escala: el objeto que lleva la escala de la maqueta.
        var casa = GameObject.Find("Casa_Modelo");
        if (casa == null) casa = GameObject.Find("Maqueta");

        // Materiales
        var matBelt = MakeMat("Cinturon_Base", new Color(0.12f, 0.12f, 0.14f), false);
        var matBody = MakeMat("Herramienta_Cuerpo", new Color(0.85f, 0.85f, 0.88f), false);
        var matTape = MakeMat("Medidor_Cuerpo", new Color(0.95f, 0.75f, 0.1f), false);
        var matLine = MakeMat("Medidor_Linea", new Color(1f, 0.92f, 0.2f), true);
        var matMark = MakeMat("Medidor_Marca", new Color(0.95f, 0.15f, 0.15f), true);

        // Cinturon (hijo del XR Origin, sigue la cintura con BeltFollower)
        var belt = new GameObject(BeltName);
        Undo.RegisterCreatedObjectUndo(belt, "Crear cinturon");
        belt.transform.SetParent(origin.transform, false);
        belt.AddComponent<BeltFollower>();

        // Contenedor de herramientas en la raiz de la escena
        var tools = new GameObject(ToolsName);
        Undo.RegisterCreatedObjectUndo(tools, "Crear herramientas");

        Vector3 start = origin.transform.position + Vector3.up * 1f;

        // --- Ranura derecha: Medidor ---
        var slotTape = CreateSlot(belt.transform, "Slot_Medidor", new Vector3(0.28f, 0f, 0.03f), matBelt);
        var tape = CreateTapeMeasure(tools.transform, start, matTape, matMark, matLine, casa != null ? casa.transform : null);
        BindToSlot(slotTape, tape);

        // --- Ranura izquierda: Linterna ---
        var slotLight = CreateSlot(belt.transform, "Slot_Linterna", new Vector3(-0.28f, 0f, 0.03f), matBelt);
        var flash = CreateFlashlight(tools.transform, start + Vector3.left * 0.3f, matBody);
        BindToSlot(slotLight, flash);

        Selection.activeGameObject = belt;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Cinturon] Listo. Guarda la escena (Ctrl+S) y prueba con el XR Device Simulator.");
    }

    // ------------------------------------------------------------------ slots

    public static XRSocketInteractor CreateSlot(Transform belt, string name, Vector3 localPos, Material baseMat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(belt, false);
        go.transform.localPosition = localPos;

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.09f;

        var socket = go.AddComponent<XRSocketInteractor>();
        socket.showInteractableHoverMeshes = false;

        // Base visual de la ranura (disco plano)
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Base";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(go.transform, false);
        disc.transform.localPosition = new Vector3(0f, -0.03f, 0f);
        disc.transform.localScale = new Vector3(0.1f, 0.004f, 0.1f);
        disc.GetComponent<MeshRenderer>().sharedMaterial = baseMat;
        return socket;
    }

    public static void BindToSlot(XRSocketInteractor socket, XRGrabInteractable tool)
    {
        socket.startingSelectedInteractable = tool;
        var ret = tool.gameObject.AddComponent<ToolAutoReturn>();
        ret.homeSocket = socket;
        EditorUtility.SetDirty(socket);
        EditorUtility.SetDirty(ret);
    }

    // ------------------------------------------------------------------ tools

    public static XRGrabInteractable CreateToolRoot(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.useGravity = true;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = false;
        return grab;
    }

    static XRGrabInteractable CreateTapeMeasure(Transform parent, Vector3 pos, Material body, Material mark, Material line, Transform scaleRef)
    {
        var grab = CreateToolRoot(parent, "Medidor", pos);
        var root = grab.transform;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cuerpo";
        cube.transform.SetParent(root, false);
        cube.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        cube.transform.localScale = new Vector3(0.035f, 0.035f, 0.12f);
        cube.GetComponent<MeshRenderer>().sharedMaterial = body;

        var tip = new GameObject("Punta");
        tip.transform.SetParent(root, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.11f);

        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "PuntaVisual";
        Object.DestroyImmediate(dot.GetComponent<Collider>());
        dot.transform.SetParent(tip.transform, false);
        dot.transform.localScale = Vector3.one * 0.016f;
        dot.GetComponent<MeshRenderer>().sharedMaterial = mark;

        var tm = root.gameObject.AddComponent<TapeMeasure>();
        tm.tip = tip.transform;
        tm.scaleReference = scaleRef;
        tm.lineMaterial = line;
        tm.markerMaterial = mark;
        EditorUtility.SetDirty(tm);
        return grab;
    }

    static XRGrabInteractable CreateFlashlight(Transform parent, Vector3 pos, Material body)
    {
        var grab = CreateToolRoot(parent, "Linterna", pos);
        var root = grab.transform;

        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Cuerpo";
        cyl.transform.SetParent(root, false);
        cyl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cyl.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        cyl.transform.localScale = new Vector3(0.035f, 0.05f, 0.035f);
        cyl.GetComponent<MeshRenderer>().sharedMaterial = body;

        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(0f, 0f, 0.11f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = 45f;
        l.range = 10f;
        l.intensity = 8f;
        l.enabled = false;

        var t = root.gameObject.AddComponent<ToggleLightTool>();
        t.lightSource = l;
        EditorUtility.SetDirty(t);
        return grab;
    }

    // -------------------------------------------------------------- materials

    public static Material MakeMat(string name, Color color, bool unlit)
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
