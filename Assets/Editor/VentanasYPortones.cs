using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ventanas abatibles dobles y portones dobles funcionales, con el mismo sistema que las puertas
/// (PuertaInteractiva + AgarrePuerta: se agarran y se empujan o jalan, con inercia y pestillo).
///
/// En el modelo vienen "horneadas" (marco + dos hojas en UN mesh). La herramienta las separa:
///   - Cada hoja va desde su borde exterior hasta el centro (la pieza mas grande de cada lado
///     marca ese borde); lo que cruza el centro o queda afuera (marco, postes, bases) es el marco.
///   - Bisagra en el borde exterior de cada hoja; cada hoja tiene su propia PuertaInteractiva.
///   - Lado de apertura segun donde esta la casa: las ventanas abren hacia AFUERA (no golpean
///     a nadie adentro) y los portones hacia ADENTRO de la propiedad.
///     Cualquier hoja se puede invertir con "5. Invertir lado de apertura".
/// Tambien pone vidrio transparente a todas las ventanas (el modelo trae el vidrio opaco).
/// </summary>
public static class VentanasYPortones
{
    static readonly Regex Ventana = new Regex("white_aluminum_sliding_window", RegexOptions.IgnoreCase);
    static readonly Regex Porton = new Regex("NBS_CLDFencingSystems|Lockmaster", RegexOptions.IgnoreCase);
    const string RutaVidrio = "Assets/Puertas/VidrioTransparente.mat";

    // ------------------------------------------------------------ menus

    [MenuItem("Tools/VR/Puertas/6. Convertir ventanas dobles y portones (todos)")]
    static void ConvertirTodos()
    {
        var lista = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include)
            .Where(mf => (Ventana.IsMatch(mf.gameObject.name) || Porton.IsMatch(mf.gameObject.name))
                         && mf.GetComponent<MeshRenderer>() != null
                         && mf.GetComponentInParent<PuertaInteractiva>() == null
                         && mf.GetComponentInParent<MaquetaExhibicion>() == null)
            .Select(mf => mf.gameObject).ToList();
        if (lista.Count == 0) { EditorUtility.DisplayDialog("Ventanas y portones", "No encontre ventanas dobles ni portones sin convertir.", "OK"); return; }
        int nv = lista.Count(g => Ventana.IsMatch(g.name)), np = lista.Count - nv;
        if (!EditorUtility.DisplayDialog("Ventanas y portones",
                "Voy a convertir " + nv + " ventanas y " + np + " portones. Se puede deshacer con Ctrl+Z. Seguir?", "Si", "Cancelar")) return;

        var losas = Losas();
        int ok = 0;
        var notas = new StringBuilder();
        try
        {
            for (int i = 0; i < lista.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Ventanas y portones", lista[i].name, (float)i / lista.Count);
                string nombre = lista[i].name;
                if (Convertir(lista[i], losas, out string msg)) ok++;
                if (!string.IsNullOrEmpty(msg)) notas.AppendLine("- " + nombre + ": " + msg);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        int vidrios = AplicarVidrio();

        string texto = "Convertidas: " + ok + " de " + lista.Count + ".\nVidrios transparentes: " + vidrios + ".\n\nGuarda la escena (Ctrl+S).";
        if (notas.Length > 0) texto += "\n\nNotas:\n" + notas;
        Debug.Log("[Ventanas] " + texto.Replace("\n", " "));
        EditorUtility.DisplayDialog("Ventanas y portones", texto, "OK");
    }

    [MenuItem("Tools/VR/Puertas/7. Vidrio transparente en todas las ventanas")]
    static void MenuVidrio()
    {
        int n = AplicarVidrio();
        Debug.Log("[Ventanas] Vidrios transparentes: " + n);
        EditorUtility.DisplayDialog("Vidrio", "Vidrios cambiados a transparente: " + n + ".\n\nGuarda la escena (Ctrl+S).", "OK");
    }

    // ------------------------------------------------------------ conversion

    static bool Convertir(GameObject root, List<Bounds> losas, out string msg)
    {
        msg = null;
        if (PrefabUtility.IsPartOfPrefabInstance(root)) { msg = "es parte de un prefab; haz Unpack Completely del modelo y repite."; return false; }
        var mf = root.GetComponent<MeshFilter>();
        var mr = root.GetComponent<MeshRenderer>();
        var src = mf.sharedMesh;
        if (src == null) { msg = "no tiene mesh."; return false; }
        if (!src.isReadable) { msg = "el mesh no es legible (activa Read/Write en el importador del modelo)."; return false; }
        bool esVentana = Ventana.IsMatch(root.name);

        Vector3 ls = root.transform.lossyScale;
        Vector3 sc = new Vector3(Mathf.Max(Mathf.Abs(ls.x), 1e-5f), Mathf.Max(Mathf.Abs(ls.y), 1e-5f), Mathf.Max(Mathf.Abs(ls.z), 1e-5f));
        Vector3[] v = src.vertices;
        var piezas = Piezas(src, v);

        // ---- ejes y centro
        Bounds total = piezas[0].b;
        foreach (var p in piezas) total.Encapsulate(p.b);
        int w = total.size.x * sc.x >= total.size.z * sc.z ? 0 : 2;
        int t = w == 0 ? 2 : 0;
        float c = total.center[w];
        float eps = 0.015f / sc[w], epsY = 0.015f / sc.y;

        // ---- hojas: de su borde exterior al centro
        var hojas = new Dictionary<int, List<PuertasSetup.Pieza>>();
        foreach (int s in new[] { -1, 1 })
        {
            var cand = piezas.Where(p => s < 0 ? p.b.max[w] <= c + eps : p.b.min[w] >= c - eps).ToList();
            if (cand.Count == 0) { msg = "no encontre la hoja de un lado."; return false; }
            var pr = cand.OrderByDescending(p => p.b.size[w] * p.b.size.y).First();
            if (pr.b.size.y < 0.4f * total.size.y) { msg = "la hoja de un lado es muy baja; no parece doble."; return false; }
            float exterior = s < 0 ? pr.b.min[w] : pr.b.max[w];
            float wMin = s < 0 ? exterior - eps : c - eps, wMax = s < 0 ? c + eps : exterior + eps;
            float yMin = pr.b.min.y - epsY, yMax = pr.b.max.y + epsY;
            hojas[s] = cand.Where(p => p.b.min[w] >= wMin && p.b.max[w] <= wMax && p.b.min.y >= yMin && p.b.max.y <= yMax).ToList();
        }
        var enHojas = new HashSet<PuertasSetup.Pieza>(hojas.Values.SelectMany(h => h));
        var marco = piezas.Where(p => !enHojas.Contains(p)).ToList();

        // ---- lado de apertura (segun donde esta la casa)
        Vector3 normal = Vector3.zero; normal[t] = 1f;
        Vector3 centroMundo = root.transform.TransformPoint(total.center);
        Vector3 nMundo = root.transform.TransformDirection(normal).normalized;
        Vector3 haciaCasa = HaciaCasa(centroMundo, losas);
        float lado = Vector3.Dot(haciaCasa, nMundo);
        float signo;
        if (Mathf.Abs(lado) < 0.1f) { signo = 1f; msg = "no supe donde esta la casa; asumi un lado (usa '5. Invertir lado' si queda al reves)."; }
        else signo = esVentana ? -Mathf.Sign(lado) : Mathf.Sign(lado); // ventana: afuera; porton: adentro

        // ---- mallas
        Vector3[] n = src.normals; Vector4[] tg = src.tangents; Vector2[] uv = src.uv; Vector2[] uv2 = src.uv2; Color[] col = src.colors;
        PuertasSetup.EnsureFolder();
        string baseName = PuertasSetup.Sanitize(root.name);
        Mesh meshMarco = PuertasSetup.Guardar(PuertasSetup.Construir(v, n, tg, uv, uv2, col, marco, baseName + "_Marco", out var subsMarco));
        var mats = mr.sharedMaterials;
        Material[] MatsDe(List<int> subs) => subs.Select(sub => sub < mats.Length ? mats[sub] : null).ToArray();

        // ---- jerarquia
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Convertir " + (esVentana ? "ventana" : "porton"));
        int grupoUndo = Undo.GetCurrentGroup();
        var sombra = mr.shadowCastingMode; bool recibe = mr.receiveShadows; int layer = root.layer;
        string original = root.name;
        Undo.DestroyObjectImmediate(mr);
        Undo.DestroyObjectImmediate(mf);

        var goMarco = PuertasSetup.NuevoHijo("Marco", root.transform, layer);
        Undo.AddComponent<MeshFilter>(goMarco).sharedMesh = meshMarco;
        var rMarco = Undo.AddComponent<MeshRenderer>(goMarco);
        rMarco.sharedMaterials = MatsDe(subsMarco); rMarco.shadowCastingMode = sombra; rMarco.receiveShadows = recibe;
        Undo.AddComponent<MeshCollider>(goMarco).sharedMesh = meshMarco;

        foreach (var kv in hojas)
        {
            int s = kv.Key;
            Bounds hb = kv.Value[0].b;
            foreach (var p in kv.Value) hb.Encapsulate(p.b);
            string etiqueta = s < 0 ? "A" : "B";
            Mesh meshHoja = PuertasSetup.Guardar(PuertasSetup.Construir(v, n, tg, uv, uv2, col, kv.Value, baseName + "_Hoja" + etiqueta, out var subsHoja));

            Vector3 bisPos = hb.center;
            bisPos[w] = s < 0 ? hb.min[w] : hb.max[w];
            Vector3 libre = Vector3.zero; libre[w] = -s;

            var goAla = PuertasSetup.NuevoHijo("Hoja " + etiqueta, root.transform, layer);
            var goBis = PuertasSetup.NuevoHijo("Bisagra", goAla.transform, layer);
            goBis.transform.localPosition = bisPos;
            var rb = Undo.AddComponent<Rigidbody>(goBis); rb.isKinematic = true; rb.useGravity = false;

            var goHoja = PuertasSetup.NuevoHijo("Hoja", goBis.transform, layer);
            goHoja.transform.localPosition = -bisPos;
            Undo.AddComponent<MeshFilter>(goHoja).sharedMesh = meshHoja;
            var rHoja = Undo.AddComponent<MeshRenderer>(goHoja);
            rHoja.sharedMaterials = MatsDe(subsHoja); rHoja.shadowCastingMode = sombra; rHoja.receiveShadows = recibe;
            var box = Undo.AddComponent<BoxCollider>(goHoja);
            box.center = hb.center;
            Vector3 tam = hb.size; tam[t] = Mathf.Max(tam[t], 0.04f / sc[t]);
            box.size = tam;
            var inter = Undo.AddComponent<AgarrePuerta>(goHoja);

            var pi = Undo.AddComponent<PuertaInteractiva>(goAla);
            pi.bisagra = goBis.transform;
            pi.hoja = goHoja.transform;
            pi.marco = goMarco.transform;
            pi.interactable = inter;
            pi.ladoLibreLocal = libre;
            pi.normalLocal = normal;
            pi.centroHojaLocal = hb.center;
            pi.aperturaLocal = normal * signo;
            pi.ladoManual = true;
            pi.anguloApertura = 90f;
            EditorUtility.SetDirty(pi);
        }

        Undo.RecordObject(root, "Renombrar");
        root.name = (esVentana ? "Ventana_" : "Porton_") + original;
        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return true;
    }

    // Piezas sueltas del mesh (vertices soldados por posicion, por submesh). Coordenadas del mesh.
    static List<PuertasSetup.Pieza> Piezas(Mesh src, Vector3[] v)
    {
        var weld = new Dictionary<Vector3Int, int>();
        int[] wid = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
            if (!weld.TryGetValue(key, out int id)) { id = weld.Count; weld[key] = id; }
            wid[i] = id;
        }
        var piezas = new List<PuertasSetup.Pieza>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tr = src.GetTriangles(s);
            int[] parent = new int[weld.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int k = 0; k + 2 < tr.Length; k += 3)
            {
                PuertasSetup.Union(parent, wid[tr[k]], wid[tr[k + 1]]);
                PuertasSetup.Union(parent, wid[tr[k + 1]], wid[tr[k + 2]]);
            }
            var porRaiz = new Dictionary<int, PuertasSetup.Pieza>();
            for (int k = 0; k + 2 < tr.Length; k += 3)
            {
                int r = PuertasSetup.Find(parent, wid[tr[k]]);
                if (!porRaiz.TryGetValue(r, out var p))
                {
                    p = new PuertasSetup.Pieza { sub = s, b = new Bounds(v[tr[k]], Vector3.zero) };
                    porRaiz[r] = p;
                    piezas.Add(p);
                }
                for (int j = 0; j < 3; j++) { p.tris.Add(tr[k + j]); p.b.Encapsulate(v[tr[k + j]]); }
            }
        }
        return piezas;
    }

    // Losas de piso del tamano de una casa (para saber hacia donde queda "adentro").
    static List<Bounds> Losas()
    {
        var lista = new List<Bounds>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (r.GetComponentInParent<MaquetaExhibicion>() != null) continue;
            Bounds b = r.bounds;
            if (b.size.y < 0.5f && b.size.x > 3f && b.size.x < 15f && b.size.z > 3f && b.size.z < 15f) lista.Add(b);
        }
        return lista;
    }

    // Direccion horizontal desde el punto hacia el centro de la casa mas cercana que lo contiene (con margen).
    static Vector3 HaciaCasa(Vector3 p, List<Bounds> losas)
    {
        const float margen = 0.8f;
        Bounds? mejor = null;
        float dMejor = float.MaxValue;
        foreach (var b in losas)
        {
            if (p.x < b.min.x - margen || p.x > b.max.x + margen || p.z < b.min.z - margen || p.z > b.max.z + margen) continue;
            float d = new Vector2(b.center.x - p.x, b.center.z - p.z).sqrMagnitude;
            if (d < dMejor) { dMejor = d; mejor = b; }
        }
        if (mejor == null) return Vector3.zero;
        Vector3 dir = mejor.Value.center - p; dir.y = 0f;
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
    }

    // ------------------------------------------------------------ vidrio

    // Cambia el material "Vidrio" (opaco en el modelo) por uno transparente, en toda la escena
    // (incluye las maquetas), y quita los paneles opacos que el modelo trae detras de cada vidrio.
    // Devuelve cuantos renderers cambio.
    static int AplicarVidrio() => AplicarVidrio(out _);

    static int AplicarVidrio(out int paneles)
    {
        var vidrio = MaterialVidrio();
        int n = 0;
        paneles = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            var mats = r.sharedMaterials;
            bool cambio = false, tieneVidrio = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i] != vidrio && mats[i].name == "Vidrio") { mats[i] = vidrio; cambio = true; }
                if (mats[i] == vidrio) tieneVidrio = true;
            }
            if (cambio)
            {
                Undo.RecordObject(r, "Vidrio transparente");
                r.sharedMaterials = mats;
                n++;
            }
            if (tieneVidrio && r.GetComponentInParent<MaquetaExhibicion>() == null)
                paneles += QuitarPanelesDetrasDelVidrio(r, vidrio);
        }
        AssetDatabase.SaveAssets();
        if (n > 0 || paneles > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        if (paneles > 0) Debug.Log("[Ventanas] Paneles opacos quitados detras de los vidrios: " + paneles + " triangulos.");
        return n;
    }

    // El modelo trae, detras de cada vidrio, una cara opaca (material del marco) del mismo tamano:
    // aunque el vidrio sea transparente, se ve ese panel. Se quitan los triangulos paralelos al vidrio,
    // a menos de 5 cm de el y completamente dentro de su contorno (asi el marco no se toca).
    // Devuelve cuantos triangulos quito.
    static int QuitarPanelesDetrasDelVidrio(MeshRenderer r, Material vidrio)
    {
        var mf = r.GetComponent<MeshFilter>();
        var src = mf != null ? mf.sharedMesh : null;
        if (src == null || !src.isReadable) return 0;
        var mats = r.sharedMaterials;
        var subsVidrio = new HashSet<int>();
        for (int i = 0; i < mats.Length && i < src.subMeshCount; i++) if (mats[i] == vidrio) subsVidrio.Add(i);
        if (subsVidrio.Count == 0) return 0;

        float escala = Mathf.Max(1e-5f, Mathf.Abs(r.transform.lossyScale.x));
        float tol = 0.002f / escala, maxDist = 0.05f / escala;
        Vector3[] v = src.vertices;
        var panos = Piezas(src, v).Where(p => subsVidrio.Contains(p.sub)).Select(p => p.b).ToList();

        bool Quitar(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a);
            if (nrm.sqrMagnitude < 1e-20f) return false;
            nrm.Normalize();
            foreach (var pano in panos)
            {
                Vector3 s = pano.size;
                int fino = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
                if (Mathf.Abs(nrm[fino]) < 0.95f) continue;
                float plano = pano.center[fino];
                bool dentro = true;
                foreach (var q in new[] { a, b, c })
                {
                    if (Mathf.Abs(q[fino] - plano) > maxDist) { dentro = false; break; }
                    for (int k = 0; k < 3; k++)
                        if (k != fino && (q[k] < pano.min[k] - tol || q[k] > pano.max[k] + tol)) { dentro = false; break; }
                    if (!dentro) break;
                }
                if (dentro) return true;
            }
            return false;
        }

        var nuevos = new List<int>[src.subMeshCount];
        int quitados = 0;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tr = src.GetTriangles(s);
            var lista = new List<int>(tr.Length);
            for (int k = 0; k + 2 < tr.Length; k += 3)
            {
                if (!subsVidrio.Contains(s) && Quitar(v[tr[k]], v[tr[k + 1]], v[tr[k + 2]])) { quitados++; continue; }
                lista.Add(tr[k]); lista.Add(tr[k + 1]); lista.Add(tr[k + 2]);
            }
            nuevos[s] = lista;
        }
        if (quitados == 0) return 0;

        // Mallas propias (generadas por las herramientas): se editan en su lugar.
        // Mallas del modelo (.glb, de solo lectura): se guarda una copia y se asigna.
        string ruta = AssetDatabase.GetAssetPath(src);
        bool propia = ruta.StartsWith("Assets/Puertas/") && ruta.EndsWith(".asset");
        Mesh destino = propia ? src : Object.Instantiate(src);
        if (propia) Undo.RecordObject(destino, "Quitar paneles");
        for (int s = 0; s < nuevos.Length; s++) destino.SetTriangles(nuevos[s], s);
        if (propia) EditorUtility.SetDirty(destino);
        else
        {
            destino.name = src.name + "_SinPanel";
            destino = PuertasSetup.Guardar(destino);
            var col = r.GetComponent<MeshCollider>();
            Undo.RecordObject(mf, "Quitar paneles");
            mf.sharedMesh = destino;
            if (col != null && col.sharedMesh == src) { Undo.RecordObject(col, "Quitar paneles"); col.sharedMesh = destino; }
        }
        return quitados;
    }

    static Material MaterialVidrio()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaVidrio);
        if (m != null) return m;
        PuertasSetup.EnsureFolder();
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "VidrioTransparente" };
        m.SetColor("_BaseColor", new Color(0.78f, 0.88f, 0.92f, 0.18f));
        m.SetFloat("_Smoothness", 0.95f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f);                 // transparente
        m.SetFloat("_Blend", 0f);                   // alpha
        m.SetFloat("_BlendModePreserveSpecular", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);                    // se ve de los dos lados (el vidrio es una sola cara)
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, RutaVidrio);
        return m;
    }
}
