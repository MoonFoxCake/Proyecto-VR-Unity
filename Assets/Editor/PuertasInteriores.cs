using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puertas interiores (oak_lever_handle...). En el modelo la hoja es una lamina fina
/// dibujada ABIERTA a 45 grados. La herramienta la separa, la gira de vuelta a la pose
/// cerrada (sin tocar los vertices: solo gira el objeto Hoja) y le pone la bisagra.
/// </summary>
public static class PuertasInteriores
{
    static readonly Regex Familia = new Regex("oak_lever_handle|interior_door", RegexOptions.IgnoreCase);

    public class Datos
    {
        public Mesh meshMarco, meshHoja;
        public List<int> subsMarco, subsHoja;
        public Vector3 pivote;   // bisagra, coordenadas del mesh (pose abierta)
        public Vector3 centro;   // centro de la lamina (pose abierta)
        public float theta;      // giro Y que cierra la hoja
        public float phi;        // direccion de la lamina en el mesh (grados)
        public float largo, alto, grosor; // unidades del mesh
        public Vector3 libre, normal;     // pose cerrada
        public Vector3 centroCerrado;
        public string aviso;
    }

    public static bool EsInterior(string nombre) { return Familia.IsMatch(nombre); }

    [MenuItem("Tools/VR/Puertas/1b. Convertir TODAS las puertas interiores (oak_lever_handle)")]
    static void ConvertirTodasInteriores()
    {
        var lista = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(mf => Familia.IsMatch(mf.gameObject.name) && mf.GetComponentInParent<PuertaInteractiva>() == null)
            .Select(mf => mf.gameObject).ToList();
        if (lista.Count == 0) { EditorUtility.DisplayDialog("Puertas", "No encontre puertas interiores sin convertir en la escena.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Puertas", "Voy a convertir " + lista.Count + " puertas interiores. Se puede deshacer con Ctrl+Z. Seguir?", "Si", "Cancelar")) return;
        PuertasSetup.Procesar(lista);
    }

    // ------------------------------------------------------------ conversion

    public static bool Convertir(GameObject root, Dictionary<Mesh, Datos> cache, out string msg)
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
        if (!src.isReadable) { msg = "el mesh no es legible (activa Read/Write en el importador del .glb)."; return false; }

        Vector3 ls = root.transform.lossyScale;
        Vector3 sc = new Vector3(Mathf.Max(Mathf.Abs(ls.x), 1e-5f), Mathf.Max(Mathf.Abs(ls.y), 1e-5f), Mathf.Max(Mathf.Abs(ls.z), 1e-5f));

        Datos d;
        if (!cache.TryGetValue(src, out d))
        {
            d = Analizar(src, sc, out msg);
            if (d == null) return false;
            cache[src] = d;
        }
        msg = d.aviso;

        var matsOriginales = mr.sharedMaterials;
        Material[] MatsDe(List<int> subs) { return subs.Select(s => s < matsOriginales.Length ? matsOriginales[s] : null).ToArray(); }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Convertir puerta interior");
        int grupoUndo = Undo.GetCurrentGroup();

        var sombra = mr.shadowCastingMode;
        bool recibe = mr.receiveShadows;
        string original = root.name;
        int layer = root.layer;

        Undo.DestroyObjectImmediate(mr);
        Undo.DestroyObjectImmediate(mf);

        var goMarco = PuertasSetup.NuevoHijo("Marco", root.transform, layer);
        var fMarco = Undo.AddComponent<MeshFilter>(goMarco); fMarco.sharedMesh = d.meshMarco;
        var rMarco = Undo.AddComponent<MeshRenderer>(goMarco); rMarco.sharedMaterials = MatsDe(d.subsMarco);
        rMarco.shadowCastingMode = sombra; rMarco.receiveShadows = recibe;
        var cMarco = Undo.AddComponent<MeshCollider>(goMarco); cMarco.sharedMesh = d.meshMarco;

        var goBis = PuertasSetup.NuevoHijo("Bisagra", root.transform, layer);
        goBis.transform.localPosition = d.pivote;
        var rb = Undo.AddComponent<Rigidbody>(goBis); rb.isKinematic = true; rb.useGravity = false;

        Quaternion cierre = Quaternion.Euler(0f, d.theta, 0f);
        var goHoja = PuertasSetup.NuevoHijo("Hoja", goBis.transform, layer);
        goHoja.transform.localRotation = cierre;
        goHoja.transform.localPosition = -(cierre * d.pivote);
        var fHoja = Undo.AddComponent<MeshFilter>(goHoja); fHoja.sharedMesh = d.meshHoja;
        var rHoja = Undo.AddComponent<MeshRenderer>(goHoja); rHoja.sharedMaterials = MatsDe(d.subsHoja);
        rHoja.shadowCastingMode = sombra; rHoja.receiveShadows = recibe;

        var goCol = PuertasSetup.NuevoHijo("Colision", goHoja.transform, layer);
        goCol.transform.localPosition = d.centro;
        goCol.transform.localRotation = Quaternion.Euler(0f, -d.phi, 0f);
        var box = Undo.AddComponent<BoxCollider>(goCol);
        box.size = new Vector3(d.largo, d.alto, Mathf.Max(d.grosor, 0.06f / sc.x));

        var inter = Undo.AddComponent<XRSimpleInteractable>(goHoja);

        var pi = Undo.AddComponent<PuertaInteractiva>(root);
        pi.bisagra = goBis.transform;
        pi.hoja = goHoja.transform;
        pi.interactable = inter;
        pi.ladoLibreLocal = d.libre;
        pi.normalLocal = d.normal;
        pi.centroHojaLocal = d.centroCerrado;
        EditorUtility.SetDirty(pi);

        Undo.RecordObject(root, "Renombrar puerta");
        root.name = "Puerta_" + original;

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return true;
    }

    // ------------------------------------------------------------ analisis

    static Vector2Int PlanKey(Vector3 p)
    {
        return new Vector2Int(Mathf.RoundToInt(p.x * 100f), Mathf.RoundToInt(p.z * 100f));
    }

    static Datos Analizar(Mesh src, Vector3 sc, out string msg)
    {
        msg = null;
        Vector3[] v = src.vertices;
        int nSub = src.subMeshCount;
        var tris = new int[nSub][];
        var esSlab = new bool[nSub][];
        var esCap = new bool[nSub][];
        for (int s = 0; s < nSub; s++)
        {
            tris[s] = src.GetTriangles(s);
            esSlab[s] = new bool[tris[s].Length / 3];
            esCap[s] = new bool[tris[s].Length / 3];
        }

        // 1) caras verticales, altas y NO alineadas con los ejes = los lados de la hoja abierta
        var claves = new Dictionary<Vector2Int, List<Vector2>>();
        float ymin = float.MaxValue, ymax = float.MinValue;
        int nSlab = 0;
        for (int s = 0; s < nSub; s++)
            for (int k = 0; k < esSlab[s].Length; k++)
            {
                int a = tris[s][3 * k], b = tris[s][3 * k + 1], c = tris[s][3 * k + 2];
                Vector3 nrm = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (nrm.sqrMagnitude < 1e-12f) continue;
                nrm.Normalize();
                if (Mathf.Abs(nrm.y) > 0.05f) continue;
                float y0 = Mathf.Min(v[a].y, Mathf.Min(v[b].y, v[c].y));
                float y1 = Mathf.Max(v[a].y, Mathf.Max(v[b].y, v[c].y));
                if ((y1 - y0) * sc.y < 1.7f) continue;
                float ang = Mathf.Atan2(nrm.z, nrm.x) * Mathf.Rad2Deg;
                float mis = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 90f) * 90f));
                if (mis <= 5f) continue;
                esSlab[s][k] = true; nSlab++;
                ymin = Mathf.Min(ymin, y0); ymax = Mathf.Max(ymax, y1);
                foreach (int idx in new[] { a, b, c })
                {
                    var key = PlanKey(v[idx]);
                    List<Vector2> l;
                    if (!claves.TryGetValue(key, out l)) { l = new List<Vector2>(); claves[key] = l; }
                    l.Add(new Vector2(v[idx].x, v[idx].z));
                }
            }
        // El marco puede tener biseles a 45 grados: me quedo solo con el grupo conectado mas grande (la hoja).
        if (nSlab > 0)
        {
            var kp = new Dictionary<Vector2Int, Vector2Int>();
            Vector2Int Raiz(Vector2Int x) { while (kp[x] != x) { kp[x] = kp[kp[x]]; x = kp[x]; } return x; }
            var cand = new List<(int s, int k, Vector2Int[] ks)>();
            for (int s = 0; s < nSub; s++)
                for (int k = 0; k < esSlab[s].Length; k++)
                    if (esSlab[s][k])
                    {
                        var ks = new[] { tris[s][3 * k], tris[s][3 * k + 1], tris[s][3 * k + 2] }.Select(i => PlanKey(v[i])).ToArray();
                        foreach (var q in ks) if (!kp.ContainsKey(q)) kp[q] = q;
                        for (int j = 1; j < 3; j++) { var ra = Raiz(ks[0]); var rb2 = Raiz(ks[j]); if (ra != rb2) kp[ra] = rb2; }
                        cand.Add((s, k, ks));
                    }
            var grupos = cand.GroupBy(c => Raiz(c.ks[0])).ToList();
            var mejor = grupos.OrderByDescending(g =>
            {
                var pp = g.SelectMany(c => c.ks).Distinct().Select(q => new Vector2(q.x, q.y)).ToList();
                float m = 0f;
                foreach (var a1 in pp) foreach (var b1 in pp) m = Mathf.Max(m, Vector2.Distance(a1, b1));
                return m;
            }).First();
            var elegidos = new HashSet<(int, int)>(mejor.Select(c => (c.s, c.k)));
            claves.Clear(); ymin = float.MaxValue; ymax = float.MinValue; nSlab = 0;
            foreach (var c in cand)
            {
                bool ok2 = elegidos.Contains((c.s, c.k));
                esSlab[c.s][c.k] = ok2;
                if (!ok2) continue;
                nSlab++;
                for (int j = 0; j < 3; j++)
                {
                    int idx = tris[c.s][3 * c.k + j];
                    ymin = Mathf.Min(ymin, v[idx].y); ymax = Mathf.Max(ymax, v[idx].y);
                    List<Vector2> l;
                    if (!claves.TryGetValue(c.ks[j], out l)) { l = new List<Vector2>(); claves[c.ks[j]] = l; }
                    l.Add(new Vector2(v[idx].x, v[idx].z));
                }
            }
        }
        if (nSlab == 0) { msg = "no encontre una hoja girada (la puerta tal vez ya viene cerrada; usa la herramienta 1 de exteriores)."; return null; }
        if (claves.Count != 4) { msg = "la hoja no tiene 4 esquinas en planta (tiene " + claves.Count + "); forma inesperada."; return null; }

        // 2) tapas (horizontales) cuyos vertices son las esquinas de la hoja
        for (int s = 0; s < nSub; s++)
            for (int k = 0; k < esSlab[s].Length; k++)
            {
                if (esSlab[s][k]) continue;
                int a = tris[s][3 * k], b = tris[s][3 * k + 1], c = tris[s][3 * k + 2];
                Vector3 nrm = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (nrm.sqrMagnitude < 1e-12f) continue;
                nrm.Normalize();
                if (Mathf.Abs(nrm.y) < 0.95f) continue;
                if (claves.ContainsKey(PlanKey(v[a])) && claves.ContainsKey(PlanKey(v[b])) && claves.ContainsKey(PlanKey(v[c])))
                    esCap[s][k] = true;
            }

        // 3) geometria de la lamina
        Vector2[] pts = claves.Values.Select(l => new Vector2(l.Average(p => p.x), l.Average(p => p.y))).ToArray();
        var pares = new List<(int i, int j, float d)>();
        for (int i = 0; i < 4; i++) for (int j = i + 1; j < 4; j++) pares.Add((i, j, Vector2.Distance(pts[i], pts[j])));
        pares = pares.OrderBy(p => p.d).ToList();
        var corto = pares[0];
        var resto = Enumerable.Range(0, 4).Where(i => i != corto.i && i != corto.j).ToArray();
        float d2 = Vector2.Distance(pts[resto[0]], pts[resto[1]]);
        if (Mathf.Abs(d2 - corto.d) > 0.1f * Mathf.Max(corto.d, 1e-4f)) { msg = "la hoja no es un rectangulo limpio."; return null; }
        Vector2 extA = (pts[corto.i] + pts[corto.j]) * 0.5f;
        Vector2 extB = (pts[resto[0]] + pts[resto[1]]) * 0.5f;
        float largo = Vector2.Distance(extA, extB);
        float grosor = corto.d;
        Vector2 dir = (extB - extA) / largo;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 C2 = (extA + extB) * 0.5f;
        float cy = (ymin + ymax) * 0.5f;
        float alto = ymax - ymin;

        // 4) piezas sueltas del resto del mesh
        var weld = new Dictionary<Vector3Int, int>();
        int[] wid = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
            int id;
            if (!weld.TryGetValue(key, out id)) { id = weld.Count; weld[key] = id; }
            wid[i] = id;
        }
        var piezas = new List<PuertasSetup.Pieza>();
        var piezasHoja = new List<PuertasSetup.Pieza>();
        for (int s = 0; s < nSub; s++)
        {
            int[] parent = new int[weld.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            var propia = new PuertasSetup.Pieza { sub = s };
            for (int k = 0; k < esSlab[s].Length; k++)
            {
                int a = tris[s][3 * k], b = tris[s][3 * k + 1], c = tris[s][3 * k + 2];
                if (esSlab[s][k] || esCap[s][k]) { propia.tris.Add(a); propia.tris.Add(b); propia.tris.Add(c); continue; }
                PuertasSetup.Union(parent, wid[a], wid[b]);
                PuertasSetup.Union(parent, wid[b], wid[c]);
            }
            if (propia.tris.Count > 0) piezasHoja.Add(propia);
            var porRaiz = new Dictionary<int, PuertasSetup.Pieza>();
            for (int k = 0; k < esSlab[s].Length; k++)
            {
                if (esSlab[s][k] || esCap[s][k]) continue;
                int a = tris[s][3 * k], b = tris[s][3 * k + 1], c = tris[s][3 * k + 2];
                int r = PuertasSetup.Find(parent, wid[a]);
                PuertasSetup.Pieza p;
                if (!porRaiz.TryGetValue(r, out p))
                {
                    p = new PuertasSetup.Pieza { sub = s, b = new Bounds(v[a], Vector3.zero) };
                    porRaiz[r] = p; piezas.Add(p);
                }
                p.tris.Add(a); p.tris.Add(b); p.tris.Add(c);
                p.b.Encapsulate(v[a]); p.b.Encapsulate(v[b]); p.b.Encapsulate(v[c]);
            }
        }

        // 5) piezas pequenas pegadas a la hoja (manija, rosetas) van con ella; el resto es marco
        var piezasMarco = new List<PuertasSetup.Pieza>();
        var extras = new List<PuertasSetup.Pieza>();
        Bounds? marcoB = null;
        foreach (var p in piezas)
        {
            p.m = new Vector3(p.b.size.x * sc.x, p.b.size.y * sc.y, p.b.size.z * sc.z);
            bool alta = p.m.y >= 1.0f;
            bool enHoja = false;
            if (!alta)
            {
                Vector2 c2 = new Vector2(p.b.center.x, p.b.center.z) - C2;
                float u = Vector2.Dot(c2, dir), w = Vector2.Dot(c2, perp);
                enHoja = Mathf.Abs(u) <= largo * 0.5f + 0.03f / sc.x
                      && Mathf.Abs(w) <= grosor * 0.5f + 0.15f / sc.x
                      && p.b.center.y >= ymin - 0.03f / sc.y && p.b.center.y <= ymax + 0.03f / sc.y;
            }
            if (enHoja) { extras.Add(p); piezasHoja.Add(p); }
            else
            {
                piezasMarco.Add(p);
                if (alta) { if (marcoB == null) marcoB = p.b; else { var t = marcoB.Value; t.Encapsulate(p.b); marcoB = t; } }
            }
        }
        if (marcoB == null) { msg = "no encontre el marco (ninguna pieza alta fuera de la hoja)."; return null; }
        Bounds fb = marcoB.Value;
        int wAxis = fb.size.x * sc.x >= fb.size.z * sc.z ? 0 : 2;
        int tAxis = wAxis == 0 ? 2 : 0;

        // 6) bisagra: el extremo lejos de la manija (o el mas cercano al marco)
        Vector2 hinge2, free2;
        string aviso = null;
        if (extras.Count > 0)
        {
            Vector2 cen = Vector2.zero;
            foreach (var p in extras) cen += new Vector2(p.b.center.x, p.b.center.z);
            cen /= extras.Count;
            bool aEsBisagra = Vector2.Distance(extA, cen) > Vector2.Distance(extB, cen);
            hinge2 = aEsBisagra ? extA : extB; free2 = aEsBisagra ? extB : extA;
        }
        else
        {
            Vector2 fc = new Vector2(fb.center.x, fb.center.z);
            bool aEsBisagra = Vector2.Distance(extA, fc) < Vector2.Distance(extB, fc);
            hinge2 = aEsBisagra ? extA : extB; free2 = aEsBisagra ? extB : extA;
            aviso = "no vi manija; adivine el lado de la bisagra. Si queda al reves usa 'Voltear bisagra'.";
        }

        Vector2 dHF = (free2 - hinge2).normalized;
        float phiHF = Mathf.Atan2(dHF.y, dHF.x) * Mathf.Rad2Deg;
        float hingeW = wAxis == 0 ? hinge2.x : hinge2.y;
        float frameW = wAxis == 0 ? fb.center.x : fb.center.z;
        float sgn = hingeW < frameW ? 1f : -1f;
        float phiT = wAxis == 0 ? (sgn > 0 ? 0f : 180f) : (sgn > 0 ? 90f : -90f);
        float theta = Mathf.DeltaAngle(phiT, phiHF);

        Vector3 pivote = new Vector3(hinge2.x, cy, hinge2.y);
        Vector3 centro = new Vector3(C2.x, cy, C2.y);
        Quaternion cierre = Quaternion.Euler(0f, theta, 0f);

        var d = new Datos();
        d.pivote = pivote; d.centro = centro; d.theta = theta;
        d.phi = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        d.largo = largo; d.alto = alto; d.grosor = grosor;
        d.libre = Vector3.zero; d.libre[wAxis] = sgn;
        d.normal = Vector3.zero; d.normal[tAxis] = 1f;
        d.centroCerrado = pivote + cierre * (centro - pivote);
        d.aviso = aviso;

        // 7) meshes nuevos
        var v0 = v; var n = src.normals; var tg = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        PuertasSetup.EnsureFolder();
        string baseName = PuertasSetup.Sanitize(string.IsNullOrEmpty(src.name) ? "PuertaInterior" : src.name);
        List<int> subsM, subsH;
        Mesh mM = PuertasSetup.Construir(v0, n, tg, uv, uv2, col, piezasMarco, baseName + "_Marco", out subsM);
        Mesh mH = PuertasSetup.Construir(v0, n, tg, uv, uv2, col, piezasHoja, baseName + "_Hoja", out subsH);
        d.meshMarco = PuertasSetup.Guardar(mM); d.subsMarco = subsM;
        d.meshHoja = PuertasSetup.Guardar(mH); d.subsHoja = subsH;
        return d;
    }
}
