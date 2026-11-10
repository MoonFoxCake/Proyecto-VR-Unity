using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Maquetas de exhibicion hechas desde el modelo ACTUAL de la escena (nunca quedan viejas):
///   1. Conjunto: todo el modelo a 1:50, flotando QUIETO.
///   2. Una casa: la casa de la pieza seleccionada a 1:20, flotando y GIRANDO, con vista por pisos.
/// Cada maqueta queda en una exhibicion: base de luz con placa (PedestalMaqueta), pista al acercarse,
/// punto "Estas aqui" (PuntoEstasAqui). Se agarra y vuelve sola (MaquetaExhibicion).
/// Las mallas se juntan en pocas (una por material y nivel) para que el Quest aguante, y se omiten
/// piezas diminutas. Volver a correr una herramienta reemplaza SU maqueta en el mismo lugar.
///
/// Como se aisla una casa (el modelo de Revit viene plano, sin grupos por casa):
/// bajo la pieza seleccionada se busca el piso de la casa; su huella (junto con los pisos,
/// cielos y techos encima) define la casa. Entra todo lo que cabe en esa huella, mas lo que la
/// toca sin ser del tamano del terreno (alero del techo, escalera exterior, barandas).
/// Los niveles salen de las alturas de las losas (pisos, cielos, techo), no de los nombres.
/// </summary>
public static class MaquetaSetup
{
    const string NombreModelo = "Proyecto Interpretacion_F";
    const string CarpetaBase = "Assets/Model/MaquetaGenerada";
    const string CarpetaMateriales = CarpetaBase + "/Materiales";
    const int AvisoTriangulos = 300000;   // presupuesto razonable para el Quest 3S
    const float AlturaSobreBase = 0.08f;  // metros entre el disco y la maqueta

    class Tipo
    {
        public string origen, nombre, carpeta, titulo, pista;
        public float escala, tamanoMinimo;
        public bool girar;
    }

    static readonly Tipo Conjunto = new Tipo
    {
        origen = "conjunto", nombre = "Maqueta Conjunto", carpeta = "Conjunto", titulo = "Conjunto",
        escala = 50f, tamanoMinimo = 0.2f, girar = false,
        pista = "Agarra para explorar"
    };
    static readonly Tipo UnaCasa = new Tipo
    {
        origen = "casa", nombre = "Maqueta Casa", carpeta = "Casa", titulo = "Casa tipo",
        escala = 20f, tamanoMinimo = 0.08f, girar = true,
        pista = "Agarra para explorar\n<size=70%>Gatillo: ver por pisos</size>"
    };

    class Pieza
    {
        public MeshRenderer r;
        public MeshFilter mf;
        public Bounds caja; // metros, en el espacio del modelo (alineado con el)
    }

    class Datos
    {
        public Vector3[] v, n;
        public Vector2[] uv;
        public int[][] tris;
    }

    // ------------------------------------------------------------ menus

    [MenuItem("Tools/VR/Maqueta/1. Regenerar maqueta del conjunto (quieta)")]
    static void MenuConjunto()
    {
        var modelo = GameObject.Find(NombreModelo);
        if (modelo == null) { Aviso("No encontre '" + NombreModelo + "' en la escena."); return; }
        var piezas = Piezas(modelo.transform, out int noLegibles);
        Construir(Conjunto, modelo.transform, new List<List<Pieza>> { piezas }, noLegibles, "todo '" + modelo.name + "'");
    }

    [MenuItem("Tools/VR/Maqueta/2. Crear maqueta de la casa seleccionada (gira)")]
    static void MenuCasa()
    {
        var sel = Selection.activeGameObject;
        if (sel == null || sel.GetComponentInParent<MaquetaExhibicion>() != null || sel.GetComponentInParent<PedestalMaqueta>() != null)
        {
            Aviso("Selecciona en la Hierarchy cualquier pieza de la casa que quieres (un muro, una puerta, el piso...) y repite.");
            return;
        }
        var modelo = GameObject.Find(NombreModelo);
        Transform raizModelo = modelo != null && sel.transform.IsChildOf(modelo.transform) ? modelo.transform : sel.transform.root;

        var todas = Piezas(raizModelo, out int noLegibles);
        Vector3 p = Alinear(raizModelo).MultiplyPoint3x4(CentroDe(sel));

        // 1) el piso de la casa debajo de lo seleccionado
        var pisos = todas.Where(q => EsLosa(q.caja, 0.5f) && q.caja.min.x <= p.x && p.x <= q.caja.max.x && q.caja.min.z <= p.z && p.z <= q.caja.max.z).ToList();
        if (pisos.Count == 0) { Aviso("No encontre el piso de una casa debajo de '" + sel.name + "'. Selecciona una pieza que este dentro de la casa."); return; }
        Bounds piso = pisos.OrderByDescending(q => q.caja.size.x * q.caja.size.z).First().caja;

        // 2) huella: ese piso + pisos, cielos y techos que estan encima de el
        float xMin = piso.min.x, xMax = piso.max.x, zMin = piso.min.z, zMax = piso.max.z;
        var losas = new List<Bounds>();
        foreach (var q in todas)
        {
            if (!EsLosa(q.caja, 1.5f) || Superposicion(q.caja, piso) < 0.5f) continue;
            losas.Add(q.caja);
            xMin = Mathf.Min(xMin, q.caja.min.x); xMax = Mathf.Max(xMax, q.caja.max.x);
            zMin = Mathf.Min(zMin, q.caja.min.z); zMax = Mathf.Max(zMax, q.caja.max.z);
        }

        // 3) lo que pertenece a la casa
        const float tol = 0.3f, maxTocando = 6f;
        var casa = new List<Pieza>();
        foreach (var q in todas)
        {
            Bounds b = q.caja;
            bool dentro = b.min.x >= xMin - tol && b.max.x <= xMax + tol && b.min.z >= zMin - tol && b.max.z <= zMax + tol;
            bool toca = b.min.x <= xMax + tol && b.max.x >= xMin - tol && b.min.z <= zMax + tol && b.max.z >= zMin - tol;
            bool centroEnUnEje = (b.center.x >= xMin && b.center.x <= xMax) || (b.center.z >= zMin && b.center.z <= zMax);
            bool chica = Mathf.Max(b.size.x, b.size.z) <= maxTocando;
            if (dentro || (toca && chica && centroEnUnEje)) casa.Add(q);
        }

        // 4) niveles: grupos de losas separados por mas de 1.2 m de altura => cortes
        var alturas = losas.Select(b => b.min.y).OrderBy(y => y).ToList();
        var cortes = new List<float>();
        for (int i = 1; i < alturas.Count; i++)
            if (alturas[i] - alturas[i - 1] > 1.2f) cortes.Add(alturas[i]);
        var niveles = Enumerable.Range(0, cortes.Count + 1).Select(_ => new List<Pieza>()).ToList();
        foreach (var q in casa) niveles[cortes.Count(c => q.caja.center.y >= c)].Add(q);
        niveles.RemoveAll(l => l.Count == 0);

        Construir(UnaCasa, raizModelo, niveles, noLegibles,
                  "la casa de '" + sel.name + "' (" + (xMax - xMin).ToString("F1") + " x " + (zMax - zMin).ToString("F1") + " m, " + niveles.Count + " niveles)");
    }

    // ------------------------------------------------------------ construir

    static void Construir(Tipo tipo, Transform raizModelo, List<List<Pieza>> niveles, int noLegibles, string descripcion)
    {
        // ---- piezas visibles a esta escala, agrupadas por nivel y material
        var grupos = new List<Dictionary<Material, List<(MeshFilter mf, int sub)>>>();
        int incluidas = 0, omitidas = 0;
        Bounds? total = null;
        foreach (var nivel in niveles)
        {
            var g = new Dictionary<Material, List<(MeshFilter, int)>>();
            grupos.Add(g);
            foreach (var q in nivel)
            {
                if (Mathf.Max(q.caja.size.x, q.caja.size.y, q.caja.size.z) < tipo.tamanoMinimo) { omitidas++; continue; }
                incluidas++;
                if (total == null) total = q.caja; else { var t = total.Value; t.Encapsulate(q.caja); total = t; }
                var mats = q.r.sharedMaterials;
                for (int s = 0; s < q.mf.sharedMesh.subMeshCount; s++)
                {
                    var mat = mats.Length == 0 ? null : mats[Mathf.Min(s, mats.Length - 1)];
                    if (mat == null) continue;
                    if (!g.TryGetValue(mat, out var lista)) g[mat] = lista = new List<(MeshFilter, int)>();
                    lista.Add((q.mf, s));
                }
            }
        }
        if (total == null) { Aviso("No quedaron piezas visibles para la maqueta."); return; }
        Bounds caja = total.Value;
        Vector3 baseCentro = new Vector3(caja.center.x, caja.min.y, caja.center.z);

        // ---- maquetas existentes
        var existentes = Object.FindObjectsByType<MaquetaExhibicion>(FindObjectsInactive.Include);
        var anterior = existentes.FirstOrDefault(m => OrigenDe(m) == tipo.origen);
        var conjunto = existentes.FirstOrDefault(m => OrigenDe(m) == Conjunto.origen);
        var vieja = tipo == Conjunto && anterior == null ? BuscarMaquetaVieja() : null;
        GameObject aBorrar = null;
        if (anterior != null)
        {
            var ped = anterior.transform.parent != null ? anterior.transform.parent.GetComponent<PedestalMaqueta>() : null;
            aBorrar = ped != null ? ped.gameObject : anterior.gameObject;
        }

        // ---- donde va la base de la exhibicion
        Vector3 posicion;
        Quaternion rotacion = Quaternion.identity;
        if (aBorrar != null) { posicion = aBorrar.transform.position; rotacion = aBorrar.transform.rotation; }
        else if (vieja != null) posicion = BaseDe(vieja);
        else if (tipo == UnaCasa && conjunto != null)
        {
            var baseConjunto = conjunto.transform.parent != null && conjunto.transform.parent.GetComponent<PedestalMaqueta>() != null
                               ? conjunto.transform.parent : conjunto.transform;
            float radioConjunto = RadioBase(conjunto.GetComponentsInChildren<Renderer>());
            posicion = baseConjunto.position + Vector3.right * (radioConjunto + 0.3f + RadioPara(caja, tipo.escala));
        }
        else
        {
            var sv = SceneView.lastActiveSceneView;
            posicion = sv != null ? sv.pivot : Vector3.up;
        }

        // ---- mallas combinadas, guardadas como assets (no inflan la escena)
        string carpeta = CarpetaBase + "/" + tipo.carpeta;
        CrearCarpeta("Assets", "Model");
        CrearCarpeta("Assets/Model", "MaquetaGenerada");
        if (AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.DeleteAsset(carpeta);
        AssetDatabase.CreateFolder(CarpetaBase, tipo.carpeta);
        if (tipo == Conjunto) BorrarMallasSueltas(); // de la primera version de la herramienta

        Matrix4x4 alinear = Alinear(raizModelo);
        var cache = new Dictionary<Mesh, Datos>();
        var mallasPorNivel = new List<List<(Mesh mesh, Material mat)>>();
        long triangulos = 0;
        int paso = 0, pasos = grupos.Sum(g => g.Count);
        try
        {
            for (int n = 0; n < grupos.Count; n++)
            {
                var mallas = new List<(Mesh, Material)>();
                mallasPorNivel.Add(mallas);
                foreach (var g in grupos[n])
                {
                    EditorUtility.DisplayProgressBar(tipo.nombre, "Juntando " + g.Key.name, (float)paso++ / Mathf.Max(1, pasos));
                    var mesh = Combinar(g.Value, alinear, baseCentro, cache);
                    if (mesh == null) continue;
                    mesh.name = tipo.carpeta + "_N" + (n + 1) + "_" + PuertasSetup.Sanitize(g.Key.name);
                    string ruta = AssetDatabase.GenerateUniqueAssetPath(carpeta + "/" + mesh.name + ".asset");
                    AssetDatabase.CreateAsset(mesh, ruta);
                    mallas.Add((AssetDatabase.LoadAssetAtPath<Mesh>(ruta), g.Key));
                    triangulos += mesh.triangles.LongLength / 3;
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        var matDisco = MaterialUnlit("Pedestal_Disco", new Color(0.45f, 0.8f, 1f, 0.18f), true);
        var matAnillo = MaterialUnlit("Pedestal_Anillo", new Color(0.45f, 0.8f, 1f, 0.6f), true);
        var matPunto = MaterialUnlit("EstasAqui", new Color(1f, 0.35f, 0.15f, 1f), false);
        AssetDatabase.SaveAssets();

        // ---- objetos
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Crear " + tipo.nombre);
        int grupoUndo = Undo.GetCurrentGroup();

        if (aBorrar != null) Undo.DestroyObjectImmediate(aBorrar);
        if (vieja != null) { Undo.RecordObject(vieja, "Apagar maqueta vieja"); vieja.SetActive(false); }

        // Exhibicion (no se mueve): base + placa + pista. La maqueta es su hija.
        var exhibicionGO = new GameObject("Exhibicion - " + tipo.nombre);
        Undo.RegisterCreatedObjectUndo(exhibicionGO, "Crear " + tipo.nombre);
        exhibicionGO.transform.SetPositionAndRotation(posicion, rotacion);

        var raiz = new GameObject(tipo.nombre);
        raiz.transform.SetParent(exhibicionGO.transform, false);
        raiz.transform.localPosition = Vector3.up * AlturaSobreBase;
        raiz.transform.localScale = Vector3.one / tipo.escala;

        var modeloGO = new GameObject("Modelo");
        modeloGO.transform.SetParent(raiz.transform, false);
        var transformsNivel = new List<Transform>();
        for (int n = 0; n < mallasPorNivel.Count; n++)
        {
            Transform padre = modeloGO.transform;
            if (mallasPorNivel.Count > 1)
            {
                var nivelGO = new GameObject("Nivel " + (n + 1));
                nivelGO.transform.SetParent(modeloGO.transform, false);
                padre = nivelGO.transform;
                transformsNivel.Add(padre);
            }
            foreach (var (mesh, mat) in mallasPorNivel[n])
            {
                var go = new GameObject(mat.name);
                go.transform.SetParent(padre, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                // Colision con la forma real: se agarra donde se apunta y el medidor marca sobre los muros.
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        var rb = raiz.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var grab = raiz.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.selectMode = InteractableSelectMode.Multiple;

        var transformador = raiz.AddComponent<XRGeneralGrabTransformer>();
        transformador.allowTwoHandedScaling = true;
        transformador.minimumScaleRatio = 0.5f;
        transformador.maximumScaleRatio = 4f;

        var exhibicion = raiz.AddComponent<MaquetaExhibicion>();
        exhibicion.girar = tipo.girar;
        exhibicion.origen = tipo.origen;
        exhibicion.fuente = raizModelo;
        exhibicion.origenLocal = baseCentro;

        var punto = raiz.AddComponent<PuntoEstasAqui>();
        punto.material = matPunto;

        if (transformsNivel.Count > 1)
        {
            var pisos = raiz.AddComponent<PisosMaqueta>();
            pisos.niveles = transformsNivel;
        }

        var pedestal = exhibicionGO.AddComponent<PedestalMaqueta>();
        pedestal.maqueta = exhibicion;
        pedestal.radio = RadioPara(caja, tipo.escala);
        pedestal.materialDisco = matDisco;
        pedestal.materialAnillo = matAnillo;
        pedestal.titulo = tipo.titulo;
        pedestal.subtitulo = "Escala 1:" + tipo.escala;
        pedestal.textoPista = transformsNivel.Count > 1 ? tipo.pista : Conjunto.pista;

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(exhibicionGO.scene);
        Selection.activeGameObject = exhibicionGO;
        EditorGUIUtility.PingObject(exhibicionGO);

        string texto = tipo.nombre + " 1:" + tipo.escala + (tipo.girar ? " (gira)" : " (quieta)") + "\nDesde " + descripcion + ".\n\n" +
                       "Piezas incluidas: " + incluidas + "  (omitidas por chicas: " + omitidas + ")\n" +
                       (transformsNivel.Count > 1 ? "Niveles para la vista por pisos: " + transformsNivel.Count + "\n" : "") +
                       "Mallas: " + mallasPorNivel.Sum(l => l.Count) + "\n" +
                       "Triangulos: " + triangulos.ToString("N0") +
                       (triangulos > AvisoTriangulos ? "  <- ALTO para el Quest; sube tamanoMinimo en MaquetaSetup.cs" : "") + "\n" +
                       (noLegibles > 0 ? "OJO: " + noLegibles + " piezas sin Read/Write quedaron fuera (activalo en el importador del modelo y repite).\n" : "") +
                       (vieja != null ? "La maqueta vieja (hecha a mano) quedo APAGADA; puedes borrarla.\n" : "") +
                       "\nGuarda la escena (Ctrl+S).";
        Debug.Log("[Maqueta] " + texto.Replace("\n", " "));
        EditorUtility.DisplayDialog("Maqueta", texto, "OK");
    }

    // ------------------------------------------------------------ utilidades

    static void Aviso(string texto)
    {
        Debug.LogWarning("[Maqueta] " + texto);
        EditorUtility.DisplayDialog("Maqueta", texto, "OK");
    }

    static string OrigenDe(MaquetaExhibicion m) => string.IsNullOrEmpty(m.origen) ? Conjunto.origen : m.origen;

    static void CrearCarpeta(string padre, string nombre)
    {
        if (!AssetDatabase.IsValidFolder(padre + "/" + nombre)) AssetDatabase.CreateFolder(padre, nombre);
    }

    // Radio del disco: la diagonal de la huella a escala (asi cabe aunque la maqueta gire) + margen.
    static float RadioPara(Bounds caja, float escala) =>
        new Vector2(caja.extents.x, caja.extents.z).magnitude / escala + 0.04f;

    static float RadioBase(Renderer[] rs)
    {
        if (rs.Length == 0) return 0.5f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return new Vector2(b.extents.x, b.extents.z).magnitude;
    }

    // Material URP Unlit guardado como asset (si es solo creado en codigo, el build puede quitar el shader).
    static Material MaterialUnlit(string nombre, Color color, bool transparente)
    {
        CrearCarpeta(CarpetaBase, "Materiales");
        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;

        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        m = new Material(sh) { name = nombre };
        m.SetColor("_BaseColor", color);
        if (transparente)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // Espacio del modelo pero en metros del mundo (quita posicion y giro del modelo, no su escala).
    static Matrix4x4 Alinear(Transform modelo) =>
        Matrix4x4.Rotate(Quaternion.Inverse(modelo.rotation)) * Matrix4x4.Translate(-modelo.position);

    static List<Pieza> Piezas(Transform modelo, out int noLegibles)
    {
        noLegibles = 0;
        Matrix4x4 alinear = Alinear(modelo);
        var lista = new List<Pieza>();
        foreach (var r in modelo.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!r.enabled || r.GetComponentInParent<MaquetaExhibicion>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (!mf.sharedMesh.isReadable) { noLegibles++; continue; }
            lista.Add(new Pieza { r = r, mf = mf, caja = Transformar(mf.sharedMesh.bounds, alinear * r.localToWorldMatrix) });
        }
        return lista;
    }

    static Bounds Transformar(Bounds b, Matrix4x4 m)
    {
        Vector3 c = m.MultiplyPoint3x4(b.center);
        Vector3 e = b.extents;
        Vector3 ext = new Vector3(
            Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
            Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
            Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
        return new Bounds(c, ext * 2f);
    }

    // Losa (piso, cielo, techo) del tamano de una casa: delgada y de 3 a 15 m de lado.
    static bool EsLosa(Bounds b, float grosorMax) =>
        b.size.y < grosorMax && b.size.x > 3f && b.size.x < 15f && b.size.z > 3f && b.size.z < 15f;

    // Fraccion de la huella de "a" que cae sobre "b".
    static float Superposicion(Bounds a, Bounds b)
    {
        float ix = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
        float iz = Mathf.Max(0f, Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
        return ix * iz / Mathf.Max(1e-6f, a.size.x * a.size.z);
    }

    static Vector3 CentroDe(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    // Junta las submallas en una sola malla en metros, con el centro de la base en el origen.
    static Mesh Combinar(List<(MeshFilter mf, int sub)> piezas, Matrix4x4 alinear, Vector3 origen, Dictionary<Mesh, Datos> cache)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        foreach (var (mf, sub) in piezas)
        {
            var src = mf.sharedMesh;
            if (!cache.TryGetValue(src, out var d))
            {
                d = new Datos { v = src.vertices, n = src.normals, uv = src.uv, tris = new int[src.subMeshCount][] };
                cache[src] = d;
            }
            if (d.tris[sub] == null) d.tris[sub] = src.GetTriangles(sub);

            Matrix4x4 m = Matrix4x4.Translate(-origen) * alinear * mf.transform.localToWorldMatrix;
            Matrix4x4 mn = m.inverse.transpose;
            bool espejo = m.determinant < 0f; // escala negativa: invertir el orden para que las caras no queden al reves

            var idx = d.tris[sub];
            var mapa = new Dictionary<int, int>();
            for (int k = 0; k + 2 < idx.Length; k += 3)
            {
                int a = Mapear(idx[k], d, m, mn, mapa, v, n, uv);
                int b = Mapear(idx[k + 1], d, m, mn, mapa, v, n, uv);
                int c = Mapear(idx[k + 2], d, m, mn, mapa, v, n, uv);
                if (espejo) { t.Add(a); t.Add(c); t.Add(b); }
                else { t.Add(a); t.Add(b); t.Add(c); }
            }
        }
        if (t.Count == 0) return null;
        var mesh = new Mesh { indexFormat = v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        // Sin tangentes (los materiales de la maqueta no las necesitan) y con compresion al guardar:
        // los .asset se guardan como texto y sin esto pesan ~4 veces mas en el repo.
        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
        mesh.UploadMeshData(false);
        return mesh;
    }

    static int Mapear(int i, Datos d, Matrix4x4 m, Matrix4x4 mn, Dictionary<int, int> mapa,
                      List<Vector3> v, List<Vector3> n, List<Vector2> uv)
    {
        if (mapa.TryGetValue(i, out int j)) return j;
        j = v.Count;
        mapa[i] = j;
        v.Add(m.MultiplyPoint3x4(d.v[i]));
        n.Add(d.n.Length > i ? mn.MultiplyVector(d.n[i]).normalized : Vector3.up);
        uv.Add(d.uv.Length > i ? d.uv[i] : Vector2.zero);
        return j;
    }

    // Mallas que la primera version dejaba sueltas en CarpetaBase (las usaba la maqueta del conjunto).
    static void BorrarMallasSueltas()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { CarpetaBase }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(ruta).Replace('\\', '/') == CarpetaBase) AssetDatabase.DeleteAsset(ruta);
        }
    }

    // La maqueta hecha a mano (prefab Assets/Model/Maqueta.prefab), si sigue en la escena.
    static GameObject BuscarMaquetaVieja()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Maqueta.prefab");
        if (prefab == null) return null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            var go = t.gameObject;
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go) && PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab) return go;
        }
        return null;
    }

    static Vector3 BaseDe(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }
}
