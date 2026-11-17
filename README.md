# Recorrido de Propiedad con VR

Experiencia de realidad virtual para Meta Quest 3 que permite recorrer y explorar el modelo de un
conjunto de casas del curso de Interpretación de Planos.

## Instalar el APK en el Meta Quest 3

1. Activar el **modo desarrollador** del visor: en la app **Meta Horizon** del celular, ir a
   *Dispositivos → (el visor) → Configuración del visor → Modo desarrollador*.
2. Conectar el visor a la computadora con un cable USB-C. En el visor, aceptar
   **"Permitir depuración USB"**.
3. Instalar el APK con una de estas opciones:
   - **Meta Quest Developer Hub**: arrastrar `ProyectoVRCasa.apk` a la ventana del visor.
   - **Línea de comandos (adb)**:
     ```
     adb install -r ProyectoVRCasa.apk
     ```
4. En el visor, abrir **Biblioteca → Fuentes desconocidas** (Unknown Sources) y elegir
   **Proyecto VR Casa**.

## Controles

| Botón | Acción |
|---|---|
| **B** (control derecho) | Abrir y cerrar el menú radial |
| **A** (control derecho) | Saltar |
| **Gatillo** apuntando al menú | Elegir una opción |
| **Grip** (agarre) | Tomar herramientas del cinturón, agarrar puertas y ventanas, presionar interruptores |
| **Joysticks** | Moverse, girar y teletransportarse |

Desde el menú radial se cambia la hora del día (amanecer, día, atardecer y noche), se activa la
lluvia y se cambian las texturas y el color de las paredes.

## Abrir el proyecto en Unity

1. Instalar **Unity Hub** y el editor **Unity 6000.5.9f1** con el módulo **Android Build Support**
   (incluye OpenJDK y Android SDK & NDK).
2. Clonar el repositorio:
   ```
   git clone https://github.com/MoonFoxCake/Proyecto-VR-Unity.git
   ```
3. En Unity Hub: **Add → Add project from disk** y elegir la carpeta clonada. La primera vez tarda
   varios minutos en importar.
4. Abrir la escena **`Assets/Scenes/SampleScene.unity`**.

## Ejecutar el proyecto

- **Con el visor (Quest Link):** conectar el visor con cable o Air Link, activar Quest Link y darle
  **Play** en Unity.
- **Sin visor (simulador):** arrastrar a la escena el prefab
  `Assets/Samples/XR Interaction Toolkit/3.5.1/XR Device Simulator/XR Device Simulator.prefab`
  y darle **Play**. Se controla con teclado y mouse. Quitarlo antes de compilar el APK.

## Compilar el APK

1. **File → Build Profiles → Android → Switch Platform** (solo la primera vez).
2. Verificar que la escena `SampleScene` esté en la lista de escenas.
3. Con el visor conectado por USB, elegir **Build And Run**. Para solo generar el archivo, elegir
   **Build** y guardarlo como `ProyectoVRCasa.apk`.
