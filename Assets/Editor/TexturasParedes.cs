using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera 3 texturas de pared que se repiten sin costuras (PNG en Assets/Texturas/Paredes):
/// Ladrillo, Bloque (de concreto, 40x20 cm como los muros del modelo) y Madera.
/// De cada una se crea una version a color y otra en grises ("_Gris") que se usa
/// para tenirla con el color que elija el usuario.
/// Cada textura representa 1.2 x 1.2 m de pared.
/// Menu: Tools > VR > Paredes > Regenerar texturas de pared
/// Se pueden reemplazar por fotos reales: basta con asignar otras en Paredes Control.
/// </summary>
public static class TexturasParedes
{
    const string Carpeta = "Assets/Texturas/Paredes";
    const int N = 1024;
    public static readonly string[] Nombres = { "Ladrillo", "Bloque", "Madera" };

    [MenuItem("Tools/VR/Paredes/Regenerar texturas de pared")]
    static void Regenerar() => Generar(true, out _);

    /// <summary>Devuelve las 3 texturas a color (y en grises), creandolas si no existen.</summary>
    public static Texture2D[] Generar(bool forzar, out Texture2D[] grises)
    {
        Directory.CreateDirectory(Carpeta);
        var res = new Texture2D[Nombres.Length];
        grises = new Texture2D[Nombres.Length];
        for (int k = 0; k < Nombres.Length; k++)
        {
            string path = $"{Carpeta}/{Nombres[k]}.png";
            string pathGris = $"{Carpeta}/{Nombres[k]}_Gris.png";
            if (forzar || !File.Exists(path) || !File.Exists(pathGris))
            {
                var color = new Color32[N * N];
                var gris = new Color32[N * N];
                if (k == 0) Ladrillo(color, gris);
                else if (k == 1) Bloque(color, gris);
                else Madera(color, gris);
                Guardar(path, color);
                Guardar(pathGris, gris);
            }
            res[k] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            grises[k] = AssetDatabase.LoadAssetAtPath<Texture2D>(pathGris);
        }
        return res;
    }

    static void Guardar(string path, Color32[] px)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }

    static Color32 G(float g) { var b = (byte)(Mathf.Clamp01(g) * 255f); return new Color32(b, b, b, 255); }

    // ------------------------------------------------------------------ ruido que se repite

    static float Hash(int x, int y, int semilla)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + semilla * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    // Ruido de valor periodico: (u, v) en [0,1) con fx x fy celdas, se repite en los bordes.
    static float Ruido(float u, float v, int fx, int fy, int semilla)
    {
        float x = u * fx, y = v * fy;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float tx = x - x0, ty = y - y0;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        float a = Hash(Mod(x0, fx), Mod(y0, fy), semilla), b = Hash(Mod(x0 + 1, fx), Mod(y0, fy), semilla);
        float c = Hash(Mod(x0, fx), Mod(y0 + 1, fy), semilla), d = Hash(Mod(x0 + 1, fx), Mod(y0 + 1, fy), semilla);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float Fbm(float u, float v, int fx, int fy, int octavas, int semilla)
    {
        float suma = 0f, amp = 0.5f, total = 0f;
        for (int o = 0; o < octavas; o++)
        {
            suma += Ruido(u, v, fx << o, fy << o, semilla + o) * amp;
            total += amp;
            amp *= 0.5f;
        }
        return suma / total;
    }

    static Color32 C(Color c) => (Color32)c;

    // ------------------------------------------------------------------ texturas

    // 16 hiladas x 5 ladrillos por 1.2 m (ladrillo ~24 x 6.5 cm + junta de 1 cm).
    static void Ladrillo(Color32[] px, Color32[] gris)
    {
        var mortero = new Color(0.72f, 0.69f, 0.63f);
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            float u = (float)i / N, v = (float)j / N;
            float fy = v * 16f; int fila = Mathf.FloorToInt(fy); float lv = fy - fila;
            float fx = u * 5f + (fila % 2) * 0.5f; int col = Mod(Mathf.FloorToInt(fx), 5); float lu = fx - Mathf.Floor(fx);

            float junta = Mathf.Min(Mathf.Min(lv, 1f - lv) / 0.07f, Mathf.Min(lu, 1f - lu) / 0.022f);
            float grano = Fbm(u, v, 32, 32, 4, 11);
            float tono = Hash(col, fila, 3);
            var ladrillo = Color.Lerp(new Color(0.55f, 0.24f, 0.17f), new Color(0.72f, 0.38f, 0.26f), tono);
            ladrillo *= 0.82f + 0.3f * grano;
            ladrillo *= Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(junta - 1f)); // borde mas oscuro
            float ruidoMortero = Fbm(u, v, 64, 64, 3, 7);
            var m = mortero * (0.85f + 0.25f * ruidoMortero);
            float mezcla = Mathf.Clamp01((junta - 0.8f) * 4f);
            px[j * N + i] = C(Color.Lerp(m, ladrillo, mezcla));

            // Grises: ladrillo claro con variacion; junta casi blanca (queda clara al tenir).
            float gl = (0.72f + 0.16f * tono) * (0.85f + 0.25f * grano) * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(junta - 1f));
            float gm = 0.9f + 0.1f * ruidoMortero;
            gris[j * N + i] = G(Mathf.Lerp(gm, gl, mezcla));
        }
    }

    // 6 hiladas x 3 bloques por 1.2 m (bloque 40 x 20 cm, junta de 1 cm).
    static void Bloque(Color32[] px, Color32[] gris)
    {
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            float u = (float)i / N, v = (float)j / N;
            float fy = v * 6f; int fila = Mathf.FloorToInt(fy); float lv = fy - fila;
            float fx = u * 3f + (fila % 2) * 0.5f; int col = Mod(Mathf.FloorToInt(fx), 3); float lu = fx - Mathf.Floor(fx);

            float junta = Mathf.Min(Mathf.Min(lv, 1f - lv) / 0.025f, Mathf.Min(lu, 1f - lu) / 0.0125f);
            float poros = Ruido(u, v, 256, 256, 5);
            float manchas = Fbm(u, v, 8, 8, 4, 9);
            float tono = Hash(col, fila, 21);
            float g = 0.58f + 0.08f * tono + 0.12f * (manchas - 0.5f) - (poros > 0.82f ? 0.12f : 0f);
            var bloque = new Color(g, g * 0.99f, g * 0.96f);
            bloque *= Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(junta - 1f));
            var mortero = new Color(0.5f, 0.49f, 0.47f) * (0.9f + 0.15f * manchas);
            float mezcla = Mathf.Clamp01((junta - 0.8f) * 4f);
            px[j * N + i] = C(Color.Lerp(mortero, bloque, mezcla));

            // Grises: el bloque normalizado a ~0.85 y las juntas mas oscuras (sombra).
            float gb = g / 0.62f * 0.85f * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(junta - 1f));
            gris[j * N + i] = G(Mathf.Lerp(0.62f * (0.9f + 0.15f * manchas), gb, mezcla));
        }
    }

    // 8 tablas verticales de 15 cm por 1.2 m.
    static void Madera(Color32[] px, Color32[] gris)
    {
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            float u = (float)i / N, v = (float)j / N;
            float fx = u * 8f; int tabla = Mathf.FloorToInt(fx); float lu = fx - tabla;

            // Vetas: lineas casi verticales deformadas por ruido estirado a lo alto.
            float deform = Fbm(u, v, 16, 2, 3, 40 + tabla);
            float vetas = Mathf.Sin((lu * 9f + deform * 6f + Hash(tabla, 0, 8) * 10f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            vetas = Mathf.Pow(vetas, 3f);
            float fibra = Ruido(u, v, 512, 16, 13);
            float tono = Hash(tabla, 1, 17);
            var madera = Color.Lerp(new Color(0.5f, 0.31f, 0.17f), new Color(0.66f, 0.45f, 0.27f), tono);
            madera *= 1f - 0.18f * vetas - 0.06f * fibra;
            float borde = Mathf.Min(lu, 1f - lu) / 0.012f;
            float sombraBorde = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(borde));
            madera *= sombraBorde;
            px[j * N + i] = C(madera);

            // Grises: vetas y bordes, con brillo base alto para que el tinte se vea vivo.
            float gm = (0.8f + 0.15f * tono) * (1f - 0.22f * vetas - 0.06f * fibra) * sombraBorde;
            gris[j * N + i] = G(gm);
        }
    }
}
