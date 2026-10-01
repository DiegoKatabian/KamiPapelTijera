"""All community-facing text and AutoMod lists for the Kami Paper Scissors Discord.

Server text is Spanish (neutral Latin American, "tuteo") and English on purpose: it is product
content for the community, not repo code. See specs/010-discord-community/spec.md.

Discord limits enforced by setup.py before any API call (check_content()):
  * message 2000 chars, channel topic 1024, AutoMod custom message 150,
  * 10 regex patterns per AutoMod rule, 260 chars per pattern.
"""

# The address kids and parents are told to write to. One place to change it (spec 010).
REPORT_EMAIL = "diego.katabian@kimmiarts.com"

TEAM_ROLE_NAME = "Kami Team"
ARTIST_ROLE_NAME = "Paper Artist"
# Language roles, given by the Onboarding question (they unlock the ESPAÑOL / ENGLISH category).
SPANISH_ROLE_NAME = "Español"
ENGLISH_ROLE_NAME = "English"

# The Onboarding question every new member answers. The third option gives both roles.
ONBOARDING_TITLE = "Idioma / Language"
ONBOARDING_OPTIONS = [
    ("Español", "Veo los canales en español."),
    ("English", "I see the English channels."),
    ("Ambos / Both", "Veo los dos idiomas / I see both languages."),
]

# --------------------------------------------------------------------------------------
# #welcome  (ES message, then EN message)
# --------------------------------------------------------------------------------------

WELCOME_ES = f"""\
✂️ **¡Bienvenido/a al server de Kami Paper Scissors!**

Kami es una chica de 13 años que vive en un mundo hecho enteramente de papel, con una tijera enorme y una magia muy especial: el **origami**. Este es el lugar para hablar del juego, de **manualidades de papel** y de **arte en general**.

🌱 **¿Primera vez aquí?**
• Lee <#{{rules}}> (son pocas y cortas).
• Cuéntanos quién eres en <#{{presentaciones}}>. No hace falta dar datos reales: un apodo basta.
• Muestra lo que hiciste en **CREATE**: <#{{fan-art}}>, <#{{paper-crafts}}> y <#{{art-general}}>.

🌐 **Elige tu idioma:** al entrar se te pregunta *Idioma / Language*. Según lo que elijas verás los canales en **ESPAÑOL**, en **ENGLISH** o en los dos. Puedes cambiarlo cuando quieras en **Canales y roles**, arriba de la lista de canales.
👧 Este server es para personas de **13 años o más**.
🔒 **Nadie del equipo te va a escribir por privado.** Si alguien dice ser del equipo y te escribe por mensaje directo, no es cierto: avísanos.

¡Qué bueno que estés aquí! 💙
— Diego y Valentino (kimmiarts)
"""

WELCOME_EN = f"""\
✂️ **Welcome to the Kami Paper Scissors server!**

Kami is a 13-year-old girl who lives in a world made entirely of paper, with a giant pair of scissors and a very special kind of magic: **origami**. This is the place to talk about the game, **paper crafts** and **art in general**.

🌱 **First time here?**
• Read <#{{rules}}> (there are only a few, and they're short).
• Tell us who you are in <#{{introductions}}>. No real details needed: a nickname is plenty.
• Show what you made in **CREATE**: <#{{fan-art}}>, <#{{paper-crafts}}> and <#{{art-general}}>.

🌐 **Pick your language:** when you join you are asked *Idioma / Language*. What you choose decides whether you see the **ESPAÑOL** channels, the **ENGLISH** ones, or both. You can change it any time in **Channels & Roles**, at the top of the channel list.
👧 This server is for people **aged 13 or older**.
🔒 **Nobody on the team will ever message you privately.** If someone says they're on the team and sends you a direct message, that isn't true: tell us.

So glad you're here! 💙
— Diego & Valentino (kimmiarts)
"""

# --------------------------------------------------------------------------------------
# #rules
# --------------------------------------------------------------------------------------

RULES_ES = f"""\
📋 **Reglas del server**

**1. Ten 13 años o más.** Es la edad mínima de Discord. Si eres menor de la edad mínima de tu país, no puedes estar aquí.

**2. Sé amable.** Nada de insultos, burlas, acoso, odio ni discriminación. Aquí venimos a pasarla bien y a aprender unos de otros.

**3. Cuida tus datos y los de los demás.** No compartas nombres reales, apellidos, edad exacta, escuela, dirección, teléfono, email, redes sociales ni fotos tuyas o de otras personas. Un apodo es suficiente.

**4. Nadie del equipo te escribe por privado.** Tampoco te pedimos fotos, datos ni "secretos". Todo se habla en los canales. Si alguien (del equipo o no) te pide hablar en privado, te pide fotos o te hace sentir incómodo/a, **no le respondas y avísanos**.

**5. Nada para adultos.** Ni contenido sexual, ni violencia fuerte, ni cosas que den miedo. Si dudas, no lo publiques.

**6. Arte propio y con crédito.** En CREATE sube **solo cosas que hiciste tú**, y menciona en qué te inspiraste. No se permiten imágenes hechas con inteligencia artificial. Nada de fotos de caras, y cuida que no se vean datos personales (nombres en papeles, uniformes de escuela, direcciones).

**7. Sin links ni publicidad.** No se pueden enviar links, invitaciones a otros servers ni promocionar cosas.

**8. Un idioma por categoría.** Al entrar eliges tu idioma y ves **ESPAÑOL**, **ENGLISH** o las dos. En cada categoría se habla su idioma. En CREATE puedes escribir en el que quieras, siempre con respeto.
"""

RULES_ES_2 = f"""\
**9. Si algo te hace sentir mal, cuéntaselo a un adulto de confianza.** Y avísanos:
• Usa **Reportar** en el mensaje (mantén apretado el mensaje, o clic derecho).
• Escribe @{TEAM_ROLE_NAME} en cualquier canal.
• O escribe a **{REPORT_EMAIL}** (si eres menor, pide ayuda a un adulto).
Si estás en peligro, llama al número de emergencias de tu país.

**10. El equipo decide.** Podemos borrar mensajes, silenciar o expulsar a alguien. Primero te avisaremos, salvo que sea algo grave: en ese caso actuamos de inmediato.

🎨 **Sobre tu arte:** publicar en este server **no nos da permiso** de usarlo en otro lado. Si queremos mostrar una obra tuya (redes, tráiler, el juego), te lo pediremos **en el canal, a la vista de todos**, y si eres menor de 18 necesitaremos que un padre, madre o tutor nos lo autorice por email.

Al aceptar estas reglas prometes cumplirlas. ¡Gracias por cuidar este lugar! 💙
"""

RULES_EN = f"""\
📋 **Server rules**

**1. Be 13 or older.** That's Discord's minimum age. If you're under the minimum age in your country, you can't be here.

**2. Be kind.** No insults, mocking, harassment, hate or discrimination. We're here to have fun and learn from each other.

**3. Protect your info and other people's.** Don't share real names, last names, exact age, school, address, phone, email, social media or photos of yourself or others. A nickname is enough.

**4. Nobody on the team messages you privately.** We also never ask for photos, personal info or "secrets". Everything happens in the channels. If anyone (team or not) asks you to talk privately, asks for photos, or makes you uncomfortable, **don't answer and tell us**.

**5. Nothing for adults.** No sexual content, no strong violence, nothing scary. If you're not sure, don't post it.

**6. Your own art, with credit.** In CREATE, post **only things you made yourself**, and mention what inspired you. AI-generated images are not allowed. No photos of faces, and make sure no personal info shows (names on paper, school uniforms, addresses).

**7. No links or ads.** You can't send links, invites to other servers, or advertise things.

**8. One language per category.** When you join you pick your language and see **ESPAÑOL**, **ENGLISH** or both. Each category uses its own language. In CREATE you can write in any language, always respectfully.
"""

RULES_EN_2 = f"""\
**9. If something makes you feel bad, tell a trusted adult.** And tell us:
• Use **Report** on the message (long-press it, or right click).
• Type @{TEAM_ROLE_NAME} in any channel.
• Or write to **{REPORT_EMAIL}** (if you're a minor, ask an adult to help).
If you're in danger, call your country's emergency number.

**10. The team decides.** We can delete messages, mute or remove someone. We'll warn you first, unless it's something serious: then we act right away.

🎨 **About your art:** posting here **doesn't give us permission** to use it elsewhere. If we'd like to show your work (social media, trailer, the game), we'll ask **in the channel, in the open**, and if you're under 18 we'll need a parent or guardian to approve it by email.

By accepting these rules you promise to follow them. Thanks for taking care of this place! 💙
"""

# Short bilingual lines for Discord's "rules screening" gate (each line <= 300 chars).
SCREENING_DESCRIPTION = (
    "Kami Paper Scissors: un lugar para el juego, el papel y el arte. / "
    "A place for the game, paper crafts and art."
)
SCREENING_RULES = [
    "Tengo 13 años o más. / I am 13 or older.",
    "Seré amable con todos. / I will be kind to everyone.",
    "No compartiré datos personales ni fotos mías ni de otras personas. / "
    "I won't share personal info or photos of me or anyone else.",
    "Nadie del equipo me escribe por privado: si alguien lo hace, avisaré. / "
    "The team never messages me privately: if someone does, I'll tell them.",
    "En CREATE subo solo arte hecho por mí, sin IA. / In CREATE I only post my own art, no AI.",
    "No enviaré links ni invitaciones a otros servers. / I won't send links or invites to other servers.",
]

# --------------------------------------------------------------------------------------
# #what-is-kami
# --------------------------------------------------------------------------------------

WHAT_IS_ES = """\
🎮 **¿Qué es Kami: Papel y Tijera?**

Un videojuego de aventuras para PC donde **todo está hecho de papel**: personajes, casas, arbustos, árboles… como en un libro pop-up.

✂️ **Kami** tiene 13 años y una tijera enorme con la que puede cortar casi cualquier cosa. Es muy buena, aunque un poco torpe con los pies.
🦢 Su magia es el **origami**: puede doblar el papel del mundo para crear herramientas, vehículos y hasta aliados.
📖 Cada nivel es un **libro distinto**, con su propio mundo, y Kami intenta volver a casa a través de ellos.

**Aquí puedes**
• Charlar del juego y pedir ayuda.
• Compartir tus **manualidades de papel** y tu **arte**.
• Seguir las novedades en <#{devlog}> y <#{announcements}>.

El juego está en desarrollo. Cuando haya algo nuevo, lo vas a ver primero aquí. 💙
"""

WHAT_IS_EN = """\
🎮 **What is Kami: Paper Scissors?**

An adventure game for PC where **everything is made of paper**: characters, houses, bushes, trees… just like in a pop-up book.

✂️ **Kami** is 13 years old and carries a huge pair of scissors that can cut almost anything. She's great with them, if a little clumsy on her feet.
🦢 Her magic is **origami**: she can fold the world's paper into tools, vehicles and even allies.
📖 Every level is a **different book** with its own world, and Kami is trying to find her way home through them.

**Here you can**
• Chat about the game and ask for help.
• Share your **paper crafts** and your **art**.
• Follow the news in <#{devlog}> and <#{announcements}>.

The game is still in development. When there's something new, you'll see it here first. 💙
"""

# --------------------------------------------------------------------------------------
# Channel topics (shown under the channel name)
# --------------------------------------------------------------------------------------

TOPICS = {
    "welcome": "Bienvenida / Welcome. 13+ only.",
    "rules": "Reglas / Rules. Léelas antes de escribir / Read them before posting.",
    "announcements": "Novedades del equipo / News from the team (ES + EN).",
    "what-is-kami": "¿Qué es Kami? / What is Kami?",
    "devlog": "Cómo vamos con el juego / Dev updates (ES + EN). Puedes reaccionar / React freely.",
    "general-es": "Charla general en español. Sin datos personales, sin links. Ver #rules.",
    "presentaciones": "Preséntate con un apodo y cuéntanos qué te gusta. Sin datos personales.",
    "ayuda-del-juego": "¿Te trabaste en un nivel? Pregunta aquí (¡sin spoilers en el título!).",
    "general-en": "General chat in English. No personal info, no links. See #rules.",
    "introductions": "Introduce yourself with a nickname and tell us what you like. No personal info.",
    "game-help": "Stuck on a level? Ask here (no spoilers in the title!).",
    "fan-art": "Fan art de Kami / Kami fan art. Solo obras propias, sin IA / Your own work only, no AI.",
    "paper-crafts": "Manualidades de papel: origami, recortes, pop-ups / Paper crafts: origami, cutouts, pop-ups.",
    "art-general": "Todo tipo de arte / Any kind of art. Solo obras propias, sin IA / Your own work only, no AI.",
    "mod-log": "Alertas de AutoMod y reportes. Solo el equipo.",
    "team-chat": "Charla del equipo.",
}

# --------------------------------------------------------------------------------------
# AutoMod
# --------------------------------------------------------------------------------------

# Shown to the member whose message is blocked (<= 150 chars).
AUTOMOD_BLOCK_MESSAGE = (
    "Ese mensaje no está permitido aquí. / That message isn't allowed here. "
    f"Dudas / Questions: @{TEAM_ROLE_NAME}"
)[:150]

# Custom profanity, ES + PT. English, sexual content and slurs are covered by Discord's preset.
# Entries WITHOUT wildcards match whole words only. That matters: "puta" as a wildcard would hit
# "computadora". Add words here and re-run setup.py; Diego curates this list.
PROFANITY = [
    # Spanish
    "puta", "puto", "putas", "putos", "hdp", "hijo de puta", "hija de puta", "mierda",
    "pija", "pijas", "verga", "vergas", "concha de tu madre", "conchudo", "pelotudo", "pelotuda",
    "forro", "forra", "carajo", "cojudo", "cabron", "cabrón", "gilipollas", "joder",
    "culiao", "culiado", "weon", "huevon", "hijueputa", "malparido",
    # Portuguese
    "porra", "caralho", "merda", "foda-se", "fodase", "buceta", "filho da puta", "viado",
    "cuzao", "cuzão", "babaca",
]

# Contact info and "take it somewhere else" attempts. Regex is Rust-flavoured on Discord's side:
# no lookarounds, no backreferences. Keep patterns simple.
CONTACT_REGEX = [
    # email
    r"(?i)[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}",
    # phone: 8+ digits, allowing spaces, dots, dashes and parentheses between them
    r"\+?\d(?:[\s.\-()]*\d){7,}",
    # "my insta", "mi snap", "meu tiktok"... Only app names: "tele" (TV), "face", "ig" would hit
    # ordinary words like "mi tele", "your face", "tu iglesia".
    r"(?i)\b(?:mi|my|meu|minha|mis|tu|tus|your)\s+(?:insta\w*|snap\w*|tik\s?tok|whats?\s?app|wsp|wpp|telegram|kik|skype)",
    # "insta: foo", "whatsapp = 123", "snap @foo"
    r"(?i)\b(?:insta(?:gram)?|snap(?:chat)?|tik\s?tok|whats?\s?app|wsp|wpp|telegram|kik|skype)\s*[:=@\-]\s*\S+",
]
# No "*wsp*": it would hit "ne-wsp-aper". Short forms live in the regexes, which use word boundaries.
CONTACT_KEYWORDS = ["*whatsapp*", "*telegram*", "*snapchat*"]

# Phrases that no one should be sending to anyone in a kids' server. They are blocked AND alerted:
# a hit here is a signal for the mods, not just a filtered word. False positives are expected
# (kids asking each other their age is normal) and the cost of one is a short delay.
SAFETY_PHRASES = [
    # English
    "*how old are you*", "*what's your age*", "*whats your age*", "*send me a pic*", "*send a pic*",
    "*send nudes*", "*are you alone*", "*are your parents home*", "*dont tell your parents*",
    "*don't tell your parents*", "*our little secret*", "*our secret*", "*dm me*", "*text me*",
    "*call me*", "*where do you live*", "*what school do you go*", "*your address*",
    "*your number*", "*add me on*", "*video call*", "*private chat*",
    # Spanish
    "*cuantos años tienes*", "*cuántos años tienes*", "*cuantos años tenes*", "*cuántos años tenés*",
    "*cuantos años tenes*", "*qué edad tienes*", "*que edad tienes*", "*mandame una foto*",
    "*mándame una foto*", "*pasame una foto*", "*pásame una foto*", "*manda foto*", "*estás solo*",
    "*estas solo*", "*estás sola*", "*estas sola*", "*no le digas a tus papás*",
    "*no le digas a tus papas*", "*no le digas a tus padres*", "*es nuestro secreto*",
    "*por privado*", "*al privado*", "*por dm*", "*escríbeme*", "*escribime*", "*dónde vives*",
    "*donde vives*", "*dónde vivís*", "*donde vivis*", "*a qué escuela vas*", "*a que escuela vas*",
    "*tu dirección*", "*tu direccion*", "*tu número*", "*tu numero*", "*videollamada*",
    # Portuguese
    "*quantos anos você tem*", "*quantos anos voce tem*", "*manda uma foto*", "*me manda foto*",
    "*você está sozinho*", "*voce esta sozinho*", "*não conte para seus pais*",
    "*nao conte para seus pais*", "*é nosso segredo*", "*e nosso segredo*", "*no privado*",
]

# Links: every link is blocked except the allow-list. (GIF pickers are off in v1: links do not
# unfold because @everyone has no "Embed Links".)
LINK_KEYWORDS = ["*http://*", "*https://*", "*www.*"]
LINK_ALLOW_LIST = ["*kimmiarts.com*", "*store.steampowered.com*"]

# Invitations to other servers.
INVITE_KEYWORDS = [
    "*discord.gg*", "*discord.com/invite*", "*discordapp.com/invite*", "*dsc.gg*", "*discord.me*",
]


def check_content(named_messages):
    """Fail loudly, before touching Discord, if any text breaks a Discord length limit."""
    problems = []
    for name, text in named_messages.items():
        limit = 1024 if name.startswith("topic:") else 2000
        if len(text) > limit:
            problems.append(f"{name}: {len(text)} chars (limit {limit})")
    if len(AUTOMOD_BLOCK_MESSAGE) > 150:
        problems.append(f"automod message: {len(AUTOMOD_BLOCK_MESSAGE)} chars (limit 150)")
    for rule_name, patterns in (("contact", CONTACT_REGEX),):
        if len(patterns) > 10:
            problems.append(f"{rule_name}: {len(patterns)} regex patterns (limit 10)")
        for pattern in patterns:
            if len(pattern) > 260:
                problems.append(f"{rule_name}: regex over 260 chars: {pattern[:40]}...")
    for rule_name, words in (
        ("custom words", PROFANITY), ("contact keywords", CONTACT_KEYWORDS),
        ("safety phrases", SAFETY_PHRASES), ("link keywords", LINK_KEYWORDS),
        ("invite keywords", INVITE_KEYWORDS),
    ):
        if len(words) > 1000:
            problems.append(f"{rule_name}: {len(words)} keywords (limit 1000)")
        problems += [f"{rule_name}: keyword over 60 chars: {w[:30]}..." for w in words if len(w) > 60]
    if len(LINK_ALLOW_LIST) > 100:
        problems.append(f"link allow-list: {len(LINK_ALLOW_LIST)} entries (limit 100)")
    if len(ONBOARDING_TITLE) > 100:
        problems.append("onboarding title over 100 chars")
    for title, description in ONBOARDING_OPTIONS:
        if len(title) > 50 or len(description) > 100:
            problems.append(f"onboarding option too long: {title!r}")
    for line in SCREENING_RULES:
        if len(line) > 300:
            problems.append(f"screening rule over 300 chars: {line[:40]}...")
    return problems
