# Tutorial: base de cinemática con QTE en Unity

Esta base no construye la escena automáticamente. La intención es que el grupo pueda crear el animatic, reemplazar recursos y cambiar el orden de los QTE sin editar demasiadas líneas de código.

Los únicos scripts son:

- `QTEManager.cs`: controla los QTE, la interfaz y las pausas de Timeline.
- `SceneController.cs`: reinicia, cambia o cierra escenas desde botones.

## 1. Preparar la escena

1. Abre `Assets/Scenes/SampleScene.unity`.
2. Guárdala con `File > Save As` usando un nombre como `QTEPrototype`.
3. Crea estos objetos vacíos en la jerarquía:

```text
QTEPrototype
├── Main Camera
├── Directional Light
├── Environment
├── Characters
├── CinematicTimeline
├── GameManager
└── Canvas
```

`Environment` y `Characters` son carpetas visuales para mantener ordenados los objetos de arte. No necesitan scripts.

## 2. Crear la interfaz

Dentro de `Canvas`, crea esta jerarquía:

```text
Canvas
├── QTEPanel
│   ├── TitleText
│   ├── InstructionText
│   ├── SequenceText
│   ├── ProgressBackground
│   │   └── ProgressBar
│   └── TimerBackground
│       └── TimerBar
└── GameOverPanel
    ├── GameOverText
    └── RetryButton
```

Pasos:

1. Crea el Canvas con `GameObject > UI > Canvas`.
2. En `Canvas Scaler`, selecciona `Scale With Screen Size` y usa `1920 x 1080`.
3. Crea `QTEPanel` y `GameOverPanel` con `UI > Panel`.
4. Los tres textos deben ser `UI > Legacy > Text`, porque el script usa `UnityEngine.UI.Text`.
5. `ProgressBar` y `TimerBar` deben ser objetos `Image`.
6. En ambos `Image`, selecciona `Image Type: Filled`, `Fill Method: Horizontal` y `Fill Origin: Left`.
7. Deja `QTEPanel` y `GameOverPanel` activos en el Editor. El script los ocultará al iniciar.
8. En el botón `RetryButton`, añade un evento `On Click`, arrastra `GameManager` y selecciona `QTEManager > RetryQTE`.

El diseño, colores y tipografías quedan completamente en manos del grupo. El código sólo cambia textos y barras.

## 3. Configurar QTEManager

1. Selecciona `GameManager`.
2. Pulsa `Add Component` y añade `QTEManager`.
3. Arrastra cada elemento del Canvas a su campo correspondiente:

| Campo del script | Objeto de la escena |
|---|---|
| Qte Panel | `Canvas/QTEPanel` |
| Game Over Panel | `Canvas/GameOverPanel` |
| Title Text | `QTEPanel/TitleText` |
| Instruction Text | `QTEPanel/InstructionText` |
| Sequence Text | `QTEPanel/SequenceText` |
| Progress Bar | `QTEPanel/ProgressBackground/ProgressBar` |
| Timer Bar | `QTEPanel/TimerBackground/TimerBar` |

4. En la lista `Qtes`, cambia `Size` a `3`.
5. Usa esta configuración inicial:

| Índice | Title | Type | Time Limit | Required Amount |
|---:|---|---|---:|---:|
| 0 | Control de respiración | Hold Buttons | 6.5 | 3.5 |
| 1 | Impulsos de pánico | Button Sequence | 6 | 5 |
| 2 | Forcejeo desesperado | Rotate Stick | 7 | 3 |

`Required Amount` significa algo distinto según el tipo:

- `Hold Buttons`: segundos que se deben mantener los botones.
- `Button Sequence`: cantidad de botones correctos.
- `Rotate Stick`: cantidad de vueltas completas.

Los eventos `On Success` y `On Failure` de cada elemento son opcionales. Sirven, por ejemplo, para reproducir un sonido, activar una animación o cambiar una luz.

## 4. Crear la cinemática con Timeline

1. Selecciona `CinematicTimeline`.
2. Abre `Window > Sequencing > Timeline`.
3. Pulsa `Create` y guarda el Timeline en una carpeta como `Assets/Timeline`.
4. Añade pistas según lo que tenga el grupo:

   - `Animation Track` para personajes u objetos.
   - `Activation Track` para mostrar u ocultar dibujos del animatic.
   - `Audio Track` para ambiente, voces y efectos.
   - `Cinemachine Track` si más adelante usan Cinemachine.
   - `Signal Track` para iniciar cada QTE.

5. Arrastra el componente `Playable Director` de `CinematicTimeline` al campo `Timeline` de `QTEManager`.

Una propuesta sencilla de tiempos es:

```text
00–08 s  Cláudio duerme y despierta asfixiado
08 s     QTE 0: respiración
12–18 s  aparición de la Pisadeira
18 s     QTE 1: secuencia de botones
21–27 s  el cuerpo comienza a responder
27 s     QTE 2: rotación
31–38 s  desaparición, diálogo y ventana abierta
```

El Timeline se detiene mientras el jugador realiza cada QTE, por lo que estos tiempos no incluyen el tiempo de interacción.

## 5. Conectar señales de Timeline

Para cada QTE:

1. Añade un `Signal Receiver` a `GameManager`.
2. En la pista `Signal Track`, crea un `Signal Emitter` en el segundo correspondiente.
3. Crea o asigna un `Signal Asset`, por ejemplo:

   - `StartBreathingQTE`
   - `StartPanicQTE`
   - `StartStruggleQTE`

4. En `Signal Receiver`, crea la reacción para ese Signal Asset.
5. Arrastra `GameManager` al evento de la reacción.
6. Selecciona `QTEManager > StartQTE (int)` e introduce el índice:

   - Respiración: `0`
   - Pánico: `1`
   - Forcejeo: `2`

Cuando Timeline alcance la señal, `QTEManager` hará lo siguiente:

```text
Signal de Timeline
        ↓
StartQTE(índice)
        ↓
Timeline se pausa y aparece QTEPanel
        ↓
Éxito ─────────────── Fallo
  ↓                      ↓
Timeline continúa     GameOverPanel
                         ↓
                     RetryQTE()
```

## 6. Controles incluidos

| Mecánica | Mando | Teclado |
|---|---|---|
| Mantener | L2 + R2 | Q + E |
| Secuencia | A / B / X / Y | S / D / A / W o flechas |
| Rotación | Cualquier análogo | Recorrer W-D-S-A en círculo |

El proyecto ya incluye el paquete `Input System`. No es necesario crear un archivo nuevo de Input Actions para esta base, porque `QTEManager` consulta directamente el teclado y el mando.

## 7. Probar antes de producir arte final

Primero construyan un animatic con elementos muy simples:

- Cubos o imágenes estáticas para los personajes.
- Una cámara por plano o movimientos básicos.
- Audios temporales claramente identificados.
- Subtítulos provisionales.

En cada prueba revisen:

1. ¿Se entiende qué botón hay que pulsar?
2. ¿Se ve cuánto progreso falta?
3. ¿Se distingue claramente el éxito del fallo?
4. ¿El QTE corresponde a lo que ocurre en la escena?
5. ¿La ejecución perfecta queda entre 30 y 45 segundos?

No esperen a tener arte final para probar las interacciones.

## 8. Reparto de trabajo sugerido

Como los recursos están separados de la lógica, el grupo puede trabajar así:

- Una persona mantiene Timeline, cámaras y montaje.
- Una persona trabaja personajes, animación o ilustraciones.
- Una persona trabaja interfaz y feedback de los QTE.
- Una persona trabaja voces, efectos, música y mezcla.

La persona que integre el proyecto sólo debe comprobar que no se pierdan las referencias de `QTEManager` en el Inspector.

## 9. Añadir otro QTE

1. Aumenta `Qtes > Size` en el Inspector.
2. Configura el nuevo elemento.
3. Añade otra señal al Timeline.
4. Conecta la señal con `StartQTE` usando el nuevo índice.

No es necesario duplicar ni modificar el script para añadir más QTE de los tres tipos existentes.
