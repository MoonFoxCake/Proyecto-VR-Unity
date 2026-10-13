using System.Collections.Generic;
using UnityEngine;

/// <summary>Iconos incluidos que el menu radial dibuja solo (no hacen falta imagenes).</summary>
public enum IconoMenu { Ninguno, Sol, Luna, Amanecer, Atardecer, Reloj, Atras, Cerrar, Lluvia }

/// <summary>
/// Genera en tiempo de ejecucion los sprites del menu radial: el circulo de
/// fondo de cada boton y los iconos simples (sol, luna, etc.).
/// </summary>
public static class RadialMenuIcons
{
    const int Size = 128;
    static readonly Dictionary<IconoMenu, Sprite> cache = new Dictionary<IconoMenu, Sprite>();
    static Sprite circulo;

    public static Sprite Circulo => circulo != null ? circulo : circulo = Dibujar((x, y) => Disco(x, y, 0f, 0f, 0.98f));

    public static Sprite Get(IconoMenu icono)
    {
        if (icono == IconoMenu.Ninguno) return null;
        if (cache.TryGetValue(icono, out var s) && s != null) return s;
        s = Dibujar(Forma(icono));
        cache[icono] = s;
        return s;
    }

    // Cada forma devuelve la opacidad (0..1) en el punto (x, y), con x, y en [-1, 1].
    static System.Func<float, float, float> Forma(IconoMenu icono)
    {
        switch (icono)
        {
            case IconoMenu.Sol:
                return (x, y) => Mathf.Max(Disco(x, y, 0f, 0f, 0.42f), Rayos(x, y, 8, 0.58f, 0.92f, 0.09f));
            case IconoMenu.Luna:
                return (x, y) => Mathf.Clamp01(Disco(x, y, 0f, 0f, 0.75f) - Disco(x, y, 0.38f, 0.25f, 0.62f));
            case IconoMenu.Amanecer:
            case IconoMenu.Atardecer:
                return (x, y) =>
                {
                    float sol = y > -0.2f ? Disco(x, y, 0f, -0.2f, 0.45f) : 0f;
                    float rayos = y > -0.1f ? Rayos(x, y + 0.2f, 7, 0.6f, 0.9f, 0.08f) : 0f;
                    float linea = Barra(x, y, -0.95f, -0.38f, 0.95f, -0.38f, 0.07f);
                    float flecha = icono == IconoMenu.Amanecer
                        ? Mathf.Max(Barra(x, y, 0f, -0.95f, 0f, -0.55f, 0.06f), Barra(x, y, -0.15f, -0.7f, 0f, -0.55f, 0.06f), Barra(x, y, 0.15f, -0.7f, 0f, -0.55f, 0.06f))
                        : Mathf.Max(Barra(x, y, 0f, -0.95f, 0f, -0.55f, 0.06f), Barra(x, y, -0.15f, -0.8f, 0f, -0.95f, 0.06f), Barra(x, y, 0.15f, -0.8f, 0f, -0.95f, 0.06f));
                    return Mathf.Max(sol, rayos, linea, flecha);
                };
            case IconoMenu.Reloj:
                return (x, y) => Mathf.Max(Anillo(x, y, 0.85f, 0.1f),
                    Barra(x, y, 0f, 0f, 0f, 0.55f, 0.07f), Barra(x, y, 0f, 0f, 0.4f, -0.15f, 0.07f));
            case IconoMenu.Atras:
                return (x, y) => Mathf.Max(Barra(x, y, -0.6f, 0f, 0.7f, 0f, 0.09f),
                    Barra(x, y, -0.6f, 0f, -0.15f, 0.45f, 0.09f), Barra(x, y, -0.6f, 0f, -0.15f, -0.45f, 0.09f));
            case IconoMenu.Cerrar:
                return (x, y) => Mathf.Max(Barra(x, y, -0.55f, -0.55f, 0.55f, 0.55f, 0.09f),
                    Barra(x, y, -0.55f, 0.55f, 0.55f, -0.55f, 0.09f));
            case IconoMenu.Lluvia:
                return (x, y) => Mathf.Max(
                    Mathf.Max(Disco(x, y, -0.35f, 0.25f, 0.3f), Disco(x, y, 0.05f, 0.42f, 0.4f),
                        Disco(x, y, 0.42f, 0.22f, 0.3f), Barra(x, y, -0.35f, 0.08f, 0.42f, 0.08f, 0.13f)),
                    Mathf.Max(Barra(x, y, -0.35f, -0.25f, -0.47f, -0.55f, 0.06f),
                        Barra(x, y, 0.05f, -0.25f, -0.07f, -0.55f, 0.06f),
                        Barra(x, y, 0.45f, -0.25f, 0.33f, -0.55f, 0.06f),
                        Barra(x, y, -0.15f, -0.65f, -0.25f, -0.9f, 0.06f),
                        Barra(x, y, 0.25f, -0.65f, 0.15f, -0.9f, 0.06f)));
            default:
                return (x, y) => 0f;
        }
    }

    static Sprite Dibujar(System.Func<float, float, float> forma)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[Size * Size];
        for (int j = 0; j < Size; j++)
        for (int i = 0; i < Size; i++)
        {
            float x = (i + 0.5f) / Size * 2f - 1f;
            float y = (j + 0.5f) / Size * 2f - 1f;
            px[j * Size + i] = new Color(1f, 1f, 1f, Mathf.Clamp01(forma(x, y)));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    // Bordes suavizados de ~1.5 px para que no se vea pixelado en VR.
    const float Aa = 3f / Size;

    static float Disco(float x, float y, float cx, float cy, float r)
    {
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        return Mathf.Clamp01((r - d) / Aa + 0.5f);
    }

    static float Anillo(float x, float y, float r, float grosor)
    {
        float d = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - r);
        return Mathf.Clamp01((grosor * 0.5f - d) / Aa + 0.5f);
    }

    static float Barra(float x, float y, float ax, float ay, float bx, float by, float grosor)
    {
        Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        float d = Vector2.Distance(p, a + ab * t);
        return Mathf.Clamp01((grosor - d) / Aa + 0.5f);
    }

    static float Rayos(float x, float y, int n, float r0, float r1, float grosor)
    {
        float v = 0f;
        for (int k = 0; k < n; k++)
        {
            float a = k * Mathf.PI * 2f / n;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            v = Mathf.Max(v, Barra(x, y, c * r0, s * r0, c * r1, s * r1, grosor));
        }
        return v;
    }
}
