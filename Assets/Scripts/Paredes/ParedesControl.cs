using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cambia el acabado de todos los muros a la vez: una textura, un color, o ambos
/// (el color se aplica encima de la textura: Ladrillo + verde = ladrillos verdes).
/// Los muros se encuentran por tag (por defecto "Paredes", en toda la escena).
/// Si nadie tiene ese tag, se usa el filtro por nombre ("Muro", como los
/// "Muro basico ..." que exporta Revit).
/// Metodos publicos para botones: UsarTextura(indice), QuitarTextura(), SetColor(color),
/// QuitarColor(), Restaurar().
/// </summary>
public class ParedesControl : MonoBehaviour
{
    [Tooltip("Se pintan los objetos con este tag (y sus hijos), en toda la escena.")]
    public string tagParedes = "Paredes";

    [Header("Alternativa si nadie tiene el tag")]
    [Tooltip("Raiz del modelo donde buscar por nombre. Vacio = toda la escena.")]
    public Transform modelo;
    [Tooltip("Se pintan los objetos cuyo nombre (o el de un padre) contiene este texto.")]
    public string filtroNombre = "Muro";

    [Header("Acabados")]
    [Tooltip("Material URP/Lit que se usa de base (se copia, no se modifica).")]
    public Material materialBase;
    [Tooltip("Version a color de cada textura (se usa cuando no se eligio color).")]
    public Texture2D[] texturas = new Texture2D[3];
    [Tooltip("Version en grises de cada textura (se usa cuando se elige un color, para que se tina bien). " +
             "Opcional: si falta, se tine la version a color.")]
    public Texture2D[] texturasParaTenir = new Texture2D[3];
    [Tooltip("Color de la pared lisa cuando no se eligio color.")]
    public Color colorLiso = new Color(0.93f, 0.9f, 0.84f);
    [Tooltip("Cuantos metros de pared cubre una repeticion de la textura.")]
    public float metrosPorRepeticion = 1.2f;
    [Tooltip("Metros que mide 1 unidad de UV del modelo. Los muros de Revit vienen en pies: 0.3048.")]
    public float metrosPorUnidadUV = 0.3048f;

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    readonly List<Renderer> muros = new List<Renderer>();
    readonly List<Material[]> originales = new List<Material[]>();
    Material material;

    // Estado actual: textura (-1 = lisa) y color elegido (null = natural).
    int texturaActual = -1;
    Color? colorElegido;

    public int CantidadMuros => muros.Count;

    void Awake()
    {
        BuscarMuros();
        if (materialBase != null) material = new Material(materialBase) { name = "Paredes (runtime)" };
        else Debug.LogWarning("[Paredes] Falta el Material Base.", this);
    }

    /// <summary>Vuelve a buscar los muros (por si se agregaron o se cambio el tag).</summary>
    public void BuscarMuros()
    {
        muros.Clear();
        originales.Clear();

        foreach (var go in BuscarPorTag())
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                Agregar(r);

        if (muros.Count > 0)
        {
            Debug.Log($"[Paredes] {muros.Count} muros encontrados con el tag '{tagParedes}'.", this);
            return;
        }

        var candidatos = modelo != null
            ? modelo.GetComponentsInChildren<Renderer>(true)
            : FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in candidatos)
            if (EsMuro(r.transform)) Agregar(r);

        if (muros.Count > 0)
            Debug.Log($"[Paredes] Nadie tiene el tag '{tagParedes}'; {muros.Count} muros encontrados por nombre ('{filtroNombre}').", this);
        else
            Debug.LogWarning($"[Paredes] No encontre muros: ni con el tag '{tagParedes}' ni con '{filtroNombre}' en el nombre.", this);
    }

    GameObject[] BuscarPorTag()
    {
        if (string.IsNullOrEmpty(tagParedes)) return new GameObject[0];
        try { return GameObject.FindGameObjectsWithTag(tagParedes); }
        catch (UnityException) // el tag no existe en Tags & Layers
        {
            Debug.LogWarning($"[Paredes] El tag '{tagParedes}' no existe (Edit > Project Settings > Tags and Layers).", this);
            return new GameObject[0];
        }
    }

    void Agregar(Renderer r)
    {
        if (r is ParticleSystemRenderer || muros.Contains(r)) return;
        muros.Add(r);
        originales.Add(r.sharedMaterials);
    }

    bool EsMuro(Transform t)
    {
        for (; t != null && t != modelo; t = t.parent)
            if (t.name.IndexOf(filtroNombre, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    /// <summary>Pone la textura numero <paramref name="indice"/> (0, 1 o 2), manteniendo el color elegido.</summary>
    public void UsarTextura(int indice)
    {
        if (indice < 0 || indice >= texturas.Length || texturas[indice] == null) return;
        texturaActual = indice;
        Actualizar();
    }

    /// <summary>Quita la textura (pared lisa), manteniendo el color elegido.</summary>
    public void QuitarTextura()
    {
        texturaActual = -1;
        Actualizar();
    }

    /// <summary>Pinta los muros de este color; si hay textura, se aplica encima de ella.</summary>
    public void SetColor(Color color)
    {
        color.a = 1f;
        colorElegido = color;
        Actualizar();
    }

    /// <summary>Vuelve al color natural de la textura (o al color liso por defecto).</summary>
    public void QuitarColor()
    {
        colorElegido = null;
        Actualizar();
    }

    /// <summary>Vuelve a los materiales con los que vino el modelo.</summary>
    public void Restaurar()
    {
        texturaActual = -1;
        colorElegido = null;
        for (int i = 0; i < muros.Count; i++)
            if (muros[i] != null) muros[i].sharedMaterials = originales[i];
    }

    void Actualizar()
    {
        if (material == null) return;

        Texture2D tex = null;
        if (texturaActual >= 0)
        {
            // Con color elegido se usa la version en grises: blanco * verde = verde de verdad.
            bool hayGris = colorElegido.HasValue && texturasParaTenir != null &&
                           texturaActual < texturasParaTenir.Length && texturasParaTenir[texturaActual] != null;
            tex = hayGris ? texturasParaTenir[texturaActual] : texturas[texturaActual];
        }

        float rep = metrosPorUnidadUV / Mathf.Max(0.01f, metrosPorRepeticion);
        material.SetTexture(BaseMapId, tex);
        material.SetTextureScale(BaseMapId, new Vector2(rep, rep));
        material.SetColor(BaseColorId, colorElegido ?? (tex != null ? Color.white : colorLiso));
        AplicarAMuros();
    }

    void AplicarAMuros()
    {
        foreach (var r in muros)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            if (mats.Length > 0 && mats[0] == material) continue; // ya lo tiene
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
        }
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
