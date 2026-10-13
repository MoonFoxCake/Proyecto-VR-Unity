using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Una opcion (boton) del menu radial.</summary>
[Serializable]
public class OpcionMenu
{
    public string texto = "Opcion";
    public IconoMenu icono = IconoMenu.Sol;
    [Tooltip("Opcional. Si se asigna, reemplaza al icono incluido.")]
    public Sprite iconoPersonalizado;
    [Tooltip("Opcional. Muestra esta textura dentro del boton (vista previa de un material).")]
    public Texture2D iconoTextura;
    [Tooltip("Nombre de otra pagina que se abre al elegir esta opcion. Vacio = ninguna.")]
    public string abrirPagina = "";
    [Tooltip("Cierra el menu despues de elegir (si no abre otra pagina).")]
    public bool cerrarAlElegir = false;
    public UnityEvent alElegir = new UnityEvent();
}

public enum TipoPagina { Opciones, SelectorColor }

[Serializable]
public class EventoColor : UnityEvent<Color> { }

/// <summary>Un circulo de opciones (o un selector de color). La primera pagina de la lista es la principal.</summary>
[Serializable]
public class PaginaMenu
{
    public string nombre = "Principal";
    [Tooltip("Opciones = ruedita de botones. Selector Color = rueda de color + barra de brillo.")]
    public TipoPagina tipo = TipoPagina.Opciones;
    public List<OpcionMenu> opciones = new List<OpcionMenu>();

    [Header("Solo para Selector Color")]
    public Color colorInicial = Color.white;
    [Tooltip("Se ejecuta cada vez que el usuario elige o arrastra un color.")]
    public EventoColor alCambiarColor = new EventoColor();
}

/// <summary>
/// Menu radial (ruedita) que aparece frente al usuario al apretar el boton A
/// del control derecho, y se cierra al apretarlo otra vez.
/// La interfaz se construye sola al iniciar: solo hay que llenar las paginas
/// y opciones en el Inspector. Cada opcion puede ejecutar acciones (Al Elegir)
/// y/o abrir otra pagina.
/// Metodos publicos para botones: Alternar(), Abrir(), Cerrar(), MostrarPagina(nombre).
/// </summary>
public class RadialMenu : MonoBehaviour
{
    [Header("Contenido")]
    public List<PaginaMenu> paginas = new List<PaginaMenu> { new PaginaMenu() };

    [Header("Abrir / cerrar")]
    [Tooltip("Boton que abre y cierra el menu. Por defecto: A del control derecho (y M en el teclado para probar).")]
    public InputActionProperty botonAbrir = new InputActionProperty(CrearAccionPorDefecto());
    [Tooltip("Vacio = Camera.main (la camara del XR Origin).")]
    public Transform cabeza;
    [Tooltip("Metros frente a la cabeza.")]
    public float distancia = 0.6f;
    [Tooltip("Metros por encima (+) o por debajo (-) de los ojos.")]
    public float altura = -0.1f;

    [Header("Aspecto")]
    [Tooltip("Diametro del menu en metros.")]
    public float diametro = 0.45f;
    public Color colorFondo = new Color(0.06f, 0.07f, 0.1f, 0.8f);
    public Color colorBoton = new Color(0.18f, 0.2f, 0.26f, 1f);
    public Color colorResalte = new Color(1f, 0.75f, 0.25f, 1f);
    public Color colorIcono = Color.white;

    [Header("Eventos")]
    public UnityEvent alAbrir = new UnityEvent();
    public UnityEvent alCerrar = new UnityEvent();

    public bool EstaAbierto => canvasGO != null && canvasGO.activeSelf;

    // Unidades internas del canvas (se escalan a "diametro" metros).
    const float TamCanvas = 1000f;
    const float TamBoton = 210f;
    const float RadioOpciones = 330f;

    GameObject canvasGO;
    TMP_Text titulo;
    readonly Dictionary<string, GameObject> contenedores = new Dictionary<string, GameObject>();
    readonly Stack<string> historial = new Stack<string>();
    string paginaActual;
    float animacion = 1f;

    static InputAction CrearAccionPorDefecto()
    {
        var accion = new InputAction("Abrir menu", InputActionType.Button);
        accion.AddBinding("<XRController>{RightHand}/primaryButton");
        accion.AddBinding("<Keyboard>/m");
        return accion;
    }

    void Awake()
    {
        Construir();
        canvasGO.SetActive(false);
    }

    void OnEnable()
    {
        var accion = botonAbrir.action;
        if (accion == null) return;
        accion.performed += OnBotonAbrir;
        accion.Enable();
    }

    void OnDisable()
    {
        var accion = botonAbrir.action;
        if (accion == null) return;
        accion.performed -= OnBotonAbrir;
        if (botonAbrir.reference == null) accion.Disable();
    }

    void OnBotonAbrir(InputAction.CallbackContext _) => Alternar();

    void Update()
    {
        // Pequena animacion de aparicion.
        if (animacion < 1f && canvasGO != null)
        {
            animacion = Mathf.Min(1f, animacion + Time.unscaledDeltaTime * 6f);
            float s = Mathf.SmoothStep(0.6f, 1f, animacion) * (diametro / TamCanvas);
            canvasGO.transform.localScale = Vector3.one * s;
        }
    }

    public void Alternar()
    {
        if (EstaAbierto) Cerrar();
        else Abrir();
    }

    public void Abrir()
    {
        ColocarFrenteAlUsuario();
        historial.Clear();
        if (paginas.Count > 0) MostrarPaginaInterna(paginas[0].nombre);
        canvasGO.SetActive(true);
        animacion = 0f;
        alAbrir.Invoke();
    }

    public void Cerrar()
    {
        if (!EstaAbierto) return;
        canvasGO.SetActive(false);
        alCerrar.Invoke();
    }

    /// <summary>Muestra otra pagina recordando la actual (para el boton Atras).</summary>
    public void MostrarPagina(string nombre)
    {
        if (!contenedores.ContainsKey(nombre))
        {
            Debug.LogWarning($"[RadialMenu] No existe la pagina '{nombre}'.", this);
            return;
        }
        if (paginaActual != null) historial.Push(paginaActual);
        MostrarPaginaInterna(nombre);
    }

    public void Atras()
    {
        if (historial.Count > 0) MostrarPaginaInterna(historial.Pop());
        else Cerrar();
    }

    void MostrarPaginaInterna(string nombre)
    {
        foreach (var kv in contenedores) kv.Value.SetActive(kv.Key == nombre);
        paginaActual = nombre;
        if (titulo != null) titulo.text = nombre;
    }

    void ColocarFrenteAlUsuario()
    {
        var head = cabeza != null ? cabeza : Camera.main != null ? Camera.main.transform : null;
        if (head == null) return;

        // Solo la direccion horizontal: el menu queda derecho aunque el usuario mire abajo.
        Vector3 frente = head.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f) frente = head.up;
        frente.y = 0f;
        frente.Normalize();

        canvasGO.transform.position = head.position + frente * distancia + Vector3.up * altura;
        canvasGO.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
    }

    // ------------------------------------------------------------------ construccion de la UI

    void Construir()
    {
        canvasGO = new GameObject("Canvas Menu Radial", typeof(RectTransform), typeof(Canvas),
            typeof(TrackedDeviceGraphicRaycaster));
        // Sin padre: asi no hereda escala/rotacion y se queda quieto donde se abrio.
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var crt = (RectTransform)canvasGO.transform;
        crt.sizeDelta = new Vector2(TamCanvas, TamCanvas);
        crt.localScale = Vector3.one * (diametro / TamCanvas);

        // Fondo circular (no bloquea los rayos fuera de los botones).
        var fondo = CrearImagen(crt, "Fondo", RadialMenuIcons.Circulo, colorFondo, TamCanvas * 0.98f);
        fondo.raycastTarget = false;

        titulo = CrearTexto(crt, "Titulo", "", 48f, new Vector2(0f, TamCanvas * 0.5f + 40f), new Vector2(TamCanvas, 80f));

        foreach (var pagina in paginas)
        {
            if (pagina == null || string.IsNullOrEmpty(pagina.nombre) || contenedores.ContainsKey(pagina.nombre))
            {
                Debug.LogWarning("[RadialMenu] Pagina sin nombre o con nombre repetido; se ignora.", this);
                continue;
            }
            var cont = new GameObject("Pagina " + pagina.nombre, typeof(RectTransform));
            var rt = (RectTransform)cont.transform;
            rt.SetParent(crt, false);
            rt.sizeDelta = Vector2.zero;
            contenedores[pagina.nombre] = cont;

            bool principal = pagina == paginas[0];

            if (pagina.tipo == TipoPagina.SelectorColor)
            {
                // Rueda arriba, barra de brillo abajo y boton Atras al pie (todo dentro del circulo).
                var pag = pagina;
                var selector = SelectorColorRadial.Crear(rt, new Vector2(0f, 90f), 520f,
                    new Vector2(0f, -255f), new Vector2(520f, 64f), pag.colorInicial);
                selector.ColorCambiado += c => pag.alCambiarColor.Invoke(c);
                CrearBoton(rt, "Atras", RadialMenuIcons.Get(IconoMenu.Atras), null,
                    new Vector2(0f, -385f), TamBoton * 0.55f, principal ? (UnityAction)Cerrar : Atras);
                continue;
            }

            int n = Mathf.Max(1, pagina.opciones.Count);
            for (int i = 0; i < pagina.opciones.Count; i++)
            {
                var op = pagina.opciones[i];
                float ang = (90f - i * 360f / n) * Mathf.Deg2Rad; // empieza arriba, sentido horario
                var pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * RadioOpciones;
                var icono = op.iconoPersonalizado != null ? op.iconoPersonalizado : RadialMenuIcons.Get(op.icono);
                CrearBoton(rt, op.texto, icono, op.texto, pos, TamBoton, () => Elegir(op), op.iconoTextura);
            }

            // Centro: Cerrar en la principal, Atras en las demas.
            CrearBoton(rt, principal ? "Cerrar" : "Atras",
                RadialMenuIcons.Get(principal ? IconoMenu.Cerrar : IconoMenu.Atras),
                null, Vector2.zero, TamBoton * 0.7f, principal ? (UnityAction)Cerrar : Atras);
        }
    }

    void Elegir(OpcionMenu op)
    {
        op.alElegir.Invoke();
        if (!string.IsNullOrEmpty(op.abrirPagina)) MostrarPagina(op.abrirPagina);
        else if (op.cerrarAlElegir) Cerrar();
    }

    void CrearBoton(RectTransform padre, string nombre, Sprite icono, string texto, Vector2 pos, float tam,
        UnityAction accion, Texture textura = null)
    {
        var img = CrearImagen(padre, "Boton " + nombre, RadialMenuIcons.Circulo, Color.white, tam);
        ((RectTransform)img.transform).anchoredPosition = pos;

        var boton = img.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.navigation = new Navigation { mode = Navigation.Mode.None };
        var colores = boton.colors;
        colores.normalColor = colorBoton;
        colores.highlightedColor = colorResalte;
        colores.pressedColor = Color.Lerp(colorResalte, Color.white, 0.4f);
        colores.selectedColor = colorBoton; // evita que quede "pegado" resaltado
        colores.fadeDuration = 0.08f;
        boton.colors = colores;
        boton.onClick.AddListener(accion);

        bool conTexto = !string.IsNullOrEmpty(texto);
        if (textura != null)
        {
            // Vista previa redonda de la textura (mascara circular).
            var mascara = CrearImagen((RectTransform)img.transform, "Mascara", RadialMenuIcons.Circulo, Color.white, tam * 0.5f);
            mascara.raycastTarget = false;
            ((RectTransform)mascara.transform).anchoredPosition = new Vector2(0f, conTexto ? tam * 0.1f : 0f);
            mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var raw = new GameObject("Textura", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rrt = (RectTransform)raw.transform;
            rrt.SetParent(mascara.transform, false);
            rrt.sizeDelta = new Vector2(tam * 0.5f, tam * 0.5f);
            raw.texture = textura;
            raw.uvRect = new Rect(0f, 0f, 0.5f, 0.5f); // un acercamiento, para que se vea el detalle
            raw.raycastTarget = false;
        }
        else if (icono != null)
        {
            var ic = CrearImagen((RectTransform)img.transform, "Icono", icono, colorIcono, tam * (conTexto ? 0.45f : 0.5f));
            ic.raycastTarget = false;
            ((RectTransform)ic.transform).anchoredPosition = new Vector2(0f, conTexto ? tam * 0.1f : 0f);
        }
        if (conTexto)
            CrearTexto((RectTransform)img.transform, "Texto", texto, tam * 0.13f,
                new Vector2(0f, icono != null ? -tam * 0.27f : 0f), new Vector2(tam * 0.9f, tam * 0.2f));
    }

    static Image CrearImagen(RectTransform padre, string nombre, Sprite sprite, Color color, float tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.sizeDelta = new Vector2(tam, tam);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        return img;
    }

    TMP_Text CrearTexto(RectTransform padre, string nombre, string texto, float tamFuente, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tamFuente;
        t.alignment = TextAlignmentOptions.Center;
        t.color = colorIcono;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    void OnDestroy()
    {
        if (canvasGO != null) Destroy(canvasGO);
    }
}
