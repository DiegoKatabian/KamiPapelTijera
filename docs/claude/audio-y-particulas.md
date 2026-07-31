# Audio y partículas

## AudioManager

Singleton (`AudioManager.instance`), vive en `Assets/Prefabs/AudioManager.prefab`. En `Awake` arma un diccionario **nombre-de-GameObject-hijo → AudioSource**: para agregar un sonido nuevo se agrega un GO hijo con AudioSource al prefab, y el string de `PlayByName` debe matchear el nombre exacto del GO.

- `PlayByName(name)` / `PlayByName(name, pitch)` / `PlayByName(name, centralPitch, variation)` (pitch random ±variation)
- `PlayRandom(params names)` — elige uno al azar (así se hacen los pasos: `Pasos_Kami_01..04`)

## TijeraHitbox y TijeraMiss

`TijeraHitbox` (trigger que se activa/desactiva por ataque): el flag `missed` arranca en `true` en `OnEnable` ("asumir miss hasta que se demuestre lo contrario") y solo pasa a `false` al cortar algo `ICortable`. En `OnDisable`, si sigue `true`, suena `TijeraMiss`. Tocar paredes/suelo NO afecta el flag — no volver a la semántica vieja (seteaba el flag en OnTriggerEnter y fallaba al errarle al aire).

## ParticleShooter (en Kami.prefab)

Array `particleSystemGameObject` por índice (constantes `PARTICLE_*` en PlayerView — usar esas, no números):

| # | Constante | Prefab / uso |
|---|-----------|--------------|
| 0 | PARTICLE_SPRINT | KamiSprintParticles — `Enable` on/off con el sprint |
| 1 | PARTICLE_JUMP | KamiJumpParticles — `Shoot` en salto y aterrizaje |
| 2 | PARTICLE_REWARD | reward — `Enable` durante ReceivingReward |
| 3 | PARTICLE_SPLASH | SplashPasosKamiMojados — `Shoot` por paso mojado |
| 4 | PARTICLE_WIND | Particles_GetAffectedByWind — `Enable` con viento |
| 5 | PARTICLE_FOOTSTEP | KamiFootstepParticles — `Shoot` por paso seco |
| 6 | PARTICLE_RUNSTOP | KamiRunstopParticles — `Shoot` en frenada en seco |

**Arquitectura actual de las partículas de pies (jump/footstep/runstop):** viven como HIJOS del GO
"Footstep Anchor" dentro de Kami.prefab (instancias anidadas de sus prefabs) y se disparan con
`Shoot()` vía `PlayerView.ShootFootAnchorParticles(index)`, que además gira el anchor 180° según el
facing. NO se instancian más (el `Create` detached quedó en el código pero sin callers). Requisitos
para que esto funcione, en los PREFABS de `Assets/Prefabs/Particulas/`:
- `looping: 0`, `playOnAwake: 0` (one-shot que se dispara con Play).
- **`moveWithTransform: 0`** = Simulation Space **World**: las partículas emitidas quedan en el
  mundo y NO siguen a Kami. Este campo YAML es un bool legacy: `1` = "las partículas se mueven con
  el transform" (**Local**), `0` = **World**. OJO: el nombre confunde y ya causó un bug — la sesión
  del 30/7 los dejó en 1 creyendo que era World, y las nubecitas de cada paso/salto viajaban
  pegadas a los pies apilándose unas sobre otras (partículas "arrastradas" por el emisor). No es
  una fuga de instancias: son las mismas partículas simuladas en el espacio equivocado.
- El aterrizaje dispara su partícula al entrar a `Landing` desde `Falling` (en PlayerView).
  `BrieflySlowDown` ya NO dispara partículas (salía duplicada en hard falls).
- `KamiFootstepParticles` = copia de KamiJumpParticles al 55% de tamaño, colores 60% hacia blanco,
  menos emisión. `KamiRunstopParticles` empuja hacia adelante (los otros dos hacia atrás).
- El GUID de KamiRunstopParticles (`a1b2c3d4e5f6...`) parece trucho pero es real: una sesión lo
  inventó y escribió el .meta consistente. Funciona; no "corregirlo".

**Glosario anti-bug de partículas** (los tres modos de fallar que ya nos pasaron):
1. *Simulation space Local sin querer* → partículas que siguen al emisor y se apilan (el bug de
   arriba). Fix: `moveWithTransform: 0`.
2. *Fuga de instancias* (object leak / unbounded spawning) → `Instantiate` por evento sin `Destroy`
   ni pooling: clones que se acumulan en la jerarquía a lo largo del juego. El patrón `Create` de
   ParticleShooter lo evita destruyendo a los `timeToDestroy` (2s).
3. *Emisión que nunca para* → sistema `looping: 1` al que le dan `Play()` y nadie `Stop()`. Fix
   tipo TijeraHitbox: `Stop()` en OnDisable ("sin esto se acumulaban ataque tras ataque").

## Brillitos de solapa

`BrillitosSolapa.prefab` (en `Assets/Prefabs/Particulas/`) = copia one-shot de "Brillitos Mágicos" (sin loop, burst de 30 en t=0, duración 0.7s). Instanciado como hijo en `Solapa.prefab` (escala local 0.125 porque el root escala ×80 con scalingMode Hierarchy). `Solapa.CambiarEstado()` le da `Play()`; si el campo serializado se pierde, `Awake` lo busca con `GetComponentInChildren`. **No tocar el "Brillitos Mágicos.prefab" original**: lo usan otras escenas.
