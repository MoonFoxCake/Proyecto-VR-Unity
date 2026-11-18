using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cambia las texturas con ruido (fotos de tela de 2048 px) de las camas, el sillon y la silla
/// de oficina por materiales lisos URP Lit. Los materiales quedan en Assets/Materiales/Muebles:
/// se pueden retocar el color y la lisura a mano sin volver a correr nada.
/// </summary>
public static class MueblesLimpios
{
    const string Carpeta = "Assets/Materiales/Muebles";

    // (parte del nombre del mueble, material original del GLB, material nuevo)
    static readonly (string nodo, string original, string nuevo)[] Reglas =
    {
        ("Kilppan",       "cama",         "Sofa_Cuero"),
        ("Bed03",         "cama",         "Cama_Tela"),
        ("Double bed",    "cama",         "Cama_Tela"),
        ("Silla-Oficina", "Material.013", "Silla_Negro"),
        ("Silla-Oficina", "Material.014", "Silla_Gris"),
    };

    // nombre -> (color, lisura)
    static readonly Dictionary<string, (string hex, float lisura)> Defs = new Dictionary<string, (string, float)>
    {
        { "Sofa_Cuero",  ("#2E3035", 0.45f) },
        { "Cama_Tela",   ("#D8D2C6", 0.15f) },
        { "Silla_Negro", ("#25292A", 0.30f) },
        { "Silla_Gris",  ("#6A6C70", 0.35f) },
    };

    [MenuItem("Tools/VR/Muebles/Limpiar texturas de camas, sofa y silla")]
    static void Limpiar()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { EditorUtility.DisplayDialog("Muebles", "No encontre el shader URP/Lit.", "OK"); return; }

        var mats = new Dictionary<string, Material>();
        foreach (var kv in Defs) mats[kv.Key] = ObtenerMaterial(kv.Key, kv.Value.hex, kv.Value.lisura, shader);

        int cambiados = 0, renderers = 0;
        var noCoincide = new HashSet<string>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string ruta = Ruta(r.transform);
            var reglasDeEste = Reglas.Where(x => ruta.Contains(x.nodo)).ToList();
            if (reglasDeEste.Count == 0) continue;

            var actuales = r.sharedMaterials;
            bool cambio = false;
            for (int i = 0; i < actuales.Length; i++)
            {
                if (actuales[i] == null) continue;
                string nom = Limpio(actuales[i].name);
                var regla = reglasDeEste.FirstOrDefault(x => nom.Equals(x.original, System.StringComparison.OrdinalIgnoreCase));
                if (regla.nuevo != null)
                {
                    if (actuales[i] != mats[regla.nuevo]) { actuales[i] = mats[regla.nuevo]; cambio = true; cambiados++; }
                }
                else if (!Defs.ContainsKey(nom)) noCoincide.Add(r.transform.name + " > " + nom);
            }
            if (cambio)
            {
                Undo.RecordObject(r, "Limpiar texturas de muebles");
                r.sharedMaterials = actuales;
                EditorUtility.SetDirty(r);
                if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                renderers++;
            }
        }
        if (cambiados > 0) EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine("Materiales cambiados: " + cambiados + " (en " + renderers + " objetos).");
        if (cambiados == 0) sb.AppendLine("\nNo encontre camas, sofa ni silla con esos materiales en la escena abierta. Abre SampleScene y vuelve a correr.");
        if (noCoincide.Count > 0) sb.AppendLine("\nMateriales que no toque en esos muebles (por si ves algo raro):\n- " + string.Join("\n- ", noCoincide.Take(8)));
        sb.AppendLine("\nPara retocar el color o el brillo: Assets/Materiales/Muebles. Ctrl+Z deshace el cambio de la escena.");
        Debug.Log("[Muebles] " + sb);
        EditorUtility.DisplayDialog("Muebles", sb.ToString(), "OK");
    }

    static Material ObtenerMaterial(string nombre, string hex, float lisura, Shader shader)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Materiales", "Muebles");
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m; // ya existe: respeto los cambios de color que alguien le haya hecho
        Color c; ColorUtility.TryParseHtmlString(hex, out c);
        m = new Material(shader) { name = nombre };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", lisura);
        AssetDatabase.CreateAsset(m, ruta);
        return AssetDatabase.LoadAssetAtPath<Material>(ruta);
    }

    static string Ruta(Transform t)
    {
        var sb = new StringBuilder();
        for (; t != null; t = t.parent) sb.Append(t.name).Append('/');
        return sb.ToString();
    }

    static string Limpio(string n) { return n.Replace(" (Instance)", "").Trim(); }
}
