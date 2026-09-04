# Nivel 2 (Newspaper) y sistemas de UI

## Estado de Nivel 2 (relevado agosto/septiembre 2026)

`Assets/Scenes/Level2_Newspaper.unity`. Geometría base, decoración, edificio izquierdo
(con animator propio, `d5a027d`) y varias páginas del diario están integradas y
estables. **La página del museo está recién arrancada** (`2178e4a`, "pagina gallinas
remake + pag 2 museo start"): el modelo `KamiMuseo.fbx` ya está importado pero sin
evidencia todavía de quest/NPC/triggers wireados en esa página — es la pieza
claramente "en progreso" del nivel ahora mismo. Sistemas transversales (page-turn,
inventario, quests, cámara, Flap UI) están production-ready y no bloquean el trabajo
del museo.

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
página" al abrir/cerrar.

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
