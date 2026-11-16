using UnityEngine;

/// <summary>
/// Calcula las medidas REALES de un objeto (metros) a partir de su geometria,
/// con el mismo criterio que TapeMeasure:
///  - Se miden en metros del mundo.
///  - Si el objeto esta dentro de una maqueta (MaquetaExhibicion), la medida real es la del
///    espacio local de la maqueta (metros reales de la casa) y ademas se da la medida en la maqueta.
///  - Se miden en el marco propio del objeto (giro en Y), no en la caja alineada al mundo, asi un
///    mueble girado 30 grados mide lo mismo que girado 0 (Renderer.bounds lo inflaria).
///  - Se usa la escala completa de la jerarquia (localToWorldMatrix): el 0.3048 de Revit
///    (pies a metros) queda aplicado.
/// Va en cualquier carpeta de scripts (NO en Editor).
/// </summary>
public static class MedidasReales
{
    public struct Dimensiones
    {
        public float largo;   // el mayor de los dos lados horizontales
        public float ancho;   // el menor de los dos lados horizontales
        public float alto;    // vertical
        public bool valido;
    }

    /// <summary>
    /// Mide todos los renderers de 'objeto' (incluidos hijos).
    /// 'espacio' = null mide en metros del mundo; si no, en el espacio local de ese Transform (maqueta).
    /// </summary>
    public static Dimensiones Calcular(Transform objeto, Transform espacio = null)
    {
        var res = new Dimensiones();
        if (objeto == null) return res;

        Matrix4x4 aEspacio = espacio != null ? espacio.worldToLocalMatrix : Matrix4x4.identity;
        Quaternion rotEspacio = espacio != null ? espacio.rotation : Quaternion.identity;

        // Giro del objeto alrededor de Y (ignora inclinaciones raras de importacion).
        Vector3 f = Vector3.ProjectOnPlane(objeto.forward, Vector3.up);
        Quaternion yaw = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up)
                                                 : Quaternion.identity;
        // Del espacio de medicion al marco del objeto.
        Quaternion aMarco = Quaternion.Inverse(Quaternion.Inverse(rotEspacio) * yaw);

        bool hay = false;
        Bounds b = new Bounds();
        foreach (var r in objeto.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            Bounds lb = r.localBounds;
            if (lb.size == Vector3.zero) continue;

            Matrix4x4 m = aEspacio * r.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 p = lb.center + Vector3.Scale(lb.extents, new Vector3(x, y, z));
                        p = aMarco * m.MultiplyPoint3x4(p);
                        if (!hay) { b = new Bounds(p, Vector3.zero); hay = true; }
                        else b.Encapsulate(p);
                    }
        }
        if (!hay) return res;

        Vector3 s = b.size;
        res.alto = s.y;
        res.largo = Mathf.Max(s.x, s.z);
        res.ancho = Mathf.Min(s.x, s.z);
        res.valido = true;
        return res;
    }

    /// <summary>Mismo formato que TapeMeasure.FormatLength: metros desde 1 m, si no centimetros.</summary>
    public static string Formato(float metros)
    {
        return metros >= 1f ? metros.ToString("F2") + " m" : (metros * 100f).ToString("F1") + " cm";
    }

    static string Linea(Dimensiones d)
    {
        return "Largo " + Formato(d.largo) + " · Ancho " + Formato(d.ancho) + " · Alto " + Formato(d.alto);
    }

    /// <summary>
    /// Texto listo para el panel (admite rich text). Vacio si el objeto no tiene geometria.
    /// En una maqueta da la medida real de la casa y debajo la de la maqueta.
    /// </summary>
    public static string Texto(Transform objeto)
    {
        var maqueta = objeto.GetComponentInParent<MaquetaExhibicion>();
        var real = Calcular(objeto, maqueta != null ? maqueta.transform : null);
        if (!real.valido) return "";

        string t = "<b>Medidas:</b> " + Linea(real);
        if (maqueta != null)
        {
            var enMaqueta = Calcular(objeto, null);
            if (enMaqueta.valido)
                t += "\n<size=70%>en la maqueta: " + Linea(enMaqueta) + "</size>";
        }
        return t;
    }
}