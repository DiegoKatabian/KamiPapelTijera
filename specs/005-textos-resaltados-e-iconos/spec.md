# 005 — Palabras clave resaltadas e íconos animados de input

**Estado**: implementado (septiembre 2026), pendiente de validación jugando.
**Branch**: `feature/textos-resaltados-e-iconos` (sale de `feature/joystick-controls`).
**Pedido de Diego**: que en todos los textos del juego las palabras clave de mecánica estén
resaltadas (negrita + color + escala) con un código de colores didáctico, y que donde hoy
dice un input escrito (`E`, `SPACE`, `(A)`) se vea un ícono animado.

## Por qué se pudo hacer sin tocar 7 sistemas

Todo el texto localizado del juego ya pasaba por un único embudo:
`LocalizedText.Aplicar()` (`Assets/Scripts/UI/LocalizedText.cs`). Diálogos, tooltips/post-its,
quests, inventario, overlays y el tab Controles (vía el puente con `LocalizeStringEvent`)
terminan todos ahí. Enchufar dos pasos nuevos en ese punto alcanza para cubrir el juego
entero — **cero prefabs tocados, cero escenas tocadas**.

Los `.text =` directos que quedan fuera del embudo son todos contadores numéricos
(`InventorySlot`, `QuestSlot`, `PedestalCanvasDisplay`, los `*TextUpdater`): no tienen prosa
ni tokens de input, así que no necesitan pasar por acá.

## Las tres piezas nuevas

| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/UI/ResaltadorDeConceptos.cs` | Envuelve palabras clave en `<b><color=…>` (+ `<size>` en las principales). Tabla trilingüe es/en/pt sacada de las tablas de localización reales. |
| `Assets/Scripts/UI/IconosDeBoton.cs` | Construye por código un `TMP_SpriteAsset` completo (textura procedural incluida) con los íconos de botón/tecla, 4 frames cada uno. |
| `Assets/Scripts/UI/AnimadorDeIconos.cs` | Anima esos sprites reescribiendo las UV del quad en la malla ya generada. |

Las tres tienen un `Activo` / kill switch para apagarlas sin revertir código.

## Código de colores (didáctico, por tipo de acción)

| Categoría | Color | Escala | Agrupa |
|---|---|---|---|
| Corte | `#D6453D` rojo | sí | cortar, tijera, cut |
| Origami | `#2D7DD2` azul | sí | doblar, plegar, origami, pliegue, fold |
| Recurso | `#3E9B4F` verde | no | papel, flores, hongos, botas |
| Peligro | `#B5651D` ámbar oscuro | no | agua, río, ahogarse, mojarse, rocoso |
| Movimiento | `#8155BA` violeta | no | saltar, correr, trepar, pasar de página |

El ámbar es oscuro **a propósito**: los post-its del juego son amarillos y un ámbar claro no
contrastaría.

## Cómo se combinan sprites animados y texto (la parte no obvia)

Un `<sprite name="btn_a_0">` es, para TMP, un carácter más: ocupa un quad en la malla y cuenta
como 1 en `characterCount`. Animarlo tiene tres caminos y sólo uno es sano:

1. **Re-`SetText` por frame** — re-maqueta todo (word-wrap, conteo de caracteres) y le pelea al
   typewriter de los diálogos. Descartado.
2. **GameObjects encima del glifo** — un `Image`+`Animator` posicionado con los bounds del
   carácter. Más cómodo para el arte, pero hay que reposicionar en cada reflow y resolver la
   conversión de coordenadas para cada Canvas distinto (globo de diálogo, los 5 post-its, el
   Flap…). Descartado por costo y riesgo.
3. **Reescribir las UV del quad ya generado** ← **el elegido**. Es la API sancionada de TMP para
   efectos sobre glifos (lo mismo que hacen los ejemplos oficiales tipo VertexColorCycler, pero
   sobre UVs en vez de colores).

El animador, por tick (~6 fps, no por frame):
`textInfo.characterInfo[i]` → los que son `elementType == Sprite` → del nombre del sprite
(`btn_a_2`) saca base y frame → busca `btn_a_3` en el sprite asset → convierte su `glyphRect` a
UV normalizada → escribe `meshInfo[materialReferenceIndex].uvs0[vertexIndex + 0..3]` →
`UpdateVertexData(TMP_VertexDataUpdateFlags.Uv0)`.

**Invariante que sostiene todo**: los 4 frames de un ícono comparten métricas de glifo idénticas
y sólo difieren en el dibujo dentro de la celda. Si las métricas cambiaran por frame, el texto se
re-maquetaría en cada tick y bailaría la línea entera.

## Cambio de política: el teclado ahora también se traduce

Hasta esta tanda, `InputPromptSystem` tenía una regla de oro: los tokens legacy (`SHIFT`,
`ESPACIO`, `E`…) se traducían **sólo con joystick**, y con teclado el texto salía idéntico a las
tablas. Los íconos rompen esa regla **a propósito**: es justamente lo que convierte el `E`
escrito a mano en las tablas en una teclita dibujada.

La red de seguridad es `InputPromptSystem.UsarIconos = false`: en false, el sistema vuelve
exactamente al comportamiento viejo, teclado incluido. Y si el atlas no se pudo construir, cada
prompt cae solo al texto de siempre.

De paso se cerró un gap que estaba anotado en `docs/claude/controles-y-gamepad.md`:
`RxTeclaEnListaDeControles` pasó de `[EUI]` a `[EUIOM]`, así que las líneas "O - Abrir Controles"
y "M - Control de sonido" del tab Controles ya no quedan con la letra pelada.

## Orden de operaciones (y por qué es ése)

En `LocalizedText.Aplicar`:

1. `ResaltadorDeConceptos.Resaltar(crudo)` — **una sola vez**, y el resultado se guarda DENTRO
   del crudo registrado.
2. `_crudos[destino] = crudo`
3. `InputPromptSystem.Procesar(crudo)` — se recalcula entero en cada cambio de device.

**Por qué el resaltado va horneado y los prompts no**: el resaltado NO es idempotente. Los tags
`<b><color>` dejan la palabra intacta en el medio, así que volver a pasarle el resaltador a un
texto ya resaltado la envuelve de nuevo — y otra vez en cada cambio de device, anidando tags
hasta ensuciar todo. Los prompts sí se pueden recalcular porque siempre parten del mismo crudo
original, nunca de su propia salida.

**Por qué el resaltado va ANTES de los prompts**: si corriera después, las regex de
`InputPromptSystem` tendrían que matchear sobre texto ya lleno de tags.

## Reglas duras que evitan bugs concretos

1. **El resaltador nunca matchea dentro de `{...}`**. Un alias válido de placeholder es
   literalmente `{INPUT:cortar}`: resaltar esa palabra ahí adentro rompe el placeholder y el
   jugador ve `{INPUT:<b>…</b>}` crudo en pantalla.
2. **El resaltador nunca matchea dentro de `<...>`** (tags de TMP ya presentes en la tabla).
3. **El vocabulario del resaltador no puede pisar el de input** (E, U, I, O, M, WASD, click,
   shift, ctrl, esc, space, stick, A, B, L1, L2, Start): si el resaltador los envuelve primero,
   las regex de `InputPromptSystem` dejan de matchear.
4. **Los sufijos `_N` de los sprites no son cosméticos**: el animador encuentra los frames con
   ese patrón.
5. El animador **no cachea `textInfo` ni arrays** entre ticks (el typewriter regenera la malla
   seguido) y chequea rangos antes de escribir: un tick que agarra la malla en estado intermedio
   no hace nada, nunca tira excepción.

## Inspector-editable settings (2026-09-09, English section)

Colours, vocabulary and emphasis are design values, not code, so they live in
`Assets/Resources/TextHighlightSettings.asset` (`TextHighlightSettings.cs`). Colour pickers,
bold/enlarge toggles per category, and the word lists as plain comma-or-newline text.

- **No wiring**: loaded by name via `Resources.Load`, so it works on Play with nothing dragged
  into any scene. `[CreateAssetMenu]` is there for making more of them.
- **Cannot break the game**: if the asset is missing or its word table produces an invalid regex,
  the highlighter logs and falls back to a built-in copy of the same values.
- **Live editing during Play**: `OnValidate` → `ResaltadorDeConceptos.Invalidate()` →
  `OnSettingsChanged` → `LocalizedText.Refrescar()`, so text already on screen repaints while a
  dialogue is open.
- **Protected phrases are plain text, not regex** — they get escaped, and runs of whitespace
  become `[\s:,]+`, so a bad edit can't throw.

**This changed how `LocalizedText` stores text.** It now keeps the **pristine** localized string
and derives both highlighting and prompts from it on every write, instead of baking the
highlight in once. Deriving always from the original is what makes rewriting safe to repeat: the
danger was only ever feeding the highlighter its OWN output (the word survives in the middle of
the tags, so it would get wrapped again on each refresh). This is also what makes live colour
editing possible at all.

## Cutscenes: sin resaltado (2026-09-09, English section)

Keyword highlighting is switched OFF for any scene whose name contains "Cutscene"
(`Nivel1_EndCutscene` today, plus any future one — no per-scene wiring to forget). Those
dialogues are cinematic and colour-coded teaching words break the tone.

Implemented in `ResaltadorDeConceptos` via a `sceneLoaded` hook, using a flag **separate** from
the manual `Activo` kill switch so the scene rule never silently overwrites a hand-made choice.
The active scene is evaluated (not the newly loaded one), so an additive load keeps the base
level's behaviour.

Two things deliberately NOT covered, pending Diego's call:
- The **MainMenu intro dialogue** is also arguably cinematic, but was not part of the request.
- **Input icons stay ON** in cutscenes — only the keyword colouring is suppressed. The cutscene
  advances with the action button, so showing that button seems right; easy to change.

## Verificación

- `python tools/compile-check.py` — compila limpio (baseline conocido: `JumpFloodOutlineRenderer`
  CS0162, `HongueroTiburcioDialogueTrigger` CS0414).
- **Lo que falta y sólo se puede validar jugando**: que los íconos queden bien alineados con la
  línea base del texto, que la respiración se sienta sutil y no molesta, que los colores contrasten
  sobre los 5 post-its y el globo de diálogo, y que el typewriter revele los íconos sin parpadeos.

## Pendiente / futuro

- **Arte real de Valentino**: hoy los íconos son placeholder dibujado por código. Cuando llegue el
  arte, se reemplaza el dibujo dentro de `IconosDeBoton` (o se carga un `TMP_SpriteAsset`
  autorado) — el resto del sistema no se entera, siempre que se respete el patrón `<base>_<frame>`.
- Los 9 de 10 `OrigamiRoute *.prefab` que hardcodean el tooltip en español en vez de usar la clave
  `origami_guide` siguen igual (deuda técnica previa, ver issue #41.5).
