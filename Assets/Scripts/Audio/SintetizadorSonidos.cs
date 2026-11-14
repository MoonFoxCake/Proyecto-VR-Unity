using UnityEngine;

/// <summary>
/// Genera por codigo los sonidos de la escena (viento, pajaros, grillos, lluvia,
/// puertas, interruptores), para no depender de archivos de audio ni licencias.
/// Cualquiera se puede reemplazar por una grabacion real en SonidosEscena.
/// </summary>
public static class SintetizadorSonidos
{
    const int RateAmbiente = 22050; // loops largos: suficiente para viento/lluvia y pesa la mitad
    const int RateEfectos = 44100;

    // ------------------------------------------------------------------ ambientes (loops)

    /// <summary>Viento suave con rafagas lentas. Loop sin cortes.</summary>
    public static AudioClip Viento(float segundos = 12f, int semilla = 1)
    {
        var rnd = new System.Random(semilla);
        int n = (int)(segundos * RateAmbiente), extra = RateAmbiente; // 1 s para el cruce del loop
        var d = new float[n + extra];
        float lp1 = 0f, lp2 = 0f, marron = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / RateAmbiente;
            // Rafagas: senos con un numero entero de ciclos en el loop (vuelven a empezar igual).
            float rafaga = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * 1f * t / segundos)
                                 + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t / segundos + 1.3f);
            float blanco = (float)(rnd.NextDouble() * 2 - 1);
            marron = Mathf.Clamp(marron + blanco * 0.02f, -1f, 1f) * 0.998f;
            // Filtro que se abre con la rafaga: mas "silbido" cuando sopla fuerte.
            float corte = 0.02f + 0.06f * rafaga;
            lp1 += corte * (blanco - lp1);
            lp2 += corte * (lp1 - lp2);
            d[i] = (lp2 * 3f + marron * 0.6f) * rafaga;
        }
        return Clip("Viento", CruzarLoop(d, n, extra), RateAmbiente, 0.8f);
    }

    /// <summary>Lluvia: siseo constante con gotitas sueltas. Loop sin cortes.</summary>
    public static AudioClip Lluvia(float segundos = 8f, int semilla = 2)
    {
        var rnd = new System.Random(semilla);
        int n = (int)(segundos * RateAmbiente), extra = RateAmbiente / 2;
        var d = new float[n + extra];
        float lp = 0f, hpPrev = 0f, hp = 0f, gota = 0f, tono = 0f, fase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float blanco = (float)(rnd.NextDouble() * 2 - 1);
            lp += 0.35f * (blanco - lp);                 // quita lo mas agudo
            hp = 0.97f * (hp + lp - hpPrev); hpPrev = lp; // quita lo grave
            if (rnd.NextDouble() < 0.0025) { gota = 0.25f + (float)rnd.NextDouble() * 0.5f; tono = 2500f + (float)rnd.NextDouble() * 3000f; }
            fase += 2f * Mathf.PI * tono / RateAmbiente;
            float golpe = gota * Mathf.Sin(fase);
            gota *= 0.992f;
            d[i] = hp * 0.7f + golpe * 0.25f;
        }
        return Clip("Lluvia", CruzarLoop(d, n, extra), RateAmbiente, 0.8f);
    }

    /// <summary>Grillos: varios grillos con ritmos distintos. Loop sin cortes.</summary>
    public static AudioClip Grillos(float segundos = 6f)
    {
        int n = (int)(segundos * RateEfectos);
        var d = new float[n];
        // (frecuencia, repeticiones del canto en el loop, pulsos por canto, volumen, desfase)
        var grillos = new[] {
            (4600f, 10, 3, 1f, 0.00f),
            (4300f, 8, 4, 0.6f, 0.13f),
            (4900f, 12, 2, 0.45f, 0.31f),
        };
        foreach (var (f, reps, pulsos, vol, desfase) in grillos)
        {
            float periodo = segundos / reps;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / RateEfectos;
                float enCanto = Mathf.Repeat(t / periodo + desfase, 1f) * periodo; // segundos desde que empezo el canto
                float pulso = 1f / 32f;                                           // 32 pulsos por segundo
                int k = (int)(enCanto / pulso);
                if (k >= pulsos) continue;
                float enPulso = (enCanto - k * pulso) / pulso;                    // 0..1
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(enPulso / 0.7f));
                d[i] += vol * env * Mathf.Sin(2f * Mathf.PI * f * t) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 2f * t));
            }
        }
        return Clip("Grillos", d, RateEfectos, 0.6f);
    }

    // ------------------------------------------------------------------ pajaros (sueltos)

    /// <summary>Un canto corto de pajaro (varios trinos). Cada variante suena distinto.</summary>
    public static AudioClip Pajaro(int variante)
    {
        var rnd = new System.Random(100 + variante * 17);
        float R() => (float)rnd.NextDouble();

        int notas = 2 + rnd.Next(5);
        float baseF = 2600f + R() * 2200f;
        bool sube = R() < 0.5f;
        float durNota = 0.05f + R() * 0.1f;
        float pausa = 0.03f + R() * 0.08f;
        float largo = notas * (durNota + pausa) + 0.1f;
        var d = new float[(int)(largo * RateEfectos)];

        float t0 = 0.02f;
        for (int k = 0; k < notas; k++)
        {
            float f0 = baseF * (1f + (R() - 0.5f) * 0.25f);
            float f1 = f0 * (sube ? 1.35f + R() * 0.3f : 0.6f + R() * 0.2f);
            float vib = 30f + R() * 50f; // trino rapido
            float dur = durNota * (0.7f + R() * 0.6f);
            float fase = 0f;
            int a = (int)(t0 * RateEfectos), b = Mathf.Min(d.Length, a + (int)(dur * RateEfectos));
            for (int i = a; i < b; i++)
            {
                float u = (float)(i - a) / (b - a);
                float f = Mathf.Lerp(f0, f1, u * u) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * vib * u * dur));
                fase += 2f * Mathf.PI * f / RateEfectos;
                float env = Mathf.Sin(Mathf.PI * u);
                env *= env;
                d[i] += env * (Mathf.Sin(fase) + 0.12f * Mathf.Sin(2f * fase));
            }
            t0 += dur + pausa * (0.6f + R() * 0.8f);
        }
        return Clip("Pajaro " + variante, d, RateEfectos, 0.7f);
    }

    // ------------------------------------------------------------------ efectos

    /// <summary>Interruptor de luz: "tic" seco de plastico.</summary>
    public static AudioClip Interruptor()
    {
        var d = new float[(int)(0.12f * RateEfectos)];
        var rnd = new System.Random(7);
        Golpe(d, 0.000f, 1.0f, rnd, 2300f, 0.012f);
        Golpe(d, 0.028f, 0.45f, rnd, 3100f, 0.008f); // rebote del mecanismo
        return Clip("Interruptor", d, RateEfectos, 0.8f);
    }

    /// <summary>Puerta que se abre: se suelta el pestillo y la bisagra rechina suave.</summary>
    public static AudioClip PuertaAbrir()
    {
        var d = new float[(int)(0.9f * RateEfectos)];
        var rnd = new System.Random(11);
        Golpe(d, 0.0f, 0.7f, rnd, 1800f, 0.015f);   // pestillo
        Golpe(d, 0.012f, 0.4f, rnd, 2900f, 0.01f);
        Rechinido(d, 0.08f, 0.65f, 0.35f, rnd);       // bisagra
        return Clip("Puerta abrir", d, RateEfectos, 0.8f);
    }

    /// <summary>Puerta que se cierra: golpe sordo de madera + pestillo.</summary>
    public static AudioClip PuertaCerrar()
    {
        var d = new float[(int)(0.45f * RateEfectos)];
        var rnd = new System.Random(13);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / RateEfectos;
            float blanco = (float)(rnd.NextDouble() * 2 - 1);
            lp += 0.08f * (blanco - lp);
            float cuerpo = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 28f)
                         + 0.5f * Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 40f);
            d[i] += cuerpo + lp * 4f * Mathf.Exp(-t * 35f);
        }
        Golpe(d, 0.01f, 0.5f, rnd, 2000f, 0.012f); // pestillo
        return Clip("Puerta cerrar", d, RateEfectos, 0.9f);
    }

    // ------------------------------------------------------------------ piezas

    // Clic corto: chasquido de ruido + resonancia metalica/plastica que se apaga rapido.
    static void Golpe(float[] d, float inicio, float vol, System.Random rnd, float frec, float caida)
    {
        int a = (int)(inicio * RateEfectos);
        int largo = (int)(caida * 6f * RateEfectos);
        for (int i = 0; i < largo && a + i < d.Length; i++)
        {
            float t = (float)i / RateEfectos;
            float ruido = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t / 0.0015f);
            float tono = Mathf.Sin(2f * Mathf.PI * frec * t) * Mathf.Exp(-t / caida)
                       + 0.4f * Mathf.Sin(2f * Mathf.PI * frec * 1.7f * t) * Mathf.Exp(-t / (caida * 0.6f));
            d[a + i] += vol * (ruido * 0.8f + tono * 0.6f);
        }
    }

    // Rechinido: friccion "pegar-soltar" = pulsos cuyo ritmo sube y baja, pasados por una resonancia.
    static void Rechinido(float[] d, float inicio, float dur, float vol, System.Random rnd)
    {
        int a = (int)(inicio * RateEfectos), b = Mathf.Min(d.Length, a + (int)(dur * RateEfectos));
        float siguiente = 0f, y1 = 0f, y2 = 0f;
        float r = 0.995f, w = 2f * Mathf.PI * 850f / RateEfectos, c = 2f * r * Mathf.Cos(w);
        for (int i = a; i < b; i++)
        {
            float u = (float)(i - a) / (b - a);
            float ritmo = 70f + 90f * Mathf.Sin(Mathf.PI * u) + 25f * Mathf.Sin(2f * Mathf.PI * 3f * u);
            float x = 0f;
            siguiente -= 1f / RateEfectos;
            if (siguiente <= 0f)
            {
                x = 1f;
                siguiente = (1f / ritmo) * (0.9f + 0.2f * (float)rnd.NextDouble());
            }
            float y = x + c * y1 - r * r * y2;
            y2 = y1; y1 = y;
            float env = Mathf.Sin(Mathf.PI * u);
            d[i] += vol * y * 0.08f * env;
        }
    }

    // Mezcla el final extra con el principio para que el loop no haga "clic".
    static float[] CruzarLoop(float[] d, int n, int extra)
    {
        var o = new float[n];
        System.Array.Copy(d, o, n);
        for (int i = 0; i < extra; i++)
        {
            float k = (float)i / extra;
            o[i] = d[i] * k + d[n + i] * (1f - k);
        }
        return o;
    }

    static AudioClip Clip(string nombre, float[] d, int rate, float pico)
    {
        float max = 0.0001f;
        foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
        float g = pico / max;
        for (int i = 0; i < d.Length; i++) d[i] *= g;
        var clip = AudioClip.Create(nombre, d.Length, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
