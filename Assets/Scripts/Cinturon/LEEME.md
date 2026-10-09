# Cinturon de herramientas (VR)

Guia rapida para agregar ranuras, herramientas y menus sin escribir codigo.
Probado con XR Interaction Toolkit 3.5.1 y el XR Device Simulator.

## Antes de empezar
1. Abre la escena que tiene tu XR Origin.
2. Menu **Tools > VR > Crear cinturon con herramientas** (solo la primera vez).
   Crea el Cinturon, 2 ranuras y 2 herramientas de ejemplo (Medidor y Linterna).
3. Guarda la escena (Ctrl+S).

## Agregar una ranura
**Tools > VR > Cinturon > 1. Agregar ranura**
Se crea en una posicion libre alrededor de la cintura. Puedes moverla
cambiando su Position local (debe quedar a menos de ~30 cm de la cintura).

## Agregar una herramienta (con gatillo configurable)
1. Selecciona la ranura vacia en la Hierarchy (Slot_...).
2. **Tools > VR > Cinturon > 2. Agregar herramienta generica a la ranura seleccionada**
3. En la herramienta nueva, componente **Tool Events**:
   - On Activated: se ejecuta al apretar el gatillo con la herramienta en la mano.
   - On Grabbed / On Released: al agarrarla / soltarla.
   Arrastra un objeto al evento y elige el metodo que quieras ejecutar.
4. Cambia el cubo "Cuerpo" por tu modelo cuando quieras.

## Agregar un menu (tablet con botones)
1. Selecciona la ranura vacia.
2. **Tools > VR > Cinturon > 3. Agregar tablet de menu a la ranura seleccionada**
3. Agarra la tablet y aprieta el gatillo: aparece/desaparece el panel.
4. Los botones estan en Menu_Tablet > Panel. Configura cada uno en su **OnClick**
   (por ejemplo, un metodo publico de tu propio script).
5. La escena necesita un EventSystem con **XR UI Input Module**
   (GameObject > XR > UI Event System). Si falta, la consola avisa.

## Si algo no funciona
- La herramienta no se engancha: revisa que la ranura tenga la herramienta en
  *Starting Selected Interactable* y la herramienta tenga la ranura en *Home Socket*
  (el menu lo hace solo; si duplicaste una herramienta a mano, revisalo).
- No puedes agarrarla: falta XR Interaction Manager o el Near-Far Interactor.
- Los botones no responden: falta XR UI Input Module en el EventSystem.
- Desactiva el XR Device Simulator antes de compilar para el Quest.
