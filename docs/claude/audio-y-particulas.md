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
| 1 | PARTICLE_JUMP | KamiJumpParticles — `Create` en salto y aterrizaje |
| 2 | PARTICLE_REWARD | reward — `Enable` durante ReceivingReward |
| 3 | PARTICLE_SPLASH | SplashPasosKamiMojados — `Shoot` por paso mojado |
| 4 | PARTICLE_WIND | Particles_GetAffectedByWind — `Enable` con viento |
| 5 | PARTICLE_FOOTSTEP | KamiFootstepParticles — `Create` por paso seco |

- `Create(index, Vector3 worldPos)`: instancia detached (parent null, world) en esa posición exacta y destruye a los `timeToDestroy` (2s). El overload viejo `Create(index, Transform)` aplica `offset`; el de Vector3 NO.
- Las partículas de pies (salto/aterrizaje/pasos) se instancian en **`Player.FeetPosition`** (base de la cápsula del CharacterController). No usar `particleAnchor` para esto (sigue el hueso `Smoke`, queda a la altura del torso).
- El aterrizaje dispara su partícula al entrar a `Landing` desde `Falling` (en PlayerView). `BrieflySlowDown` ya NO instancia partículas (salía duplicada en hard falls).
- Pasos: cada `HandleFootstep` de Spine instancia y destruye un prefab — si algún día pesa, es candidato a pooling.
- `KamiFootstepParticles` = copia de KamiJumpParticles al 55% de tamaño, colores 60% hacia blanco, menos emisión.

## Brillitos de solapa

`BrillitosSolapa.prefab` (en `Assets/Prefabs/Particulas/`) = copia one-shot de "Brillitos Mágicos" (sin loop, burst de 30 en t=0, duración 0.7s). Instanciado como hijo en `Solapa.prefab` (escala local 0.125 porque el root escala ×80 con scalingMode Hierarchy). `Solapa.CambiarEstado()` le da `Play()`; si el campo serializado se pierde, `Awake` lo busca con `GetComponentInChildren`. **No tocar el "Brillitos Mágicos.prefab" original**: lo usan otras escenas.
