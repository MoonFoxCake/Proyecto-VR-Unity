using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Convierte una puerta "horneada" (marco + hoja + volumen de giro en UN solo mesh)
/// en una puerta funcional: Puerta > Marco + Bisagra > Hoja.
/// Primero la familia Aluprof (puertas macizas).
/// </summary>
public static class PuertasSetup
{
    const string CarpetaMallas = "Assets/Puertas/Meshes";
    static readonly Regex FamiliaAluprof = new Regex("^Aluprof", RegexOptions.IgnoreCase);

    const int Otro = 0, Giro = 1, Marco = 2, HojaPrincipal = 3, AnexoHoja = 4, AnexoMarco = 5;

    internal class Pieza
    {
        public int sub;
        public List<int> tris = new List<int>(); // indices de vertice, de 3 en 3
        public Bounds b;
        public Vector3 m; // tamano en metros
        public int cls;
    }

    // ------------------------------------------------------------ menus

    [MenuItem("Tools/VR/Puertas/1. Convertir puertas seleccionadas")]
    static void ConvertirSeleccion()
    {
        var lista = Selection.gameObjects.ToList();
        if (lista.Count == 0) { EditorUtility.DisplayDialog("Puertas", "Selecciona primero una o mas puertas en la Hierarchy.", "OK"); return; }
        Procesar(lista);
    }

    [MenuItem("Tools/VR/Puertas/2. Convertir TODAS las puertas Aluprof de la escena")]
    static void ConvertirTodas()
    {
        var lista = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(mf => FamiliaAluprof.IsMatch(mf.gameObject.name) && mf.GetComponentInParent<PuertaInteractiva>() == null)
            .Select(mf => mf.gameObject).ToList();
        if (lista.Count == 0) { EditorUtility.DisplayDialog("Puertas", "No encontre puertas Aluprof sin convertir en la escena.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Puertas", "Voy a convertir " + lista.Count + " puertas. Se puede deshacer con Ctrl+Z. Seguir?", "Si", "Cancelar")) return;
        Procesar(lista);
    }

    [MenuItem("Tools/VR/Puertas/3. Voltear bisagra de la puerta seleccionada")]
    static void VoltearBisagra()
    {
        var go = Selection.activeGameObject;
        var pi = go != null ? go.GetComponentInParent<PuertaInteractiva>() : null;
        if (pi == null || pi.bisagra == null || pi.hoja == null)
        {
            EditorUtility.DisplayDialog("Puertas", "Selecciona una puerta ya convertida (o una parte de ella).", "OK");
            return;
        }
        Undo.RecordObjects(new Object[] { pi, pi.bisagra, pi.hoja }, "Voltear bisagra");
        pi.bisagra.localRotation = Quaternion.identity;
        Vector3 vieja = pi.bisagra.localPosition;
        Vector3 nueva = 2f * pi.centroHojaLocal - vieja;
        pi.bisagra.localPosition = nueva;
        pi.hoja.localPosition += vieja - nueva;
        pi.ladoLibreLocal = -pi.ladoLibreLocal;
        EditorUtility.SetDirty(pi);
        EditorSceneManager.MarkSceneDirty(pi.gameObject.scene);
    }

    // ------------------------------------------------------------ proceso

    internal static void Procesar(List<GameObject> lista)
    {
        int ok = 0;
        var avisos = new StringBuilder();
        var cacheInt = new Dictionary<Mesh, PuertasInteriores.Datos>();
        foreach (var go in lista)
        {
            string msg;
            if (ConvertirAuto(go, cacheInt, out msg)) ok++;
            if (!string.IsNullOrEmpty(msg)) avisos.AppendLine("- " + go.name + ": " + msg);
        }
        AssetDatabase.SaveAssets();
        string texto = "Convertidas: " + ok + " de " + lista.Count + ".";
        if (avisos.Length > 0) texto += "\n\nNotas:\n" + avisos;
        Debug.Log("[Puertas] " + texto);
        EditorUtility.DisplayDialog("Puertas", texto, "OK");
    }

    static bool ConvertirAuto(GameObject root, Dictionary<Mesh, PuertasInteriores.Datos> cache, out string msg)
    {
        if (PuertasInteriores.EsInterior(root.name)) return PuertasInteriores.Convertir(root, cache, out msg);
        if (Convertir(root, out msg)) return true;
        if (msg != null && msg.StartsWith("no encontre"))
        {
            string msg2;
            if (PuertasInteriores.Convertir(root, cache, out msg2)) { msg = msg2; return true; }
        }
        return false;
    }

    static bool Convertir(GameObject root, out string msg)
    {
        msg = null;
        if (root.GetComponent<PuertaInteractiva>() != null) { msg = "ya estaba convertida."; return false; }
        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            msg = "es parte de un prefab/modelo. Haz clic derecho en el modelo (GLB) de la Hierarchy > Prefab > Unpack Completely y repite.";
            return false;
        }
        var mf = root.GetComponent<MeshFilter>();
        var mr = root.GetComponent<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null) { msg = "no tiene MeshFilter/MeshRenderer con mesh."; return false; }
        var src = mf.sharedMesh;
        if (!src.isReadable)
        {
            msg = "el mesh no es legible. Selecciona el .glb en Project y activa la opcion de mesh legible (Read/Write) en el Inspector, Apply.";
            return false;
        }

        Vector3[] v = src.vertices;
        Vector3 ls = root.transform.lossyScale;
        Vector3 sc = new Vector3(Mathf.Max(Mathf.Abs(ls.x), 1e-5f), Mathf.Max(Mathf.Abs(ls.y), 1e-5f), Mathf.Max(Mathf.Abs(ls.z), 1e-5f));

        // ---- soldar vertices por posicion
        var weld = new Dictionary<Vector3Int, int>();
        int[] wid = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
            int id;
            if (!weld.TryGetValue(key, out id)) { id = weld.Count; weld[key] = id; }
            wid[i] = id;
        }
        int nWeld = weld.Count;

        // ---- piezas sueltas por submesh
        var piezas = new List<Pieza>();
        int nSub = src.subMeshCount;
        for (int s = 0; s < nSub; s++)
        {
            int[] t = src.GetTriangles(s);
            int[] parent = new int[nWeld];
            for (int i = 0; i < nWeld; i++) parent[i] = i;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Union(parent, wid[t[k]], wid[t[k + 1]]);
                Union(parent, wid[t[k + 1]], wid[t[k + 2]]);
            }
            var porRaiz = new Dictionary<int, Pieza>();
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                int r = Find(parent, wid[t[k]]);
                Pieza p;
                if (!porRaiz.TryGetValue(r, out p))
                {
                    p = new Pieza { sub = s, b = new Bounds(v[t[k]], Vector3.zero) };
                    porRaiz[r] = p;
                    piezas.Add(p);
                }
                for (int j = 0; j < 3; j++) { p.tris.Add(t[k + j]); p.b.Encapsulate(v[t[k + j]]); }
            }
        }
        foreach (var p in piezas) p.m = new Vector3(p.b.size.x * sc.x, p.b.size.y * sc.y, p.b.size.z * sc.z);

        // ---- clasificar
        foreach (var p in piezas)
            if (p.m.y >= 1.7f && p.m.x >= 0.4f && p.m.z >= 0.4f) p.cls = Giro;

        var finas = piezas.Where(p => p.cls == Otro && p.m.y >= 1.7f && Mathf.Min(p.m.x, p.m.z) <= 0.25f).ToList();
        if (finas.Count == 0) { msg = "no encontre marco ni hoja (ninguna pieza alta y delgada)."; return false; }

        Pieza marco = finas.OrderByDescending(p => Mathf.Max(p.m.x, p.m.z)).First();
        marco.cls = Marco;
        Pieza hoja = finas.Where(p => p != marco && Mathf.Max(p.m.x, p.m.z) >= 0.5f)
            .OrderByDescending(p => Mathf.Max(p.m.x, p.m.z) * p.m.y * Mathf.Max(Mathf.Min(p.m.x, p.m.z), 0.005f))
            .FirstOrDefault();
        if (hoja == null) { msg = "no encontre la hoja (solo hay una pieza alta y delgada)."; return false; }
        hoja.cls = HojaPrincipal;

        int wAxis = hoja.m.x >= hoja.m.z ? 0 : 2;
        int tAxis = wAxis == 0 ? 2 : 0;
        float tolW = 0.03f / sc[wAxis], tolT = 0.15f / sc[tAxis], tolY = 0.03f / sc.y;

        foreach (var p in piezas.Where(q => q.cls == Otro))
        {
            Vector3 c = p.b.center;
            bool enHoja = c[wAxis] >= hoja.b.min[wAxis] - tolW && c[wAxis] <= hoja.b.max[wAxis] + tolW
                       && c[tAxis] >= hoja.b.min[tAxis] - tolT && c[tAxis] <= hoja.b.max[tAxis] + tolT
                       && c.y >= hoja.b.min.y - tolY && c.y <= hoja.b.max.y + tolY;
            p.cls = enHoja ? AnexoHoja : AnexoMarco;
        }

        // ---- lado de la bisagra: grupo mas grande de piezas pequenas identicas en la hoja
        int lado = -1;
        var grupo = piezas.Where(p => p.cls == AnexoHoja)
            .GroupBy(p => p.sub + "|" + p.tris.Count + "|" + Mathf.RoundToInt(p.m.x * 1000) + "|" + Mathf.RoundToInt(p.m.y * 1000) + "|" + Mathf.RoundToInt(p.m.z * 1000))
            .Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (grupo != null)
        {
            float prom = grupo.Average(p => p.b.center[wAxis] - hoja.b.center[wAxis]);
            if (Mathf.Abs(prom) > 1e-5f) lado = prom > 0 ? 1 : -1;
            else msg = "no pude saber el lado de la bisagra; usa 'Voltear bisagra' si queda al reves.";
        }
        else msg = "no encontre las bisagras en el mesh; asumi un lado. Si queda al reves usa 'Voltear bisagra'.";

        Vector3 bisPos = hoja.b.center;
        bisPos[wAxis] = lado > 0 ? hoja.b.max[wAxis] : hoja.b.min[wAxis];
        Vector3 libre = Vector3.zero; libre[wAxis] = -lado;
        Vector3 normal = Vector3.zero; normal[tAxis] = 1f;

        // ---- datos de vertices
        Vector3[] n = src.normals;
        Vector4[] tg = src.tangents;
        Vector2[] uv = src.uv;
        Vector2[] uv2 = src.uv2;
        Color[] col = src.colors;

        var piezasMarco = piezas.Where(p => p.cls == Marco || p.cls == AnexoMarco).ToList();
        var piezasHoja = piezas.Where(p => p.cls == HojaPrincipal || p.cls == AnexoHoja).ToList();

        EnsureFolder();
        string baseName = Sanitize(root.name);
        List<int> subsMarco, subsHoja;
        Mesh meshMarco = Construir(v, n, tg, uv, uv2, col, piezasMarco, baseName + "_Marco", out subsMarco);
        Mesh meshHoja = Construir(v, n, tg, uv, uv2, col, piezasHoja, baseName + "_Hoja", out subsHoja);
        meshMarco = Guardar(meshMarco);
        meshHoja = Guardar(meshHoja);

        var matsOriginales = mr.sharedMaterials;
        Material[] MatsDe(List<int> subs) => subs.Select(s => s < matsOriginales.Length ? matsOriginales[s] : null).ToArray();

        // ---- armar jerarquia
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Convertir puerta");
        int grupoUndo = Undo.GetCurrentGroup();

        var sombra = mr.shadowCastingMode;
        bool recibe = mr.receiveShadows;
        string original = root.name;
        int layer = root.layer;

        Undo.DestroyObjectImmediate(mr);
        Undo.DestroyObjectImmediate(mf);

        var goMarco = NuevoHijo("Marco", root.transform, layer);
        var fMarco = Undo.AddComponent<MeshFilter>(goMarco); fMarco.sharedMesh = meshMarco;
        var rMarco = Undo.AddComponent<MeshRenderer>(goMarco); rMarco.sharedMaterials = MatsDe(subsMarco);
        rMarco.shadowCastingMode = sombra; rMarco.receiveShadows = recibe;
        var cMarco = Undo.AddComponent<MeshCollider>(goMarco); cMarco.sharedMesh = meshMarco;

        var goBis = NuevoHijo("Bisagra", root.transform, layer);
        goBis.transform.localPosition = bisPos;
        var rb = Undo.AddComponent<Rigidbody>(goBis); rb.isKinematic = true; rb.useGravity = false;

        var goHoja = NuevoHijo("Hoja", goBis.transform, layer);
        goHoja.transform.localPosition = -bisPos;
        var fHoja = Undo.AddComponent<MeshFilter>(goHoja); fHoja.sharedMesh = meshHoja;
        var rHoja = Undo.AddComponent<MeshRenderer>(goHoja); rHoja.sharedMaterials = MatsDe(subsHoja);
        rHoja.shadowCastingMode = sombra; rHoja.receiveShadows = recibe;
        var box = Undo.AddComponent<BoxCollider>(goHoja);
        box.center = hoja.b.center;
        Vector3 tam = hoja.b.size;
        tam[tAxis] = Mathf.Max(tam[tAxis], 0.06f / sc[tAxis]);
        box.size = tam;
        var inter = Undo.AddComponent<XRSimpleInteractable>(goHoja);

        var pi = Undo.AddComponent<PuertaInteractiva>(root);
        pi.bisagra = goBis.transform;
        pi.hoja = goHoja.transform;
        pi.interactable = inter;
        pi.ladoLibreLocal = libre;
        pi.normalLocal = normal;
        pi.centroHojaLocal = hoja.b.center;
        EditorUtility.SetDirty(pi);

        Undo.RecordObject(root, "Renombrar puerta");
        root.name = "Puerta_" + original;

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return true;
    }

    // ------------------------------------------------------------ utilidades

    internal static GameObject NuevoHijo(string nombre, Transform padre, int layer)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        go.layer = layer;
        return go;
    }

    internal static Mesh Construir(Vector3[] v, Vector3[] n, Vector4[] tg, Vector2[] uv, Vector2[] uv2, Color[] col,
                          List<Pieza> piezas, string nombre, out List<int> subsUsados)
    {
        subsUsados = piezas.Select(p => p.sub).Distinct().OrderBy(s => s).ToList();
        var remap = new Dictionary<int, int>();
        var lv = new List<Vector3>(); var ln = new List<Vector3>(); var lt = new List<Vector4>();
        var luv = new List<Vector2>(); var luv2 = new List<Vector2>(); var lc = new List<Color>();
        bool hn = n.Length == v.Length, ht = tg.Length == v.Length, hu = uv.Length == v.Length,
             hu2 = uv2.Length == v.Length, hc = col.Length == v.Length;

        var tris = new List<List<int>>();
        foreach (int s in subsUsados)
        {
            var lista = new List<int>();
            foreach (var p in piezas.Where(q => q.sub == s))
                foreach (int vi in p.tris)
                {
                    int ni;
                    if (!remap.TryGetValue(vi, out ni))
                    {
                        ni = lv.Count; remap[vi] = ni;
                        lv.Add(v[vi]);
                        if (hn) ln.Add(n[vi]);
                        if (ht) lt.Add(tg[vi]);
                        if (hu) luv.Add(uv[vi]);
                        if (hu2) luv2.Add(uv2[vi]);
                        if (hc) lc.Add(col[vi]);
                    }
                    lista.Add(ni);
                }
            tris.Add(lista);
        }

        var m = new Mesh { name = nombre };
        if (lv.Count > 65535) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(lv);
        if (hn) m.SetNormals(ln);
        if (ht) m.SetTangents(lt);
        if (hu) m.SetUVs(0, luv);
        if (hu2) m.SetUVs(1, luv2);
        if (hc) m.SetColors(lc);
        m.subMeshCount = tris.Count;
        for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i);
        if (!hn) m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    internal static Mesh Guardar(Mesh m)
    {
        string ruta = AssetDatabase.GenerateUniqueAssetPath(CarpetaMallas + "/" + m.name + ".asset");
        AssetDatabase.CreateAsset(m, ruta);
        return AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
    }

    internal static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Puertas")) AssetDatabase.CreateFolder("Assets", "Puertas");
        if (!AssetDatabase.IsValidFolder(CarpetaMallas)) AssetDatabase.CreateFolder("Assets/Puertas", "Meshes");
    }

    internal static string Sanitize(string s) => Regex.Replace(s, "[^A-Za-z0-9_\\-]", "_");

    internal static int Find(int[] p, int x)
    {
        while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
        return x;
    }

    internal static void Union(int[] p, int a, int b)
    {
        int ra = Find(p, a), rb = Find(p, b);
        if (ra != rb) p[ra] = rb;
    }
}
