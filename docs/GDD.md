# GDD — Kami: Papel y Tijera

**Documento de diseño maestro ("la biblia")**
Estudio: kimmiarts (Diego Katabian, Valentino)
Engine: Unity (URP) · Plataforma: PC (Steam) · Animación de personajes: Spine (spine-unity 3.8)
Estado: **living document** — vive junto al código, se actualiza a medida que el juego cambia. Donde el diseño y el código difieran, gana el código (y este doc se corrige).

> Convención: cada sección marca su nivel de certeza. 🟢 = implementado y estable. 🟡 = implementado parcialmente / en curso. 🔴 = diseñado pero no implementado. ⚪ = a definir (TBD), no inventar contenido aquí sin decisión del equipo.

---

## 0. Control de versión

| Versión | Fecha | Cambios |
|---|---|---|
| 0.1 | 2026-09-04 | Primera redacción completa a partir de auditoría de código, `ROADMAP.md`, specs en `specs/` y documentación técnica en `docs/claude/`. |
| 0.2 | 2026-09-04 | Lore final incorporado: guion completo del Acto 1 (texto en español, canónico), outline página-por-página del Nivel 2 ("Kami, Tinta y Castigo/Condena"), y script outline original (inglés, `KPT Script Outline.pdf`) usado para completar Acto 2 (detalle) y Acto 3 completo. Secciones 1, 2, 3 y 5 reescritas. Se agregó §2.7 con puntos de reconciliación narrativa↔implementación que necesitan decisión del equipo. |
| 0.3 | 2026-09-04 | **GDD cerrado**: las 8 disidencias de §2.7 resueltas en sesión con Diego. Principio general adoptado: **diseño transmedia** — el juego y el guion/película comparten universo y personajes pero pueden resolver beats de forma distinta sin que eso sea un bug a corregir. Título del Nivel 2 finalizado, mecánica de Rocoso y sistema de recompensas confirmados sin cambios en el juego, "Nivel 4" descartado, cutscene de cierre del Nivel 1 descartada del juego (queda solo en el guion), B-plot de la Abuela confirmado canon de historia, e idea anotada para la 2ª sidequest del Nivel 2 (minijuego de ritmo en el bar de jazz). |

Fuentes usadas para este documento: `CLAUDE.md` (raíz), `docs/claude/*.md`, `ROADMAP.md`, `.specify/memory/constitution.md`, `specs/*/spec.md`. Cualquier sección ⚪ necesita una conversación con Diego/Valentino, no una suposición.

---

## 1. Visión

### 1.1 Pitch 🟢
Kami tiene 12 años, pelo celeste atado en dos rodetes, vestido blanco con overol — estética **cottagecore**. Vive con su tío Norberto y su abuela en una granja (no tiene padres), dentro de un mundo **enteramente hecho de papel**: personajes, edificios, arbustos, árboles, todo, como en un libro pop-up. Es bondadosa y servicial hasta la exageración, muy hábil con su enorme tijera (corta casi cualquier cosa) pero un poco torpe con los pies. Su magia es el **Origami**: puede doblar el papel del mundo — incluso a personas de papel — para crear herramientas, vehículos y aliados improvisados.

El juego entero es la travesía de Kami de vuelta a casa después de que **el Narrador** — una entidad metaficcional que narra todo lo que le pasa, dueño literal del escritorio donde conviven todos los libros del mundo — la castiga por "romper las reglas de las historias" (doblar personajes, cortar el escenario a su antojo) y la destierra a otros libros que no son el suyo. Cada nivel del juego es un **libro/historia distinta**, con estética y reglas propias, pero todos comparten la mecánica central: todo es de papel, y a Kami se le puede cortar y doblar.

### 1.2 Pilares de diseño 🟡
1. **El libro es el mundo.** La metáfora de papel/pop-up no es solo estética: pasar de página (`paginas-y-hoja.md`), plegar (Abuela/origami) y cortar (tijera) son las verbas centrales de exploración y puzzle — y ahora está confirmado por guion: fuera de los libros existe literalmente el escritorio del Narrador, el toque metaficcional es intencional y estructura los 3 niveles como 3 libros distintos.
2. **Progresión legible por equipamiento.** Cada mejora (tijera mejorada, botas de agua, botas rápidas) es visible en el personaje y desbloquea rutas antes cerradas — economía de recursos simple y física de reconocer sin UI.
3. **Mundo hecho por gente, no por sistemas invisibles.** NPCs con pedidos concretos (quests data-driven, `QuestSO`) en vez de listas de tareas abstractas — reforzado por el guion: cada pedido de ayuda viene con un regalo personal, no una recompensa genérica (ver §2.7 sobre cómo esto convive con el sistema de recompensas de equipamiento).
4. **Simplicidad de equipo chico.** Diseño ejecutable por 2 personas (código: Diego; arte: Valentino) — ver los 4 pilares técnicos en `CLAUDE.md` (pensar antes de codear, simplicidad, cirugía, objetivo verificable).

### 1.3 Público objetivo ⚪
Sin definición formal (rango etario, referencia de mercado). El tono por nivel ya está claro por guion — granja cálida/cottagecore (Nivel 1), noir detectivesco con humor (Nivel 2), álbum de fotos melancólico con final emotivo sobre una hija que se fue de casa (Nivel 3) — lo cual sugiere un juego familiar/todo-público pero con una capa emocional que también puede llegarle a un público algo mayor (padres, jugadores adultos). Rango etario exacto sigue siendo decisión de negocio/marketing, no de diseño.

### 1.4 Referencias / inspiración
Confirmado por guion, referencia tonal por nivel:
- **Nivel 1 — Kami Papel y Tijera**: cottagecore, vida de granja, aventura familiar.
- **Nivel 2 — Kami Tinta y Condena** *(o "Castigo" — ver inconsistencia de título en §2.7)*: noir detectivesco, saxofón de jazz lento, "todos en este mundo toman café", estética de diario/tabloide.
- **Nivel 3 — Kami Fotos y Recuerdos**: álbum de fotos familiar, tono melancólico/nostálgico, clímax tipo Odisea (tormenta + bote de papel).

Referencias externas concretas (juegos, películas puntuales) siguen ⚪ sin documentar.

---

## 2. Narrativa y mundo

> **Fuentes de esta sección** (en orden de autoridad): (1) guion en español del Acto 1, pegado por Diego el 2026-09-04 — **texto canónico, palabra por palabra donde no se dice lo contrario**; (2) outline página-por-página del Nivel 2 ("Kami, Tinta y Castigo"), también canónico; (3) `KPT Script Outline.pdf` (`D:\DesKtop\kimmiarts\Kami\kami movie\KPT Script Outline.pdf`, en inglés) — versión más vieja, usada solo para **completar huecos que el material nuevo no cubre** (marcado explícitamente como tal). Donde las dos fuentes chocan, gana el material nuevo en español y se anota la discrepancia en §2.7.

### 2.1 Premisa 🟢
El mundo del juego es una colección de **libros** sobre el escritorio de un **Narrador** omnisciente que narra todo lo que le pasa a Kami (toque metaficcional deliberado). Cada libro es un universo autocontenido, enteramente hecho de papel para sus habitantes — pero para ellos eso es completamente normal, no un fenómeno extraño. El juego entero es la historia de **Kami intentando volver a su libro** después de que el Narrador la destierra a libros ajenos como castigo por "romper las reglas de la historia" (cortar y doblar el escenario y a los personajes a su antojo).

#### 2.1.1 Principio de diseño: transmedia, no traducción literal (decidido 2026-09-04) 🟢
El guion (destinado a película/libro) y el videojuego **comparten universo, personajes y beats principales, pero no tienen por qué resolver cada detalle de la misma manera**. Es diseño transmedia deliberado, no una inconsistencia a corregir. Regla práctica para todo el equipo:

- Si un beat del guion choca con una mecánica de juego ya implementada y estable, **el juego no se cambia por el guion** — se documenta la versión de cada medio por separado (ver ejemplos resueltos en §2.7).
- El guion sigue siendo la fuente de verdad para tono, personajes, motivaciones y estructura de actos — pero un beat concreto puede jugarse distinto de como se cuenta en el libro/película.
- Ante una divergencia nueva, no asumir cuál gana: anotarla y decidirla explícitamente (mismo patrón que §2.7).

### 2.2 Kami — la protagonista 🟢
12 años, pelo celeste en dos rodetes, vestido blanco + overol, estética cottagecore. Vive con su tío Norberto y su abuela en una granja de campo; no tiene padres. Diligente y compasiva — ayuda a todo el mundo con sus tareas y problemas todos los días — pero un poco torpe con los pies (coherente con la fricción de locomoción ya implementada, ver `RunStop` en `docs/claude/spine-kami.md`). Es **muy** hábil con su enorme tijera (corta casi cualquier cosa) y tiene magia de **Origami**: dobla papel — incluso personas de papel — para construir cosas que la ayuden.

### 2.3 Estructura en 3 libros (= 3 niveles)

| # | Título canónico | Corresponde a | Estado |
|---|---|---|---|
| 1 | **Kami Papel y Tijera** | `Nivel1_KamiPapelTijera.unity` ("La Rural") | 🟢 Guion completo (Acto 1, español) + sistemas de juego estables |
| 2 | **Kami Tinta y Condena** *(título finalizado 2026-09-04 — el outline detallado de páginas usaba "Kami, Tinta y Castigo" como título de trabajo, ya reemplazado)* | `Level2_Newspaper.unity` | 🟡 Guion completo página-por-página (español) + sistemas transversales estables, contenido narrativo sin implementar todavía |
| 3 | **Kami Fotos y Recuerdos** | Sin escena propia en el proyecto todavía — el placeholder de "Nivel 3" en branches sin mergear (`level3-scenes`) sería este libro (⚪ confirmar) | 🔴 Guion completo (solo en el script outline en inglés) — cero implementación |

El requisito de diseño explícito de Diego para **cada** nivel: al menos un tramo de plataformeo/exploración, un uso de la tijera, un uso de origami, y 1-2 sidequests. Se anota abajo cómo lo cumple cada libro.

### 2.4 Acto 1 — Kami Papel y Tijera 🟢 (guion canónico, completo)

Kami vive con su abuela y su tío Norberto en una granja. Un día, casi todos le piden ayuda a la vez:

- **Dalia** (florista): "Kami, Kami! Me tenés que ayudar! Necesito que uses tus tijeras para cortar flores." → Kami corta flores con la tijera. Regalo: **una flor**.
- **El Chino** (herrero): "Necesito que me traigas ramas para encender el horno!" *(el script en inglés, más detallado, especifica: "fetch me 100 paper sheets for kindling" — encaja exacto con la implementación actual de `Quest04_Chino`: 100× papel)* → Kami junta ramas/papel con la tijera. Regalo: **un broche de pelo**.
- **Tiburcio**: "Necesito que me traigas de vuelta a las gallinas que se escaparon." → Kami corre y salta para traerlas de vuelta (encaja con el sistema actual: cortar el árbol libera/reordena el cruce de las gallinas patrullando, evento `OnTreeCutForChickens`). Regalo: **una docena de huevos**.

Kami, cansada, vuelve a casa. Su **Tío Norberto** le pide que encuentre a la Abuela, que salió a caminar y no volvió. Kami la busca gritando su nombre:
1. Usa su magia de Origami para hacer un **avión de papel** y buscarla desde las alturas — no la encuentra.
2. Al borde del río, hace un **puente de papel** para cruzar — no la encuentra.
3. Detrás de una represa, la encuentra por fin: **"¡Acá estoy!"**

Del ruido de los gritos se despierta **Rocoso**, un monstruo de piedra enorme que gruñe pero no habla, y está muy enojado. La Abuela, asustada, le explica a Kami que no pueden correr: hay que **cortar la represa** para que el agua arrastre a Rocoso. Kami, también asustada, corta la represa con su tijera; el río se lleva a Rocoso rodando lejos.

Al volver, la Abuela se tuerce el pie y no puede caminar. Kami tiene una idea: como la Abuela también es de papel, la **dobla cuidadosamente en origami** y la lleva en su morral. Llegan a casa, donde el Tío Norberto tiene el asado listo ("¡A comer!", sin mirar). Kami **desdobla** a la Abuela con cuidado y por fin comen y descansan juntas.

**Punto de giro (guion/película — sigue canon de la historia)**: mientras comen, una mano gigante del Narrador (con guantes blancos) agarra a Kami y a la Abuela: *"¿Qué es esto de cambiar historias y doblar personajes? Eso va contra las reglas. Voy a tener que castigarlas."* Las tira a libros distintos: Kami cae en *Crime, Punishment, and Blood* (= Kami Tinta y Condena), la Abuela cae en un libro de recetas, *Desserts, Cakes, and Chocolates*.

**Decidido 2026-09-04 (transmedia, §2.1.1)**: esta escena **no se implementa como cutscene en el juego** — el salto del Nivel 1 al Nivel 2 se resuelve de otra forma en el videojuego (mecanismo concreto todavía ⚪ sin diseñar, no bloquea nada hoy porque los niveles ya son cargables por separado). El beat queda como parte del guion/película únicamente.

**Cumplimiento del requisito de nivel**: plataformeo/exploración (búsqueda de la Abuela por el mapa), tijera (represa, flores, ramas), origami (avión, puente, plegado de la Abuela), sidequests (Dalia, Chino, Tiburcio — 3, más de las 1-2 pedidas).

### 2.5 Acto 2 — Kami Tinta y Condena / Castigo 🟡 (guion canónico, página por página)

Historia de detectives estilo noir dentro de un diario/tabloide de una gran ciudad — papel de nuevo, pero con estética noir y saxofón de jazz lento *(tono confirmado por el script en inglés; el texto nuevo en español tiene un humor más liviano en los diálogos — ver §2.7)*. Kami consigue un outfit de detective acorde al mundo.

| Página | Título | Beats |
|---|---|---|
| **1** | La Gran Ciudad y el Café | Kami llega perdida y se encuentra con **Natalia**: *"hola jajaj no te preocupes, yo te ayudo a volver."* **[Rama opcional]**: Kami se encuentra con Natalia siendo acosada por un Desconocido. En el café charlan más — *"wow tenemos muchas cosas en común"* — y Natalia revela su investigación: *"estoy investigando lo del Pelusa Original robado, seguime."* **NPC de sidequest 1**: el amigo de Natalia que investiga la desaparición de una chica — llamada **Cami** (ver conexión directa con la trama del Narrador en §2.6/§2.7: el Narrador tiene una hija real llamada Cami que se fue de casa — probablemente la misma persona, pista temprana). |
| **2** | Persiguiendo pistas alrededor del museo | Las chicas siguen las pistas del ladrón: Kami **corta unas cortinas** (uso de tijera) y hace una **grulla de origami** para pasar por encima de unos portones/gates (uso de origami + plataformeo/exploración). |
| **3** | La Casa de Natalia | Abajo de la casa de Natalia hay un bar de jazz/tango — **idea anotada para la sidequest 2** (2026-09-04, sin diseñar en detalle todavía): un minijuego de ritmo en el bar, al tempo de la música ("como el osu de los papeles pero a tempo" — reusaría/adaptaría la mecánica de corte de papel existente sincronizada a un beat musical). Las chicas encuentran un obsequio misterioso en la puerta con una carta de un admirador anónimo: **¡es el cuadro robado (el Pelusa Original)!** *"Kami, este es el cuadro! Lo tenemos que llevar al museo!"* Pero es una emboscada de **Ariel** (viejo amigo de Natalia, el verdadero ladrón): *"¡Policías! ¡Ahí están las ladronas!"* — las arrestan. |
| **4** | La Comisaría | Las chicas son traídas de forma "papelística" hasta la comisaría (efecto sonoro tipo cohete/motor: *fiiiiuuu... brmbrmbrm*). La **Abuela cae sobre la comisaría rompiendo todo** — irrumpe de golpe, sin explicación en el texto nuevo de cómo llegó ahí *(el script en inglés sí lo explica: ver B-plot en §2.5.1)*. Las chicas se escapan cortando todo y armando una **cajita de papel** para escabullirse entre los bultos de la comisaría. |
| **5** | El Museo | Las chicas van al museo, explican todo y demuestran su verdad: tienen un ticket de compra fechado justo a la hora en que desapareció el cuadro (coartada). El dueño del museo las manda de vuelta a su libro con una **catapulta** — pero en el aire chocan contra la **lámpara azul del escritorio** (del Narrador), lo cual las desvía a un tercer libro en vez de a casa → transición al Acto 3. |

#### 2.5.1 B-plot de la Abuela (confirmado canon de historia 2026-09-04)
Mientras tanto, la Abuela, atrapada en el libro de recetas *Desserts, Cakes, and Chocolates*, improvisa recetas caóticas en un programa de cocina de TV; impresiona al chef principal y lo convence de lanzarla (con una prensa de crema) hacia el libro de Kami — esto explica la llegada abrupta de la Abuela en la Página 4 ("cae sobre la comisaría rompiendo todo"). **Sigue canon de la historia/película**; el juego no tiene por qué implementarlo como nivel o minijuego jugable (principio transmedia, §2.1.1) — queda como trasfondo narrativo, no como contenido a construir en Unity salvo que el equipo decida lo contrario más adelante.

**Cumplimiento del requisito de nivel**: plataformeo/exploración (Página 2 y 4), tijera (cortinas, Página 4), origami (grulla, cajita de papel), sidequests: 1) el amigo de Natalia (Página 1); 2) idea anotada, minijuego de ritmo en el bar de jazz bajo la casa de Natalia (Página 3, sin diseñar en detalle — ver tabla arriba).

### 2.6 Acto 3 — Kami Fotos y Recuerdos 🔴 (solo en el script en inglés — sin contraparte en español todavía, sin escena en el proyecto)

Kami y la Abuela caen en un libro que es un álbum de fotos familiar. Recorren pasillos llenos de fotos de un padre y una hija; Kami nota parecidos entre ella y la chica de las fotos. Frustrada y desesperada por volver a casa, corta las fotos para doblar un **dragón de papel** gigante y furioso, y así escapar volando.

El Narrador interviene: *"No deberías ver estas fotos."* Intenta desdoblar al dragón pero falla; el caos daña las fotos, y el Narrador le suplica a Kami que pare. Finalmente revela su dolor: tuvo una hija real, **Cami**, a quien le leía cuentos — ahora que se mudó, usa a Kami para revivir esos recuerdos. Conmovida, Kami dobla un **corazón de origami** con la última foto: una imagen del Narrador con su hija.

**Clímax**: una tormenta inunda el escritorio. Kami dobla un **bote de papel de emergencia** para ella y la Abuela; en un viaje tipo Odisea, navegan la tormenta y llegan por fin a su propio libro. El Narrador, redimido, le promete a Kami que de ahora en más podrá viajar entre libros libremente cuando quiera.

**Cumplimiento del requisito de nivel**: plataformeo/exploración (pasillos del álbum, travesía del bote), tijera (cortar las fotos), origami (dragón, corazón, bote) — sidequests: ⚪ ninguna definida todavía (el libro es corto y muy lineal/emocional en el guion; puede ser intencional que no lleve sidequests).

### 2.7 Reconciliación narrativa ↔ implementación — decisiones cerradas (sesión 2026-09-04)

Las 8 disidencias detectadas al incorporar el guion se resolvieron con Diego. Se documentan las decisiones, no solo la pregunta original, para que quede registro de *por qué*:

1. **Título del Nivel 2** → **"Kami Tinta y Condena"**, definitivo. "Kami, Tinta y Castigo" era título de trabajo del outline de páginas, ya reemplazado en este documento.
2. **Combate de Rocoso vs. lore** → **no se toca el juego**. Aplicando el principio transmedia (§2.1.1): en el **juego**, Rocoso se puede vencer con la Tijera Mejorada (combate normal, FSM ya implementada) **o** con el set-piece de la represa — ambos caminos válidos. En el **guion/película**, se vence únicamente cortando la represa. Sin impacto en la spec `specs/001-rocoso-navmesh`: el refactor a NavMesh sigue siendo puramente técnico, no depende de esta decisión.
3. **Regalos narrativos vs. recompensas de equipamiento** → **no se toca el juego**. El sistema de quests (`QuestSO`, equipamiento como recompensa) queda exactamente como está. Los regalos chicos (flor, broche de pelo, docena de huevos) son capa exclusiva del guion/película, sin necesidad de mapeo 1:1 contra el juego.
4. **Cutscene de cierre del Nivel 1** (mano del Narrador) → **no se implementa en el juego**. Queda como beat del guion/película únicamente. El juego resuelve el paso de Nivel 1 a Nivel 2 de otra manera — mecanismo concreto todavía ⚪ sin diseñar, sin urgencia porque los niveles ya funcionan como cargas separadas.
5. **"Nivel 4"** → **descartado**. El juego y la historia constan de 3 libros/niveles nada más (Kami Papel y Tijera / Kami Tinta y Condena / Kami Fotos y Recuerdos). La branch `level4-scenes` queda sin scope narrativo — su destino técnico (borrar, archivar) es una decisión de repo aparte, no de este GDD (ver ROADMAP.md).
6. **Tono de Natalia** → gana el texto nuevo en español (más liviano, con humor) por ser la fuente más reciente; el tono serio del script en inglés queda superado. Sin más acción que tenerlo en cuenta al escribir el resto de sus líneas.
7. **2ª sidequest del Nivel 2** → **idea anotada, sin diseñar en detalle**: un minijuego de ritmo en el bar de jazz/tango debajo de la casa de Natalia (Página 3), reusando/adaptando la mecánica de corte de papel a tempo musical ("como el osu de los papeles pero a tempo"). Queda registrado en la tabla de §2.5 como punto de partida para cuando se diseñe esa página.
8. **B-plot de la Abuela en el libro de recetas** → **confirmado canon de la historia/película**. El juego no tiene por qué implementarlo como contenido jugable (principio transmedia) — ver §2.5.1.

---

## 3. Personajes

### 3.1 Kami (protagonista) 🟢

**Descripción física** (guion): 12 años, pelo celeste en dos rodetes, vestido blanco + overol, estética cottagecore. En el Nivel 2 consigue un outfit de detective acorde al mundo noir (⚪ diseño de ese outfit sin especificar).

**Personalidad**: diligente y compasiva, ayuda a todos con sus tareas y problemas todos los días; hábil con la tijera pero un poco torpe con los pies.

**Fantasía central**: una niña ágil que resuelve el mundo con dos herramientas de papelería — tijera (destructiva/quirúrgica) y origami (constructiva, incluye doblar personas de papel). El player-controller es un **MVC casero** (ver §12.1) con foco en animación Spine muy pulida (tracks superpuestos, ver §8.1).

**Estados de Kami** (implícitos en `PlayerModel`/`PlayerView`): Idle, caminar, correr (con RunStop), saltar/caer/aterrizar, atacar (parado o en movimiento), casting (origami), tirar de solapas, recibir daño, morir (con causa), pasar de página (RidingPage), pisar zonas mojadas.

**Equipamiento visible** (§7):
- Tijera Normal / Tijera Mejorada — cambia skin de Spine + daño. Narrativamente, la tijera se la da el Tío Norberto al principio del Acto 1 ("le señala unas tijeras cómicamente grandes que va a necesitar después").
- Botas de agua (recompensa Tiburcio) — 🔴 falta el skin de Spine (issue de roadmap).
- Botas rápidas (recompensa Chino) — bloqueada por el NPC stub.

### 3.2 Roster de NPCs de Kami Papel y Tijera (Nivel 1)

| NPC | Descripción (guion) | Pedido / rol narrativo | Regalo narrativo | Quest implementada | Estado técnico |
|---|---|---|---|---|---|
| Tío Norberto | 42 años, alto, delgado pero en forma, pelo celeste | Le pide a Kami que encuentre a la Abuela (él está ocupado asando la carne); le señala la tijera al principio | — | Sin quest propia — entrega la tijera inicial en su primer diálogo | 🟢 Completo |
| Abuela | ⚪ (sin descripción física en el guion — figura materna/mentora) | Sale a caminar y se pierde; guía a Kami para derrotar a Rocoso cortando la represa; se tuerce el pie y Kami la dobla en origami para llevarla | — (ella misma es "el regalo": volver a casa) | `Quest01_Abuela` — entregar 1× recurso "abuela" (evento `OnAbuelaFold`) → Tijera Mejorada | 🟢 Completo, el más complejo (coordina origami fold/unfold) |
| Dalia | 30 años, petisa, linda, vestido rosa, trenzas rojas — florista | "Cortame 3 flores azules (clemátides), estoy desbordada de pedidos" | Una flor | `Quest02_Dalia` — 3× flores → Tijera Mejorada | 🟢 Completo |
| El Chino | 35 años, musculoso, alto, espaldas anchas — herrero | "Traeme ramas/100 hojas de papel para encender el horno, se apagó con el viento" | Un broche de pelo | `Quest04_Chino` — 100× papel → Botas Rápidas | 🔴 **Stub vacío** — la quest ya está configurada y ahora tiene guion completo para escribirse; sin NPC funcional todavía |
| Tiburcio (Honguero) | ⚪ sin descripción física en el guion | "Traeme de vuelta a las gallinas que se escaparon" (versión narrativa actual — reemplazó a una versión anterior sobre cortar hongos de la cabeza de las gallinas) | Una docena de huevos | `Quest03_Tiburcio` — cortar el árbol (evento `OnTreeCutForChickens`) → Botas de Agua | 🟢 Completo, recién estabilizado (ver §12.5) |
| Florista | Posible duplicado/alias de Dalia — el guion no menciona un segundo personaje florista | ⚪ Sin pedido propio conocido | ⚪ | Sin quest asociada conocida | 🔴 **Stub vacío** — revisar si `NPC_Florista` es en realidad Dalia sin terminar de conectar, antes de escribirle un pedido nuevo desde cero |

### 3.3 Roster de NPCs de Kami Tinta y Condena (Nivel 2)

| NPC | Descripción / rol | Estado técnico |
|---|---|---|
| Natalia | Joven detective atlética, expresión decidida (tono script en inglés) / presentación más cómica y liviana en el guion nuevo ("hola jajaj no te preocupes, yo te ayudo a volver"). Investiga el robo del cuadro "El Pelusa Original". Comparte con Kami que no conoce a sus padres y que ambas encuentran atractivo al chico del café. Le regala a Kami tijeras más chicas, revelando que es costurera. | 🔴 No implementado — spec en `specs/003-natalia-npc-nivel2`, dependencia externa: animación Spine de Valentino |
| Amigo de Natalia (sidequest 1) | Investiga la desaparición de una chica llamada **Cami** — probable conexión directa con la hija real del Narrador (ver §2.6/§2.7) | 🔴 No implementado, sin nombre propio definido |
| Desconocido | Acosa a Natalia en un encuentro **opcional** al llegar Kami a la ciudad | 🔴 No implementado, rama opcional — confirmar si es obligatoria u opcional de verdad |
| Ariel | Viejo amigo de Natalia, resulta ser el ladrón del cuadro; tiempo atrás intentó besarla, ella lo rechazó y se distanciaron; arma la emboscada en la casa de Natalia y las hace arrestar; al final se disculpa con Natalia (sigue arrestado) | 🔴 No implementado |
| Dueño/curador del museo | Recibe la explicación y la coartada de las chicas al final; usa una catapulta antigua para mandarlas de vuelta a su libro (las desvía por accidente hacia el Nivel 3) | 🔴 No implementado |
| Chino | Puede o no reaparecer aquí (herrero, sidequest 2 del Nivel 2 aún sin definir — ⚪) | 🔴 Sin definir |

### 3.4 Personajes de Kami Fotos y Recuerdos (Nivel 3)

| NPC | Rol | Estado |
|---|---|---|
| El Narrador | Entidad metaficcional, narra todo el juego; interviene físicamente (mano gigante con guantes blancos) para castigar a Kami en el Acto 1; en el Acto 3 revela que tuvo una hija real, Cami, a la que le leía cuentos, y que usa a Kami para revivir esos recuerdos; termina redimido, promete a Kami libertad para viajar entre libros | 🔴 No implementado — personaje central de la trama, sin ficha técnica de gameplay todavía (¿es un personaje jugable en algún momento, un elemento de escenario, una voz en off?) |

### 3.5 Enemigos

| Enemigo | Movimiento | Estado | Notas de diseño |
|---|---|---|---|
| Gallinas (`GallinaAgent`) | NavMeshAgent, patrulla por zonas (antes/cruce/después del árbol de Tiburcio) | 🟢 Completo (agosto 2026) | No detectan ni evaden salvo por distancia (`evadeDistance`); zona "segura" tras cortar el árbol |
| Rocoso | Física por fuerzas (`Rigidbody.AddForce`), FSM de 5 estados | 🟢 Completo como enemigo de combate — **decidido (§2.7 punto 2, diseño transmedia)**: en el juego se vence con la Tijera Mejorada o con el set-piece de la represa; en el guion/película solo con la represa. No hace falta tocar el código por esto. | Persecución en línea recta, sin path planning; migrar a NavMesh es spec `specs/001-rocoso-navmesh` — refactor puramente técnico, sin relación con la decisión narrativa |
| Enemigo de sigilo | ⚪ Heredaría de `PatrollingAgent` | 🔴 No implementado — spec `specs/002-enemigo-sigilo` con preguntas abiertas | El proyecto no tiene ningún precedente de detección (vision cone / line-of-sight / hearing) — se construye desde cero; sin conexión narrativa confirmada todavía (¿en qué libro viviría?) |
| `EnemySpawner` (genérico) | — | 🔴 Armado pero sin caller activo | Candidato a reusarse para el enemigo de sigilo (issue de roadmap) |

**Patrón de implementación de NPC nuevo** (Natalia, Chino, Florista, Ariel, etc.): `*DialogueTrigger.cs` (hereda `QuestDialogueTrigger` si es quest de tipo Resource estándar, o custom si es Event-based como Tiburcio) + opcionalmente `QuestSO` nueva + entradas en 3 tablas de localización (es/en/pt) + si tiene movimiento, estados sobre `NPC.cs`/`NPC_IdleState`/`NPC_FollowPlayerState`.

---

## 4. Gameplay Core

### 4.1 Movimiento y control 🟢
Locomoción estándar (Idle/walk/Skip/Run con RunStop, salto/caída/aterrizaje) sobre `CharacterController`, decidida por `GetAxisRaw` (no `GetAxis`, para evitar que el smoothing meta un estado de Walking espurio entre Skip e Idle). Flip visual vía `Skeleton.ScaleX`. Detalle técnico completo en `docs/claude/spine-kami.md`.

**Paso de página (page-turn)** — la mecánica de traversal única del juego: al llegar al borde de una página y presionar E, Kami queda visualmente enganchada al borde de la hoja que gira (`RidingPage`, sin IK, se mueve el transform raíz) mientras el juego resuelve la posición real de destino por detrás. Ver flujo completo en `docs/claude/paginas-y-hoja.md`.

### 4.2 La Tijera — mecánica de corte 🟢
- `ICortable`: interfaz que implementa todo lo cortable en el mundo.
- `TijeraHitbox`: hitbox de ataque con patrón "asumir miss hasta que se demuestre lo contrario" (flag `missed` en `true` por defecto en `OnEnable`, pasa a `false` solo si corta algo `ICortable`; tocar paredes/suelo no cuenta). Dispara SFX `TijeraMiss` si termina en miss.
- Dos skins/niveles de tijera (Normal / Mejorada) vía `Player.SetTijeraEquipment()` — ver §7.
- Animaciones `Attack` (parado) y `AttackMOVE` (moviéndose/aire) con evento de hitbox embebido en el frame 0.467s.

### 4.3 Origami 🟢
Minijuego de plegado con **costo en papel**, mostrado en un canvas por pedestal (`PedestalCanvasDisplay`) que aparece cuando Kami pisa el trigger de un sello — mostrado siempre (tenga o no papel suficiente: es cuando más falta hace verlo). Detalle de flujo, cross-talk entre los ~12 sellos del nivel y la excepción de diseño de los sellos de la Abuela (sin base 3D ni canvas de costo) en `docs/claude/origami-y-tooltips.md`.

### 4.4 Solapas (pop-up mechanics) 🟢
Partes del libro que se abren/cierran como un pop-up real (`Solapa`, Animator de Unity con bool `isOpen`). `TriggerSolapa.Interact()` decide abrir (`PullSolapas`) o cerrar (`PullSolapasReverse` — 🔴 animación pendiente de exportar, hoy cae a `PullSolapas` con warning) según el estado previo. Bloquea movimiento/salto mientras dura.

### 4.5 Muerte y respawn — "muerte con causa" 🟢
El juego distingue **por qué** murió Kami y lo refleja en animación, texto de overlay y política de respawn:

| Causa | Trigger | Animación | Respawn |
|---|---|---|---|
| Ahogo (río) | `Rio.cs` + delay tunable, sistema `IMojable` | `Drowning` | Configurable: última posición segura (snapshot con doble buffer) o punto de entrada de página |
| Rocoso | Golpe de headbutt | `Death` | Respawn común (`lastUsedSpawn`) |
| Genérica | Otras fuentes de daño | `Death` | Respawn común |

Secuencia completa (anim → overlay tras delay → respawn recién al cerrar overlay con E) en `docs/claude/spine-kami.md`. **Pendiente de setup de escena**: el GO del defeat overlay necesita el componente `DefeatOverlay` correctamente asignado y las keys de localización `DefeatDrowning`/`DefeatRocoso`/`DefeatGeneric` creadas (issue de roadmap #20).

### 4.6 Combate / daño (`IGolpeable`) 🟢
Patrón único reusado por toda hitbox de ataque del juego (tijera de Kami, headbutt de Rocoso): collider trigger que arranca deshabilitado, se prende solo durante la ventana de animación correspondiente, y marca un flag booleano en vez de resolver daño en el propio evento del trigger — el estado que disparó el ataque decide qué hacer después de que la animación termina, no el hitbox.

---

## 5. Niveles

### 5.1 Nivel 1 — "Kami Papel y Tijera" (La Rural) 🟢
**Escena activa**: `Nivel1_KamiPapelTijera.unity` (⚠️ NO `Nivel1_LaRural SpineTest.unity`, que quedó stale desde fines de agosto 2026 y puede tener referencias rotas).

- **Contenido**: quests de Abuela/Dalia/Tiburcio/Norberto, río con mecánica de ahogo, gallinas patrullando (NavMesh baked específicamente para ellas), Rocoso. Guion completo — ver §2.4.
- **Estado**: production-ready a nivel sistemas, con deuda técnica menor anotada (Tiburcio sin fallback si `OnQuestCompleted` no llega — bajo riesgo). **Decidido (§2.7 punto 4)**: la cutscene de cierre del guion (el Narrador castigando a Kami y a la Abuela) **no se implementa en el juego** — queda solo en la película/libro. El mecanismo con el que el juego encadena Nivel 1 → Nivel 2 sigue ⚪ sin diseñar, sin urgencia.

### 5.2 Nivel 2 — "Kami Tinta y Condena" (Newspaper) 🟡
**Escena activa**: `Level2_Newspaper.unity`. Guion completo página-por-página (§2.5); implementación de contenido narrativo todavía no empezada.

- **Estable (sistemas)**: geometría base, decoración, edificio izquierdo (animator propio), varias páginas del diario, todos los sistemas transversales (page-turn, inventario, quests, cámara, Flap UI).
- **En progreso (pieza activa de desarrollo)**: página del museo — modelo `KamiMuseo.fbx` importado, sin quest/NPC/triggers wireados todavía. Corresponde narrativamente a la **Página 5** del guion (§2.5).
- **Mapa de páginas de guion → contenido de nivel**:

  | Página de guion | Título | Mapeo a nivel |
  |---|---|---|
  | 1 | La Gran Ciudad y el Café | Llegada + Natalia + sidequest 1 (amigo, chica desaparecida) — sin implementar |
  | 2 | Persiguiendo pistas alrededor del museo | Cortinas (tijera) + grulla sobre gates (origami) — sin implementar |
  | 3 | La Casa de Natalia | Bar de jazz/tango abajo, obsequio del cuadro, emboscada de Ariel — sin implementar |
  | 4 | La Comisaría | Llegada de la Abuela, escape con cajita de papel — sin implementar |
  | 5 | El Museo | Corresponde a la página del museo ya en desarrollo (`KamiMuseo.fbx`) — final del nivel, catapulta hacia el Nivel 3 |

- **Pendiente conocido (sistemas)**: feedback visual de botas de agua (TODO literal en `LevelManager.cs` ~línea 121 — falta el skin de Spine).
- **Pendiente conocido (narrativo)**: destrabar Chino/Florista con el guion ya disponible; diseñar en detalle la 2ª sidequest (minijuego de ritmo en el bar de jazz, §2.7 punto 7).

### 5.3 Nivel 3 — "Kami Fotos y Recuerdos" 🔴
Guion completo (§2.6), **sin escena propia todavía** en el proyecto. El placeholder "Nivel 3" (`level3-scenes`, branch sin mergear, copia de `SampleScene`) sería este libro — ⚪ confirmar con el equipo. Es el cierre de la historia: revela la trama del Narrador y su hija Cami, clímax con dragón/corazón/bote de papel.

### 5.4 "Nivel 4" — descartado (decidido 2026-09-04)
El juego consta de **3 libros/niveles en total** (§2.7 punto 5) — no hay un cuarto libro en la historia. La branch `level4-scenes` queda sin scope narrativo; su destino técnico (archivar o borrar la branch) es una decisión de repo aparte, a reflejar en `ROADMAP.md`.

---

## 6. Sistemas

### 6.1 Page-turn (paso de página) 🟢
Pipeline de eventos (`Evento.OnPlayerPressedE` → `PageScrollerManager` → `HojaMaster`/`HojaMaster_Rev` (rig **Mecanim**, no Spine) → `RidingPage` → reposicionamiento autoritativo) documentado en detalle en `docs/claude/paginas-y-hoja.md`. Verificado en agosto 2026: coincide con el código actual, sin discrepancias.

### 6.2 Quests — arquitectura data-driven 🟢
`QuestSO` (ScriptableObject): nombre/descripción (keys de localización), condición (`Resource` con tipo+cantidad, o `Event`), tipo de recompensa, sprite de retrato. `QuestManager` (singleton) escucha eventos de recurso/quest, evalúa y dispara `OnQuestCompleted`. `QuestEffector` activa/desactiva GameObjects al completar/entregar. Detalle en `docs/claude/quests-y-dialogos.md`.

### 6.3 Diálogos 🟢
Jerarquía de herencia: `TriggerScript` → `TriggerDialogue` (array de `DialogueSO`) → `QuestDialogueTrigger` (flujo estándar de 4 diálogos: pedido → recordatorio en loop → agradecimiento+recompensa → charla post-entrega) → triggers específicos por NPC. Localización vía Unity Localization (`Assets/Localization Settings/Tables/DialogueTable_es/_en/_pt.asset`).

### 6.4 Inventario y recursos 🟢
8 tipos de recurso (`LevelManager`): hongos, flores, papel, botasAgua, botasRapidas, tijera, tijeraMejorada, abuela. `InventoryManager` reacciona a `Evento.OnResourceUpdated`; `InventorySlot` muestra sprite/color/nombre localizado + cantidad, con reacomodo sin huecos al quitar ítems.

### 6.5 Equipamiento 🟡
Hoy solo tijeras (`TijeraEquipment.Normal`/`Mejorada`, dos skins de Spine Atlas 8). Diseño explícitamente preparado para expandirse a multi-slot (botas, guantes) cuando el proyecto migre a **Spine 4.x** (hoy en 3.8, sin esa API) — el comentario está ya en el código (`Player.cs`) esperando esa migración.

### 6.6 IA y enemigos — tres paradigmas conviven 🟡
El proyecto **no** tiene un único sistema de movimiento de IA:

1. **NavMeshAgent** (`PatrollingAgent`/`GallinaAgent`) — patrulla por waypoints + evade por distancia, con puntos de extensión virtuales listos para heredar (`ShouldEvade`, `UpdateEvadeDestination`, `OnEvadeStart`, `OnWaypointReached`).
2. **Física por fuerzas** (`Rocoso` + `FiniteStateMachine` genérica de 5 estados) — persecución directa sin path planning.
3. **A\* por nodos manuales** (`Barquito`) — grafo de `Node` colocados a mano + steering `Arrive()`, sin NavMesh ni Rigidbody.

Implicación de diseño: **no asumir que una solución que funcionó en un paradigma se traslada trivialmente a otro** (ver spec de Rocoso→NavMesh, que es un refactor real, no un flag). Detalle completo en `docs/claude/enemigos-e-ia.md`.

### 6.7 Cámara 🟢
`CameraManager`: array de Cinemachine VirtualCameras por `CameraMode` (CloseUp, OrigamiCasting, Normal, General, BookCenter, ReceiveReward). Secuencia de page-turn: CloseUp → BookCenter (con delay) → Normal. `CamWheelManager`: selector radial manual con resync automático al cambiar de cámara por otro medio.

---

## 7. UI / UX

### 7.1 Flap menu (menú deslizable) 🟢
4 tabs: Quests, Inventario, Settings (brillo/contraste/volumen + confirmación de salida), Controles. Pausa el juego (`Time.timeScale = 0`) y baja música a 0.4x al abrir del todo.

### 7.2 CamWheel (selector de cámara) 🟢
Menú radial, mismo patrón de apertura/cierre que Flap (`IFlap`).

### 7.3 Tooltips / post-its 🟢
`TooltipManager` orquesta (localiza texto + elige `PostIt` por color); cada `PostIt` maneja su propio ciclo de vida (fade-in, timer de muerte por color, fade-out) — arquitectura corregida tras un bug histórico de timer global compartido. Detalle en `docs/claude/origami-y-tooltips.md`.

### 7.4 Canvas de costo de origami 🟢
Ver §4.3 y `docs/claude/origami-y-tooltips.md` — regla de oro: el canvas queda siempre activo, la visibilidad es alpha de CanvasGroup, nunca `SetActive`.

### 7.5 Accesibilidad ⚪
No hay documentación de opciones de accesibilidad (remapeo de controles, daltonismo, subtítulos más allá de diálogo básico) más allá de los sliders de Settings — a definir si es un objetivo del proyecto.

---

## 8. Arte

### 8.1 Pipeline de personajes — Spine 🟢
Runtime **spine-unity 3.8** (vendoreado en `Assets/Spine`) — ⚠️ NO tiene APIs de Spine 4.0 (ej. `TrackEntry.Reverse`); verificar siempre contra `Assets/Spine/Runtime/spine-csharp/` antes de asumir algo.

**Tracks de Kami** (orden de superposición, de abajo hacia arriba): body (0) → noscissors override (1) → wind override (2) → attack one-shots (3) → paperplane override (4) → hit one-shot (5).

**Skeleton activo**: `Assets/2D/Kami Spine/Atlas 5/skeleton.json` (Atlas 1-4 son viejos, no usar). Animaciones nombradas exactamente como en Atlas 8 (ver lista completa en `docs/claude/spine-kami.md`): Idle, walk, Skip, Run, RunStop, jump, falling, landing, Attack, AttackMOVE, PullSolapas(Reverse), Casting, IdleToCasting, Reward(Loop), Hit, Death, Drowning, Wind, TitleScreen. Existen pero no se usan: IdleNoScissors, walk2, jumpComplete.

**Skins**: `default`, `Tijera_Normal`, `Tijera_Upgrade_1` (Atlas 8).

**Convención NO NEGOCIABLE**: no corregir los typos del skeleton (`NoScissortsOverride`, `tiejraBack`) — son del asset exportado por Valentino, no bugs de código.

### 8.2 HojaMaster — el libro en sí, NO es Spine 🟢
`Assets/Prefabs/Hoja/HojaMaster.prefab` es un rig **Mecanim** (malla skinneada de `HojaOriginal.fbx`), con cadena de huesos `PN000pageJoint00`…`PN000pageJoint30`. Distinción importante para cualquiera que asuma "todo en este juego es Spine".

### 8.3 Dirección de arte general ⚪
Paleta, referencias visuales, estilo de iluminación URP por nivel — a definir/documentar (recae en Valentino).

### 8.4 UI visual (mockups, guía de estilo) ⚪
A definir.

---

## 9. Audio

### 9.1 AudioManager 🟢
Singleton (`Assets/Prefabs/AudioManager.prefab`) — diccionario nombre-de-GO-hijo → AudioSource armado en `Awake`; agregar un sonido nuevo = agregar un GO hijo con AudioSource, el string de `PlayByName` debe matchear exacto. `PlayRandom(params names)` para variación (ej. pasos `Pasos_Kami_01..04`).

### 9.2 Partículas ligadas a gameplay 🟢
`ParticleShooter` en `Kami.prefab`, array indexado por constantes (`PARTICLE_SPRINT/JUMP/REWARD/SPLASH/WIND/FOOTSTEP/RUNSTOP`). Arquitectura actual: partículas de pies viven como hijos del "Footstep Anchor", disparadas one-shot con `Shoot()` (ya no se instancian sueltas). Tres modos de bug ya documentados y evitados (simulation space local sin querer, fuga de instancias, emisión que nunca para, flip no sincronizado) — ver glosario completo en `docs/claude/audio-y-particulas.md`.

### 9.3 Música por nivel 🟢
Nivel 1: MemoFloraMainLoop. Nivel 2: BohrenDestroyingAngels. Decidido por `LevelManager`.

### 9.4 Dirección de audio (referencias, estilo sonoro) ⚪
A definir.

---

## 10. Progresión y economía

### 10.1 Recursos y su rol

| Recurso | Uso | Otorgado por |
|---|---|---|
| Papel | Costo de origami; también condición de `Quest04_Chino` (100×) | Recolección en niveles |
| Flores | Condición de `Quest02_Dalia` (3×) | Recolección |
| Hongos | Recurso de inventario | Recolección |
| "Abuela" (recurso especial) | Condición de `Quest01_Abuela` (1×, vía evento `OnAbuelaFold`) | Evento de gameplay, no recolección directa |
| Tijera / Tijera Mejorada | Equipamiento (§6.5) | Norberto (inicial) / recompensa de quest |
| Botas de Agua | Equipamiento — habilita cruzar zonas mojadas sin penalidad visual completa (🔴 falta skin) | Recompensa `Quest03_Tiburcio` |
| Botas Rápidas | Equipamiento — velocidad | Recompensa `Quest04_Chino` (bloqueada por NPC stub) |

### 10.2 Curva de dificultad / balance ⚪
No hay documento de balance (cantidades exactas, tiempos, curva de progresión entre niveles) — a definir. Los valores tuneables existen en inspector por-sistema (ej. `enterAttackRange`/`viewRange` de Rocoso) pero no hay una visión consolidada de balance global.

---

## 11. Localización 🟢
3 idiomas activos: **es** (base), **en**, **pt**. Tablas en `Assets/Localization Settings/Tables/` (⚠️ carpeta con espacio en el nombre, fácil de errar al buscarla) — `DialogueTable_*` y `UITexts`/`ItemTable` para el resto de la UI. Cualquier NPC/quest/feature nueva que agregue texto necesita entradas en las 3 tablas antes de darse por completa. Branches `localization` y `English-version` existen sin mergear — su destino es un ítem de roadmap sin resolver (#27).

---

## 12. Técnico

### 12.1 Arquitectura del Player — MVC casero 🟢
`Player.cs` es la fachada (stats, componentes, `CurrentState` como única fuente de verdad) que construye y coordina:
- **PlayerModel** — lógica de estados y física.
- **PlayerView** — reacciona: anims de Spine, sonidos, partículas (escucha `OnStateChanged` y eventos embebidos en las anims).
- **PlayerController** — recibe input.

Los tres hablan con `Player`, nunca entre sí directamente.

### 12.2 Stack 🟢
Unity + URP, Steam como plataforma de distribución, Spine-unity 3.8 vendoreado, GitHub para control de versión. Sin CLI de Unity para compilar desde herramientas externas — la verificación final de compilación la hace el editor de Diego.

### 12.3 Convenciones de código 🟢
Código y comentarios en **español**, explicando el *por qué* no el *qué*. Llaves siempre (incluso en una línea), `switch` con `break` explícito, `[SerializeField]` privado + `[Tooltip]` en español, `Debug.Log($"[NombreClase] ...")` en puntos de decisión, guard clauses con `Debug.LogWarning` (nunca no-op silencioso). `PascalCase` clases, `camelCase` variables/campos.

### 12.4 Herramientas de análisis del proyecto 🟢
- **graphify** (`graphify-out/`, gitignoreado): grafo de conocimiento de `Assets/Scripts` — regenerar con `/graphify Assets/Scripts --update` tras cambios grandes de arquitectura.
- **Spec Kit** (`.specify/`, `specs/`): flujo `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement` para features medianas/grandes. Constitución del proyecto en `.specify/memory/constitution.md` (los mismos 4 pilares del estudio, adaptados: pensar antes de codear, simplicidad, cirugía no refactors de paso, ejecutar contra objetivo verificable, trazabilidad/guard clauses, respeto a assets de terceros compartidos).

### 12.5 Deuda técnica conocida (no bloqueante, anotada para no perderla)

| Ítem | Riesgo | Nota |
|---|---|---|
| `treeWasCut` de Tiburcio sin fallback si `OnQuestCompleted` no llega | Bajo | Requeriría reiniciar sin forma de recuperarse |
| Animación `PullSolapasReverse` no exportada | Bajo | Cae a `PullSolapas` con warning, funcional pero visualmente incorrecto al cerrar |
| Botas de agua sin skin de Spine | Medio (recompensa de quest sin feedback visual) | TODO literal en `LevelManager.cs` |
| `ChinoDialogueTrigger` y `NPC_Florista` — stubs vacíos | Medio | Quest 4 completa está bloqueada de facto |
| GUID de `KamiRunstopParticles` parece trucho pero es real y funcional | Ninguno | No "corregir" |
| DefeatOverlay: componente + keys de localización pendientes de wireo en escena | Medio | Bloquea mostrar causa de muerte correctamente |

---

## 13. Producción y roadmap

Fuente autoritativa y viva: [`ROADMAP.md`](../ROADMAP.md) + issues de GitHub (los issues son la unidad de trabajo, el roadmap es el resumen navegable).

### 13.1 Milestones activos (snapshot 2026-09-04)

1. **Nivel 2: Newspaper** (en curso) — terminar página del museo, skin de botas de agua, destrabar Chino y Florista (ahora con guion propio, ver §3.2), setup de DefeatOverlay.
2. **Enemigos v2: NavMesh + Sigilo** — migrar Rocoso a NavMeshAgent (refactor puramente técnico, sin relación con la narrativa — ver §2.7 punto 2, ya resuelto), diseñar e implementar enemigo de sigilo (con preguntas abiertas de diseño), decidir destino de `EnemySpawner`.
3. **Natalia: NPC de Nivel 2** — implementación completa siguiendo el patrón de 4 NPCs ya existentes; no bloqueada por el arte de Valentino (puede arrancar con placeholder). Ahora hay guion completo (§2.5, §3.3) para escribir sus diálogos.
4. **Deuda técnica y pulido** — ver tabla §12.5 + branches sin resolver.
5. **Nivel 3 "Kami Fotos y Recuerdos"** — guion completo (§2.6), sin escena todavía; candidato a absorber el placeholder `level3-scenes`.
6. **Contenido narrativo del Nivel 2** (a raíz del guion incorporado): implementar las 5 páginas del guion (§5.2) — Natalia, sidequest del amigo/Cami, cortinas+grulla, casa de Natalia/Ariel + sidequest de ritmo en el bar de jazz (idea anotada, §2.7 punto 7), comisaría, final del museo. Bloqueado en parte por la spec de Natalia (milestone 3).
7. **Transición Nivel 1 → Nivel 2 en el juego** — dado que la cutscene del guion (mano del Narrador) queda fuera del juego (§2.7 punto 4), falta diseñar cómo el juego encadena ambos niveles. Sin urgencia (los niveles ya cargan por separado), pero es deuda de diseño a resolver antes de pulir la experiencia de principio a fin.
8. **Limpieza de la branch `level4-scenes`** — el juego consta de 3 niveles nada más (§2.7 punto 5, §5.4); decidir si se archiva o se borra.

### 13.2 Proceso de trabajo del equipo 🟢
- Specs de features medianas/grandes en `specs/` vía Spec Kit antes de tocar código.
- Grafo de conocimiento (graphify) para no perder la foto de arquitectura tras cambios grandes.
- Branch de trabajo actual de animación: `feature/spine-animations`.

### 13.3 Riesgos abiertos ⚪
- Dependencia de arte (Valentino) para features con Spine nuevo (ej. Natalia, botas de agua) — el roadmap ya nota que el trabajo de código no está bloqueado, pero el "terminado" del feature sí.
- Tres paradigmas de IA conviven — cualquier estimación de "reusar lo que ya funciona" para un enemigo nuevo debe validarse contra el paradigma correcto (§6.6).
- Sin CI/build automatizado — toda verificación final depende del editor local de Diego.

---

## 14. Apéndices

### 14.1 Glosario de eventos (`Evento`) — subconjunto relevante para diseño

`OnPlayerPressedE`, `OnPlayerPressedSpace`, `OnPlayerPrimaryClick` (definido pero sin trigger real hoy), `OnDialogueStart`, `OnDialogueEnd`, `OnDialogueWriteText`, `OnQuestCompleted`, `OnQuestDelivered`, `OnQuestRewardedStart`, `OnQuestRewardedEnd`, `OnAbuelaDropoff`, `OnAbuelaFold`, `OnAbuelaUnfold`, `OnEncounterEnd`, `OnResourceUpdated`, `OnTreeCutForChickens`, `OnPageTurnStart`, `OnNewPageOpen`, `OnPageFinishTurning`, `OnCameraChange`, `OnMouseEnterFlap`/`OnMouseExitFlap`, `OnOrigamiStart`/`OnOrigamiEnd`, `OnPlayerDie`.

### 14.2 Glosario de términos de producción

- **Solapa**: elemento pop-up que se abre/cierra (mecánica de libro, §4.4).
- **Sello**: pedestal de origami (§4.3).
- **HojaMaster**: la página física que gira durante un page-turn (§8.2).
- **RidingPage**: estado de Kami enganchada visualmente al borde de la hoja durante el giro.
- **IMojable**: interfaz de "puede mojarse" (río, ahogo).
- **ICortable**: interfaz de "puede cortarse" (tijera).
- **IGolpeable**: interfaz de "puede recibir daño" (combate).

### 14.3 Índice de documentación técnica relacionada
Este GDD es el documento de **diseño**; el detalle de **implementación** vive en:
- [`docs/claude/spine-kami.md`](claude/spine-kami.md) — Spine, tracks, animaciones, skins
- [`docs/claude/audio-y-particulas.md`](claude/audio-y-particulas.md) — AudioManager, partículas, glosario anti-bug
- [`docs/claude/origami-y-tooltips.md`](claude/origami-y-tooltips.md) — canvas de costo, tooltips/post-its
- [`docs/claude/paginas-y-hoja.md`](claude/paginas-y-hoja.md) — HojaMaster, PositionMarker, RidingPage
- [`docs/claude/enemigos-e-ia.md`](claude/enemigos-e-ia.md) — Rocoso, PatrollingAgent/GallinaAgent, Barquito
- [`docs/claude/nivel2-y-ui.md`](claude/nivel2-y-ui.md) — Nivel 2, LevelManager, Inventario, Flap/CamWheel, Cámara
- [`docs/claude/quests-y-dialogos.md`](claude/quests-y-dialogos.md) — Quests y diálogos de NPCs
- [`ROADMAP.md`](../ROADMAP.md) — plan de trabajo vigente
- [`.specify/memory/constitution.md`](../.specify/memory/constitution.md) — principios del proyecto para Spec Kit

### 14.4 Secciones pendientes de contenido (resumen de todos los ⚪ que quedan en este doc)
La narrativa profunda quedó resuelta en la v0.3 (§2, §3, §2.7 — las 8 disidencias narrativa↔implementación ya están decididas). Lo que sigue realmente pendiente:
- Público objetivo y referencias de mercado (§1.3/1.4).
- Dirección de arte consolidada más allá de Spine/HojaMaster (§8.3/8.4).
- Balance/curva de dificultad numérica (§10.2).
- Accesibilidad (§7.5).
- Guion propio del Nivel 3 en español (hoy solo existe en el script en inglés, §2.6).
- Diseño en detalle de la 2ª sidequest del Nivel 2 (minijuego de ritmo, idea anotada en §2.5/§2.7 punto 7, falta especificar mecánica, scoring, etc.).
- Mecanismo con el que el juego encadena Nivel 1 → Nivel 2 sin la cutscene del guion (§2.7 punto 4).
- Confirmar si el placeholder "Nivel 3" (`level3-scenes`) es en efecto "Kami Fotos y Recuerdos" (§5.3), y destino de la branch `level4-scenes` ya descartada narrativamente (§5.4).
- Riesgos de producción a mediano plazo (§13.3).
