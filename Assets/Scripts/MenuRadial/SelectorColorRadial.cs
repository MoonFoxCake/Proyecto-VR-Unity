using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selector de color para el menu radial: una rueda (tono y saturacion) y una
/// barra de brillo. Funciona con los rayos de los controles (clic o arrastrar).
/// Lo construye RadialMenu en las paginas de tipo "Selector Color".
/// </summary>
public class SelectorColorRadial : MonoBehaviour
{
    public event Action<Color> ColorCambiado;

    public Color ColorActual => Color.HSVToRGB(h, s, v);

    float h, s, v = 1f;
    RectTransform rueda, barra, marcaRueda, marcaBarra;
    Image imgBarra, muestra;

    public static SelectorColorRadial Crear(RectTransform padre, Vector2 centroRueda, float diamRueda,
        Vector2 centroBarra, Vector2 tamBarra, Color inicial)
    {
        var go = new GameObject("Selector Color", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.sizeDelta = Vector2.zero;
        var sel = go.AddComponent<SelectorColorRadial>();

        // Rueda
        var imgRueda = NuevaImagen(rt, "Rueda", RadialMenuIcons.RuedaDeColor, Color.white, new Vector2(diamRueda, diamRueda), centroRueda);
        sel.rueda = (RectTransform)imgRueda.transform;
        imgRueda.gameObject.AddComponent<ZonaToque>().alTocar = sel.TocarRueda;
        sel.marcaRueda = NuevaMarca(sel.rueda, 46f);

        // Barra de brillo (negro -> color) y muestra del color elegido
        float anchoMuestra = tamBarra.y * 1.2f;
        var tamSoloBarra = new Vector2(tamBarra.x - anchoMuestra - 20f, tamBarra.y);
        var posBarra = centroBarra - new Vector2((anchoMuestra + 20f) * 0.5f, 0f);
        sel.imgBarra = NuevaImagen(rt, "Barra Brillo", RadialMenuIcons.Degradado, Color.white, tamSoloBarra, posBarra);
        sel.imgBarra.preserveAspect = false;
        sel.barra = (RectTransform)sel.imgBarra.transform;
        sel.imgBarra.gameObject.AddComponent<ZonaToque>().alTocar = sel.TocarBarra;
        sel.marcaBarra = NuevaMarca(sel.barra, tamBarra.y * 0.9f);

        var posMuestra = centroBarra + new Vector2(tamBarra.x * 0.5f - anchoMuestra * 0.5f, 0f);
        sel.muestra = NuevaImagen(rt, "Muestra", RadialMenuIcons.Circulo, inicial, new Vector2(anchoMuestra, anchoMuestra), posMuestra);
        sel.muestra.raycastTarget = false;

        sel.SetColor(inicial, false);
        return sel;
    }

    public void SetColor(Color c, bool notificar)
    {
        Color.RGBToHSV(c, out h, out s, out v);
        Actualizar(notificar);
    }

    void TocarRueda(Vector2 local)
    {
        float radio = rueda.rect.width * 0.5f;
        Vector2 p = Vector2.ClampMagnitude(local, radio);
        h = Mathf.Repeat(Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI), 1f);
        s = p.magnitude / radio;
        if (v < 0.15f) v = 1f; // si estaba casi negro, que se note el cambio de tono
        Actualizar(true);
    }

    void TocarBarra(Vector2 local)
    {
        float ancho = barra.rect.width;
        v = Mathf.Clamp01(local.x / ancho + 0.5f);
        Actualizar(true);
    }

    void Actualizar(bool notificar)
    {
        float radio = rueda.rect.width * 0.5f;
        float a = h * 2f * Mathf.PI;
        marcaRueda.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * radio;
        marcaBarra.anchoredPosition = new Vector2((v - 0.5f) * barra.rect.width, 0f);
        imgBarra.color = Color.HSVToRGB(h, s, 1f);
        muestra.color = ColorActual;
        if (notificar) ColorCambiado?.Invoke(ColorActual);
    }

    static Image NuevaImagen(RectTransform padre, string nombre, Sprite sprite, Color color, Vector2 tam, Vector2 pos)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.sizeDelta = tam;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        return img;
    }

    // Aro blanco con borde oscuro: se ve sobre colores claros y oscuros.
    static RectTransform NuevaMarca(RectTransform padre, float tam)
    {
        var borde = NuevaImagen(padre, "Marca", RadialMenuIcons.Aro, new Color(0.05f, 0.05f, 0.05f), new Vector2(tam, tam), Vector2.zero);
        borde.raycastTarget = false;
        var aro = NuevaImagen((RectTransform)borde.transform, "Aro", RadialMenuIcons.Aro, Color.white, new Vector2(tam * 0.8f, tam * 0.8f), Vector2.zero);
        aro.raycastTarget = false;
        return (RectTransform)borde.transform;
    }

    /// <summary>Convierte clics/arrastres (mouse o rayo XR) en un punto local de la imagen.</summary>
    class ZonaToque : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public Action<Vector2> alTocar;

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnPointerDown(PointerEventData e) => Tocar(e);
        public void OnDrag(PointerEventData e) => Tocar(e);

        void Tocar(PointerEventData e)
        {
            // Solo mientras el rayo esta sobre esta imagen: se usa el punto 3D exacto.
            // Al arrastrar hacia afuera se mantiene el ultimo valor.
            var hit = e.pointerCurrentRaycast;
            if (!hit.isValid || hit.gameObject != gameObject) return;
            alTocar?.Invoke(((RectTransform)transform).InverseTransformPoint(hit.worldPosition));
        }
    }
}
