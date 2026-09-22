# Nivel 2 (Newspaper) y sistemas de UI

## Estado de Nivel 2 (relevado agosto/septiembre 2026)

`Assets/Scenes/Level2_Newspaper.unity`. Base geometry, decoration, the left building
(with its own animator, `d5a027d`), and several newspaper pages are integrated and
stable. Blocking for all 5 pages is in progress on the `pages-blocking` branch (pages
3 and 4 in progress as of 2026-09-22).

**The full 5-page narrative is now defined** (Diego, 2026-09-22): Kami meets Natalia,
they investigate a stolen painting (the Pelusa), get framed by Ariel, end up
arrested, escape with the Grandmother's help, and clear their name at the museum
(`KamiMuseo.fbx`, page 5) before catapulting back to their book. Full spec + phased
breakdown (what current code already covers, what's 100% new) in
`specs/006-nivel2-detective-natalia/spec.md` and `tasks.md`. This replaces the old
"museum page just started" description — the real scope is much larger than that one
page (ambient traffic, a police vision cone, new Origami routes, the Natalia NPC,
etc., all 100% new). Cross-cutting systems (page-turn, inventory, quests, camera,
Flap UI) are production-ready and get reused as the base.

**Pendiente conocido**: feedback visual de las botas de agua (TODO en
`LevelManager.cs`, línea ~121) — falta el skin de Spine correspondiente.

## LevelManager

Singleton dueño de los recursos del nivel: `Dictionary<ResourceType, int>` con 8 tipos
(hongos, flores, papel, botasAgua, botasRapidas, tijera, tijeraMejorada, abuela).
Guarda la referencia a `Player`, y los flags de control `agency` / `inDialogue` que
gatean input y diálogo (ver `paginas-y-hoja.md` para el uso de `inDialogue` durante el
page-turn). `AddResource()` dispara `Evento.OnResourceUpdated`, consumido por
`InventoryManager` y `QuestSlot`. Los métodos `GiveSprintBoots()`/`GiveWaterBoots()`/
`GiveTijeraMejorada()` son las recompensas de quest — ver `SetTijeraEquipment()` en
`spine-kami.md` para el caso tijera. También decide qué música de nivel usar
(MemoFloraMainLoop en Nivel 1, BohrenDestroyingAngels en Nivel 2) y expone un sistema
de cheats solo-editor gateado por flags.

**Verificado**: el flujo de page-turn documentado en `paginas-y-hoja.md` sigue
coincidiendo con el código actual (PageScrollerManager → PlayerPageSpawnManager →
Hoja/EdgeBone → RidingPage) — no se encontraron discrepancias.

## Inventario

- `InventoryItem` (ScriptableObject): nombre, sprite, color de "sticker", `ResourceType`.
- `InventoryManager` (singleton): diccionarios `ResourceType → InventoryItem` y
  `InventoryItem → int` (cantidad); reacciona a `Evento.OnResourceUpdated`.
- `InventorySlot`: sprite + color + nombre localizado (tabla `ItemTable`, con fallback
  al nombre del asset si la carga async todavía no resolvió) + cantidad si es &gt;1;
  sonidos de hover/click; fade in/out para el "sticker" de recompensa nueva. Los slots
  se reacomodan sin huecos cuando se quita un ítem.

## Flap UI (menú deslizable) y CamWheel (selector de cámara)

**FlapManager**: panel que entra deslizando (tecla ESC/I/U) con 4 tabs — Quests
(`QuestSlot[]`), Inventario (`InventorySlot[]`), Settings (sliders de
brillo/contraste/volumen + confirmación de salida) y Controles. Al abrir del todo
pausa el juego (`Time.timeScale = 0`) y baja la música a 0.4x; sonido de "vuelta de
página" al abrir/cerrar. Con joystick: R1/L1 ciclan de tab y B cierra (ver
`controles-y-gamepad.md`); el botón de la "tirita" (imagen `_tiritaPull`/`_tiritaPush`
que sobresale del costado) tiene `FlapManager.BTN_ToggleFlap()` cableado a su `OnClick` y
también es navegable/apretable con A. Los 3 sliders de Settings usan un handle animado
("gallina", `Chicken Handle.controller`) que camina tanto al arrastrar con mouse como al
enfocar con joystick (mismo clip en los estados Pressed/Selected del Animator). Arriba de
la columna de íconos de sección hay un cartel (`FlapTabHint`, componente
`SoloConJoystick`) que dice "L1 / R1 para cambiar de sección", visible solo con joystick.

**CamWheelManager** (implementa `IFlap`, mismo patrón de apertura/cierre que Flap):
menú radial para elegir `CameraMode` a mano. Botones indexados por el enum de cámara;
se resincroniza solo al cambiar de cámara por otro medio (`Evento.OnCameraChange` →
`FakeSelectButton()` resalta el botón activo).

**HoverDetector**: dispara `Evento.OnMouseEnterFlap`/`OnMouseExitFlap`, usado para
suprimir tooltips mientras el mouse está sobre UI.

## Cámara

`CameraManager`: array de Cinemachine VirtualCameras, una por `CameraMode`
(`CloseUp`, `OrigamiCasting`, `Normal`, `General`, `BookCenter`, `ReceiveReward`).
`SetCamera(modo)` apaga todas y prende la target; `ToggleNextCamera()` (click medio)
cicla. Durante un page-turn la secuencia es CloseUp → BookCenter (con delay) → Normal.
`SplashCamaraController` es un sistema aparte y más simple (dos cámaras que alternan
solas cada 5s) solo para la pantalla de splash/intro.
