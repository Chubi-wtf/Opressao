# Pipeline de arte para las cinemáticas QTE

Este documento permite trabajar con material provisional y sustituirlo por arte final sin reconstruir el juego.

## Estados del contenido

- `Assets/Art/Placeholder`: dibujos provisionales usados para montar planos y tiempos.
- `Assets/Art/Final`: ilustraciones aprobadas para la entrega.
- `Assets/Audio/Placeholder`: voces, música y efectos temporales.
- `Assets/Audio/Final`: audio mezclado y aprobado.
- `Assets/Videos/Placeholder`: animatics y pruebas de movimiento.
- `Assets/Videos/Final`: videos definitivos listos para el juego.

No se debe reemplazar un archivo cambiando su identificador de plano. El código del plano permanece estable y solamente cambia su versión.

## Convención de nombres

Formato general:

`ESC##_SH###_DESCRIPCION_ESTADO.ext`

Ejemplos:

- `ESC01_SH010_PASILLO_placeholder.png`
- `ESC01_SH010_PASILLO_final.png`
- `ESC01_SH020_PERSONAJE_A_final.png`
- `ESC01_SH020_FONDO_final.png`
- `ESC01_SH030_ANIMATIC_placeholder.mp4`

Los planos avanzan de diez en diez (`SH010`, `SH020`, `SH030`) para poder insertar planos intermedios sin renombrar todo.

## Especificaciones para ilustraciones

- Resolución base: 1920 x 1080.
- Proporción: 16:9 horizontal.
- Formato: PNG.
- Espacio de color: sRGB.
- Transparencia: solo para personajes, efectos o capas que deban moverse por separado.
- Mantener personajes, fondo y efectos en archivos distintos cuando se necesite parallax o animación.
- Evitar texto dibujado dentro de la imagen; los subtítulos y mensajes se crean en Unity.

## Especificaciones para video

- Contenedor: MP4.
- Video: H.264.
- Audio: AAC, 48 kHz.
- Resolución recomendada: 1920 x 1080.
- Fotogramas por segundo: 30 o 60, pero iguales durante toda la secuencia.
- Evitar velocidad de fotogramas variable.

## Ficha mínima por plano

| Campo | Ejemplo |
|---|---|
| Código | ESC01_SH010 |
| Descripción | Personaje entra al pasillo |
| Duración provisional | 4.0 s |
| Capas | Fondo + personaje + sombra |
| Movimiento | Zoom lento 100–110 % |
| QTE | Ninguno |
| Estado | Boceto / revisión / aprobado |
| Responsable | Nombre |

## Flujo de aprobación

1. Guion y orden de planos.
2. Storyboard con códigos definitivos.
3. Animatic provisional en Timeline.
4. Aprobación de tiempos y ubicación de QTE.
5. Entrega de ilustraciones por capas.
6. Sustitución del placeholder conservando el código del plano.
7. Revisión de encuadre, color, audio y legibilidad.
8. Aprobación final y bloqueo del plano.

## Reglas para los QTE

- Cada QTE se inicia con un Signal de Timeline.
- El Signal se coloca en el acontecimiento narrativo, no en un segundo elegido al azar.
- Timeline y el video deben permanecer pausados mientras el QTE está activo.
- El clip posterior al QTE debe tener un pequeño margen inicial para evitar cortes visuales.
- Se prueban éxito, fracaso y reintento antes de sustituir el arte provisional.

Durante las pruebas dentro del Editor, `F10` o `9` completa el QTE activo si la opción
`Debug Qte Flow` está marcada en `QTEManager`. Este atajo no se compila en la
versión final del juego.

## Lista de control antes de integrar arte final

- El identificador del plano coincide con el documento de entrega.
- La resolución y proporción son correctas.
- No hay bordes transparentes accidentales.
- Los elementos importantes están dentro del área segura.
- El QTE no tapa información narrativa necesaria.
- La imagen o video se desactiva al terminar su clip.
- El audio no continúa mientras Timeline está pausado.
