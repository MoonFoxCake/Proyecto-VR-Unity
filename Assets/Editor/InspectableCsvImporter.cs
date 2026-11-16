#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rellena InspectableObject a partir de un CSV, buscando el patrón dentro del
/// nombre de cada objeto (sin importar mayúsculas ni tildes). Gana la primera
/// línea del CSV que coincida. Debe estar dentro de una carpeta llamada "Editor".
/// Menú: Tools > VR > Inspector
/// </summary>
public static class InspectableCsvImporter
{
    class Entry
    {
        public string pattern;
        public string title;
        public string description;
        public List<InfoEntry> chars = new List<InfoEntry>();
    }

    [MenuItem("Tools/VR/Inspector/Aplicar CSV a los hijos directos de la selección")]
    static void ApplyToChildren() { Run(true); }

    [MenuItem("Tools/VR/Inspector/Aplicar CSV a los objetos seleccionados")]
    static void ApplyToSelected() { Run(false); }

    [MenuItem("Tools/VR/Inspector/Aplicar CSV a los hijos directos de la selección", true)]
    [MenuItem("Tools/VR/Inspector/Aplicar CSV a los objetos seleccionados", true)]
    static bool HasSelection() { return Selection.gameObjects.Length > 0; }

    static void Run(bool directChildren)
    {
        string path = EditorUtility.OpenFilePanel("Selecciona el CSV de descripciones",
                                                  Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        List<Entry> entries = Load(path);
        if (entries.Count == 0)
        {
            EditorUtility.DisplayDialog("CSV vacío",
                "No se encontró ninguna línea válida en el CSV.", "OK");
            return;
        }

        // 0 = Respetar, 1 = Cancelar, 2 = Sobrescribir
        int opt = EditorUtility.DisplayDialogComplex(
            "Objetos que ya tienen información",
            "Algunos objetos pueden tener ya una descripción escrita por ti.\n¿Qué hago con ellos?",
            "Respetarlos", "Cancelar", "Sobrescribir");
        if (opt == 1) return;
        bool overwrite = opt == 2;

        var targets = new List<GameObject>();
        foreach (var go in Selection.gameObjects)
        {
            if (!go.scene.IsValid()) continue;
            if (!directChildren) targets.Add(go);
            else foreach (Transform c in go.transform) targets.Add(c.gameObject);
        }

        int applied = 0, kept = 0;
        var unmatched = new Dictionary<string, int>();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Aplicar CSV a InspectableObject");

        foreach (var go in targets)
        {
            Entry e = Find(entries, go.name);
            if (e == null)
            {
                string key = go.name;
                unmatched[key] = unmatched.ContainsKey(key) ? unmatched[key] + 1 : 1;
                continue;
            }

            var io = go.GetComponent<InspectableObject>();
            if (io == null)
                io = Undo.AddComponent<InspectableObject>(go);
            else
            {
                if (!overwrite && !string.IsNullOrEmpty(io.description)) { kept++; continue; }
                Undo.RecordObject(io, "Aplicar CSV");
            }

            io.title = e.title;
            io.description = e.description;
            io.characteristics = new List<InfoEntry>(e.chars);
            EditorUtility.SetDirty(io);
            EditorSceneManager.MarkSceneDirty(go.scene);
            applied++;
        }

        int unmatchedCount = 0;
        foreach (var kv in unmatched) unmatchedCount += kv.Value;

        string msg = "CSV aplicado a " + applied + " objeto(s). Respetados: " + kept +
                     ". Sin coincidencia: " + unmatchedCount + ".";
        if (unmatched.Count > 0)
        {
            var sb = new StringBuilder(msg + "\nSin coincidencia:\n");
            int shown = 0;
            foreach (var kv in unmatched)
            {
                if (shown++ >= 15) { sb.AppendLine("... y más."); break; }
                sb.AppendLine("  " + kv.Value + " x " + kv.Key);
            }
            msg = sb.ToString();
        }
        Debug.Log(msg);
    }

    // ---------- CSV ----------

    static List<Entry> Load(string path)
    {
        var list = new List<Entry>();
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            string[] p = line.Split(';');
            if (p.Length < 3) continue;
            if (p[0].Trim().ToLowerInvariant() == "patron") continue;

            var e = new Entry
            {
                pattern = Norm(p[0].Trim()),
                title = p[1].Trim(),
                description = p[2].Trim()
            };

            if (p.Length >= 4)
            {
                foreach (string part in p[3].Split('|'))
                {
                    int i = part.IndexOf(':');
                    if (i <= 0) continue;
                    e.chars.Add(new InfoEntry
                    {
                        label = part.Substring(0, i).Trim(),
                        value = part.Substring(i + 1).Trim()
                    });
                }
            }
            if (e.pattern.Length > 0) list.Add(e);
        }
        return list;
    }

    static Entry Find(List<Entry> entries, string objectName)
    {
        string n = Norm(objectName);
        foreach (var e in entries)
            if (n.Contains(e.pattern)) return e;
        return null;
    }

    // Minúsculas y sin tildes (ñ -> n, ã -> a).
    static string Norm(string s)
    {
        string d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }
}
#endif
