# Spanish opening-mark audit (¿ ¡) - spec 024 task 1.D

Generated read-only from `Assets/Localization Settings/Tables/*_es.asset` (keys mapped through each `* Shared Data.asset`). Strings are shown decoded; `\n` marks a line break; a trailing space in the source is kept but is invisible inside the backticks.

## Summary

| table | entries | already using ¿/¡ | need fixing | of which Level 2 |
|---|---|---|---|---|
| DialogueTable_es | 170 | 48 | 38 | 0 |
| ItemTable_es | 30 | 0 | 3 | 0 |
| Level2_Dialogues_es | 2 | 0 | 0 | 0 |
| LogoTable_es | 3 | 0 | 0 | 0 |
| QuestTable_es | 16 | 0 | 2 | 0 |
| StringTable_es | 0 | 0 | 0 | 0 |
| TooltipTable_es | 21 | 1 | 7 | 0 |
| UITexts_es | 37 | 1 | 6 | 0 |

**Total strings to fix: 56** (all Level 1 / menu / UI; no Level 2 string is missing a mark).

Notes:
- Level 2 dialogue lives inside `DialogueTable_es` (keys `natalia_*`, `grace_*`, `museum_*`, ...); 48 of its entries already use ¿/¡ and none is missing one. `Level2_Dialogues_es` only holds 2 non-dialogue text entries (the "friend" note and the café ticket) and needs nothing. `StringTable_es` is empty, `LogoTable_es` has nothing to fix.
- Level 2 strings that already carry marks outside DialogueTable: `TooltipTable_es` `catapult_cut_rope`, `UITexts_es` `origami_fail_retry`.
- Skipped on purpose: `unknown_name` ("???").
- `QuestTable` key `tiburcioquest` has a trailing newline in its key in the shared data (`tiburcioquest\n`); shown trimmed here, not touched.
- Placement judgement calls: `dalia_02_01` opens mid-sentence ("Kami, ¿no podrás...?"), `chino_01_02` too ("Che, ¿sabés que...?"), `nivel1_end_03` uses the combined form `¡¿Qué hiciste?!`. Everything else opens at the start of its sentence.
- `norberto_04_01` "Qué ingenioso traer a la Abuela doblada." is an exclamation written with a period, left as is.
- Proposals were produced mechanically (opening mark at the start of each sentence ending in ? or !) and then reviewed by eye; a few `!` that read more like statements (e.g. `abuela_01_03`, `dalia_03_02` "Sé que sos..."), still get ¡ because the source ends them with `!`.

## TMP font check (¿ = U+00BF 191, ¡ = U+00A1 161)

| font asset | path | population | has 191 | has 161 | used by |
|---|---|---|---|---|---|
| Vanilla_Caramel SDF | `Assets/TextMesh Pro/Fonts/Vanilla_Caramel SDF.asset` | static (0), 199 chars | yes | yes | the main UI/dialogue font: DialogueGlobe, FlapManager, PostIt, QuestSlot, InventorySlot, VictoryOverlay, MainMenu, OrigamiTextRevealPanel... (38 prefab/scene references) |
| Cutscene | `Assets/TextMesh Pro/Fonts/Cutscene.asset` | static (0), 199 chars | yes | yes | end cutscenes (Nivel1_EndCutscene, Level2_EndCutscene) and the origami `CurrentFold` prefab |
| Nice Sugar SDF | `Assets/TextMesh Pro/Fonts/Nice Sugar SDF.asset` | static (0), 95 chars (ASCII only) | NO | NO | only `Loading Animation Canvas.prefab` ("Loading..."); no accents either (no é/ñ/á), irrelevant to Spanish text |
| LiberationSans SDF | `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` | static (0), 250 chars | yes | yes | TMP default font (TMP Settings), no explicit references |
| LiberationSans SDF - Fallback | same folder | dynamic (1), 0 chars, source font LiberationSans | not baked (dynamic: can add them at runtime, the source TTF has both) | same | fallback of LiberationSans SDF, no references |

Conclusion: the fonts used for dialogue and UI (Vanilla_Caramel, Cutscene) already contain both glyphs. Proof in play: 48 `DialogueTable_es` entries (Level 2) plus `catapult_cut_rope` and `origami_fail_retry` already use them in those fonts. No font work is needed to add the marks. (Static atlases: if a glyph is ever absent it renders as a missing-glyph square; not the case here.)

## DialogueTable_es (38 to fix, of 170 entries)

| key | area | current | proposed |
|---|---|---|---|
| dialogue_intro_03 | Level 1 intro | `Empezamos?` | `¿Empezamos?` |
| dialogue_intro_05 | Level 1 intro | `Lo conocés? ` | `¿Lo conocés? ` |
| dialogue_intro_06 | Level 1 intro | `Claro que lo conocés! Si este es tu libro! Te acordás como empieza? ` | `¡Claro que lo conocés! ¡Si este es tu libro! ¿Te acordás como empieza? ` |
| abuela_01_02 | Level 1 NPC | `Kami! Qué bueno que viniste! Me caí del barranco y no sé cómo volver.` | `¡Kami! ¡Qué bueno que viniste! Me caí del barranco y no sé cómo volver.` |
| abuela_01_03 | Level 1 NPC | `Encima hay un Rocoso de graande! Por suerte está dormido.` | `¡Encima hay un Rocoso de graande! Por suerte está dormido.` |
| abuela_01_04 | Level 1 NPC | `Por qué no cortás la represa y le das un buen chapuzón? :)` | `¿Por qué no cortás la represa y le das un buen chapuzón? :)` |
| abuela_02_01 | Level 1 NPC | `Qué grande mi nietita! Ahora vamos para casa que tengo un hammmbre!` | `¡Qué grande mi nietita! ¡Ahora vamos para casa que tengo un hammmbre!` |
| abuela_02_03 | Level 1 NPC | `Ay, no sabés cómo me duele la espalda! No me doblás y me llevás en tu morralcito?` | `¡Ay, no sabés cómo me duele la espalda! ¿No me doblás y me llevás en tu morralcito?` |
| abuela_03_01 | Level 1 NPC | `Muchas gracias por traerme Kami. Bueno, ya! Vamos a comer!` | `Muchas gracias por traerme Kami. ¡Bueno, ya! ¡Vamos a comer!` |
| chino_01_01 | Level 1 NPC | `Buenas buenas! Mirá quién llegó!` | `¡Buenas buenas! ¡Mirá quién llegó!` |
| chino_01_02 | Level 1 NPC | `Che, sabés que no estoy pudiendo prender el horno? Está muy fuerte el viento hoy...` | `Che, ¿sabés que no estoy pudiendo prender el horno? Está muy fuerte el viento hoy...` |
| chino_01_03 | Level 1 NPC | `Podés traerme 100 papeles? No hace falta que los traigas ahora. Podés ir y volver entre páginas cuando quieras.` | `¿Podés traerme 100 papeles? No hace falta que los traigas ahora. Podés ir y volver entre páginas cuando quieras.` |
| chino_03_01 | Level 1 NPC | `Bien ahí! Muchas gracias por los papeles.` | `¡Bien ahí! Muchas gracias por los papeles.` |
| chino_04_01 | Level 1 NPC | `Listo, ahora podés cortar piedras! \nEspero que te sirva!` | `¡Listo, ahora podés cortar piedras! \n¡Espero que te sirva!` |
| dalia_01_01 | Level 1 NPC | `Buen día Kami!` | `¡Buen día Kami!` |
| dalia_01_03 | Level 1 NPC | `Estoy haciendo unas pociones especiales pero me quedé sin flores!` | `¡Estoy haciendo unas pociones especiales pero me quedé sin flores!` |
| dalia_01_04 | Level 1 NPC | `Son grandotas y llamativas, pero no puedo dejar mi puestito para ir a buscarlas! Vos podrás traerme tres de ellas?` | `¡Son grandotas y llamativas, pero no puedo dejar mi puestito para ir a buscarlas! ¿Vos podrás traerme tres de ellas?` |
| dalia_02_01 | Level 1 NPC | `Kami, no podrás traerme tres flores por favor? ` | `Kami, ¿no podrás traerme tres flores por favor? ` |
| dalia_03_01 | Level 1 NPC | `Gracias por traerme las flores! Sos lo más!` | `¡Gracias por traerme las flores! ¡Sos lo más!` |
| dalia_03_02 | Level 1 NPC | `Por haber sido taan buena conmigo te voy a regalar estos papeles. Sé que sos re talentosa con los origamis, seguro te sirven para algo!` | `Por haber sido taan buena conmigo te voy a regalar estos papeles. ¡Sé que sos re talentosa con los origamis, seguro te sirven para algo!` |
| dalia_04_01 | Level 1 NPC | `Gracias por traerme las flores!` | `¡Gracias por traerme las flores!` |
| norberto_01_01 | Level 1 NPC | `Kami, Kami! Me tenés que ayudar!` | `¡Kami, Kami! ¡Me tenés que ayudar!` |
| norberto_01_02 | Level 1 NPC | `La abuela se perdió de nuevo. Se fue a hacer su caminata y todavía no volvió!` | `La abuela se perdió de nuevo. ¡Se fue a hacer su caminata y todavía no volvió!` |
| norberto_01_03 | Level 1 NPC | `Podés ir a buscarla? Yo acabo de tirar los choris y tengo miedo que se me quemen.` | `¿Podés ir a buscarla? Yo acabo de tirar los choris y tengo miedo que se me quemen.` |
| norberto_02_01 | Level 1 NPC | `Usá tu tijera y tu magia de origami para encontrar a la abuela y traerla!` | `¡Usá tu tijera y tu magia de origami para encontrar a la abuela y traerla!` |
| norberto_03_01 | Level 1 NPC | `Volviste a buscar tus Botas de Lectura Rápida, no? Las dejaste acá. Con estas podés ir más rápido por el libro.` | `¿Volviste a buscar tus Botas de Lectura Rápida, no? Las dejaste acá. Con estas podés ir más rápido por el libro.` |
| norberto_03_04 | Level 1 NPC | `Y? Encontraste a la abuela? Dónde está?` | `¿Y? ¿Encontraste a la abuela? ¿Dónde está?` |
| norberto_04_01 | Level 1 NPC | `Qué ingenioso traer a la Abuela doblada. Nunca vi eso!` | `Qué ingenioso traer a la Abuela doblada. ¡Nunca vi eso!` |
| tiburcio_01_01 | Level 1 NPC | `Kami, Kami! Me tenés que ayudar!` | `¡Kami, Kami! ¡Me tenés que ayudar!` |
| tiburcio_01_02 | Level 1 NPC | `Las gallinas quedaron atrapadas del otro lado del río! ` | `¡Las gallinas quedaron atrapadas del otro lado del río! ` |
| tiburcio_01_03 | Level 1 NPC | `Vos podrías cortar el árbol para hacerles un puente?` | `¿Vos podrías cortar el árbol para hacerles un puente?` |
| tiburcio_02_01 | Level 1 NPC | `Necesito que cortes ese árbol!` | `¡Necesito que cortes ese árbol!` |
| tiburcio_03_01 | Level 1 NPC | `Gracias! Mirá, las gallinas ya están cruzando. Tomá esto como recompensa.` | `¡Gracias! Mirá, las gallinas ya están cruzando. Tomá esto como recompensa.` |
| tiburcio_03_02 | Level 1 NPC | `[Ahora podés caminar sobre el agua!]` | `[¡Ahora podés caminar sobre el agua!]` |
| tiburcio_04_01 | Level 1 NPC | `Gracias por darme una mano Kami! Las gallinas están felices en el rancho.` | `¡Gracias por darme una mano Kami! Las gallinas están felices en el rancho.` |
| nivel1_end_03 | Level 1 end cutscene | `Qué hiciste?! Vení para acá.` | `¡¿Qué hiciste?! Vení para acá.` |
| nivel1_end_04 | Level 1 end cutscene | `Qué es eso de hacer origamis de los demás personajes?` | `¿Qué es eso de hacer origamis de los demás personajes?` |
| nivel1_end_05 | Level 1 end cutscene | `No podés andar cambiando las historias! Se suponía que la abuela no lograba salir de esa página...` | `¡No podés andar cambiando las historias! Se suponía que la abuela no lograba salir de esa página...` |

## ItemTable_es (3 to fix, of 30 entries)

| key | area | current | proposed |
|---|---|---|---|
| abuela_text | Level 1 item | `"Dale querida metele que tengo hambre!"` | `"¡Dale querida metele que tengo hambre!"` |
| boots_rain_text | Level 1 item | `Si sobre agua estás caminando, con estas botas de mojarte te podés ir olvidando!` | `¡Si sobre agua estás caminando, con estas botas de mojarte te podés ir olvidando!` |
| scissors_text | Level 1 item | `CLIC para cortar!\n Estas tijeras eran de tu madre.` | `¡CLIC para cortar!\n Estas tijeras eran de tu madre.` |

## Level2_Dialogues_es

No entries need changes (2 entries).

## LogoTable_es

No entries need changes (3 entries).

## QuestTable_es (2 to fix, of 16 entries)

| key | area | current | proposed |
|---|---|---|---|
| mainquest_title | Level 1 quest | `Traé de vuelta a la abuela!` | `¡Traé de vuelta a la abuela!` |
| tiburcioquest | Level 1 quest | `Las gallinas de Tiburcio tienen hongos en la cabeza! Cortá 3 y lleváselos a Tiburcio.` | `¡Las gallinas de Tiburcio tienen hongos en la cabeza! Cortá 3 y lleváselos a Tiburcio.` |

## StringTable_es

No entries need changes (0 entries).

## TooltipTable_es (7 to fix, of 21 entries)

| key | area | current | proposed |
|---|---|---|---|
| abuela_dropoff | Level 1 tooltip | `Dejaste a la abuela en casa!` | `¡Dejaste a la abuela en casa!` |
| gallina_entry | Level 1 tooltip | `La gallina intenta esquivarte!` | `¡La gallina intenta esquivarte!` |
| water_warning | Level 1 tooltip | `Cuidado con el agua! Usá el pedestal de origami para doblar un puente` | `¡Cuidado con el agua! Usá el pedestal de origami para doblar un puente` |
| rocoso_water | Level 1 tooltip | `Cuidado con el agua! \nSaltá usando ESPACIO.` | `¡Cuidado con el agua! \nSaltá usando ESPACIO.` |
| rocoso_warning | Level 1 tooltip | `Cuidado con los Rocosos! Resisten tus tijeras... Pero no un arbolazo en la cabeza.` | `¡Cuidado con los Rocosos! Resisten tus tijeras... Pero no un arbolazo en la cabeza.` |
| rocoso_wakeup | Level 1 tooltip | `Despertaste al Rocoso!` | `¡Despertaste al Rocoso!` |
| origami_guide | Level 1 tooltip | `Arrastrá hasta el final y soltá. \nNo te salgas del camino!` | `Arrastrá hasta el final y soltá. \n¡No te salgas del camino!` |

## UITexts_es (6 to fix, of 37 entries)

| key | area | current | proposed |
|---|---|---|---|
| areyousure_title | UI / menu | `Querés salir en serio? :(` | `¿Querés salir en serio? :(` |
| overlay_victory | UI / menu | `Encontraste a la abuela pero tal vez queden secretos por revelar.\nQuerés terminar este libro o preferís quedarte a explorar?` | `Encontraste a la abuela pero tal vez queden secretos por revelar.\n¿Querés terminar este libro o preferís quedarte a explorar?` |
| DefeatDrowning | UI / menu | `Caíste al agua!\nTocá E para reiniciar.` | `¡Caíste al agua!\nTocá E para reiniciar.` |
| DefeatRocoso | UI / menu | `El Rocoso te derrotó!\nTocá E para reiniciar.` | `¡El Rocoso te derrotó!\nTocá E para reiniciar.` |
| DefeatCaught | UI / menu | `Te atraparon!\nTocá E para reiniciar.` | `¡Te atraparon!\nTocá E para reiniciar.` |
| DefeatCar | UI / menu | `Te atropelló un auto!\nTocá E para reiniciar.` | `¡Te atropelló un auto!\nTocá E para reiniciar.` |
