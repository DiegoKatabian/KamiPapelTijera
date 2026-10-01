"""Builds the Kami Paper Scissors Discord server. One-shot, idempotent, REST only.

    python setup.py --dry-run          # print the whole plan, no token needed, no API calls
    python setup.py                    # build / repair the server
    python setup.py --wipe             # also delete every channel that is not in the plan
    python setup.py --invite           # also create one permanent invite to #welcome

The script is the source of truth: re-running it puts roles, channels, permissions and AutoMod
rules back to what is written here (anything added by hand to those objects is overwritten).
Design and rationale: specs/010-discord-community/spec.md.
"""

import argparse
import asyncio
import json
import os
import sys
from dataclasses import dataclass, field
from pathlib import Path

import discord
from discord import AutoModRuleAction, AutoModTrigger, PermissionOverwrite, Permissions

import content as C

# Windows consoles default to a legacy codepage that cannot print the Ñ in 'ESPAÑOL'.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).parent
WEBHOOKS_FILE = HERE / "discord-webhooks.json"  # gitignored: webhook URLs are secrets
WEBHOOK_NAME = "Kami Paper Scissors"
WEBHOOK_CHANNELS = ("announcements", "devlog")
AUTOMOD_PREFIX = "Kami: "
PAUSE = 0.4  # seconds between writes; discord.py also honours rate-limit headers

# ---------------------------------------------------------------------------------------
# Roles and permissions
# ---------------------------------------------------------------------------------------

# Whitelist, not blacklist: anything not listed here is OFF for @everyone. That removes attach
# files, embed links, external emoji/stickers/apps, threads, @everyone pings, invites and voice in
# one move. Channels turn back on only what they need (see overwrites_for).
EVERYONE_PERMISSIONS = Permissions(
    view_channel=True,
    send_messages=True,
    read_message_history=True,
    add_reactions=True,
    change_nickname=True,
)

# Moderators, deliberately NOT Administrator. Diego owns the server; Valentino gets this role.
TEAM_PERMISSIONS = Permissions(
    view_channel=True,
    send_messages=True,
    send_messages_in_threads=True,
    create_public_threads=True,
    read_message_history=True,
    add_reactions=True,
    attach_files=True,
    embed_links=True,
    use_external_emojis=True,
    use_application_commands=True,
    mention_everyone=True,
    manage_messages=True,
    manage_threads=True,
    manage_nicknames=True,
    kick_members=True,
    ban_members=True,
    moderate_members=True,
    view_audit_log=True,
    create_instant_invite=True,
    change_nickname=True,
)


@dataclass(frozen=True)
class RoleSpec:
    name: str
    color: int
    hoist: bool
    mentionable: bool
    permissions: Permissions


ROLES = [
    RoleSpec(C.TEAM_ROLE_NAME, 0x5BC0EB, True, True, TEAM_PERMISSIONS),
    # Cosmetic: the team awards it for fan art. No permissions beyond @everyone's.
    RoleSpec(C.ARTIST_ROLE_NAME, 0xF2C94C, False, False, Permissions.none()),
    # Language roles: handed out by the Onboarding question, they only decide which language
    # category a member can see. No permissions of their own (Onboarding refuses roles that have any).
    RoleSpec(C.SPANISH_ROLE_NAME, 0xF28B82, False, False, Permissions.none()),
    RoleSpec(C.ENGLISH_ROLE_NAME, 0x8AB4F8, False, False, Permissions.none()),
]


# ---------------------------------------------------------------------------------------
# Channel structure
# ---------------------------------------------------------------------------------------
# model: readonly  = team posts, nobody reacts
#        announce  = team posts, everyone reacts
#        open      = everyone talks, no attachments
#        creative  = everyone talks and attaches files (the ONLY place attachments are on)
#        open_es / open_en = like open, but only members with that language role (and the team) see it
#        team_only = invisible to everyone else


@dataclass(frozen=True)
class ChannelSpec:
    name: str
    model: str
    slowmode: int = 0


@dataclass(frozen=True)
class CategorySpec:
    name: str
    channels: tuple = field(default_factory=tuple)


STRUCTURE = [
    CategorySpec("START HERE", (
        ChannelSpec("welcome", "readonly"),
        ChannelSpec("rules", "readonly"),
        ChannelSpec("announcements", "announce"),
    )),
    CategorySpec("THE GAME", (
        ChannelSpec("what-is-kami", "readonly"),
        ChannelSpec("devlog", "announce"),
    )),
    CategorySpec("ESPAÑOL", (
        ChannelSpec("general-es", "open_es", slowmode=5),
        ChannelSpec("presentaciones", "open_es", slowmode=30),
        ChannelSpec("ayuda-del-juego", "open_es", slowmode=5),
    )),
    CategorySpec("ENGLISH", (
        ChannelSpec("general-en", "open_en", slowmode=5),
        ChannelSpec("introductions", "open_en", slowmode=30),
        ChannelSpec("game-help", "open_en", slowmode=5),
    )),
    CategorySpec("CREATE", (
        ChannelSpec("fan-art", "creative", slowmode=60),
        ChannelSpec("paper-crafts", "creative", slowmode=60),
        ChannelSpec("art-general", "creative", slowmode=60),
    )),
    CategorySpec("TEAM", (
        ChannelSpec("mod-log", "team_only"),
        ChannelSpec("team-chat", "team_only"),
    )),
]

# Channel -> pinned messages (posted in order, ES first). Matched to the bot's own pins by order,
# so a re-run edits them in place instead of duplicating.
PINS = {
    "welcome": [C.WELCOME_ES, C.WELCOME_EN],
    "rules": [C.RULES_ES, C.RULES_ES_2, C.RULES_EN, C.RULES_EN_2],
    "what-is-kami": [C.WHAT_IS_ES, C.WHAT_IS_EN],
}

ALL_CHANNEL_NAMES = [ch.name for cat in STRUCTURE for ch in cat.channels]

# What a new member lands on before choosing a language: the shared channels only. Discord shows
# these in the Onboarding flow and lists them under "Channels & Roles".
ONBOARDING_DEFAULT_CHANNELS = (
    "welcome", "rules", "announcements", "what-is-kami", "devlog", "fan-art", "paper-crafts", "art-general",
)


def overwrites_for(model, everyone, roles):
    """Permission overwrites for one channel (and its category). Explicit on purpose, so a re-run
    repairs drift instead of trusting whatever is there. `roles` maps role name -> Role."""
    team = roles[C.TEAM_ROLE_NAME]
    team_post = PermissionOverwrite(
        view_channel=True, send_messages=True, add_reactions=True, attach_files=True, embed_links=True
    )
    if model in ("open_es", "open_en"):
        language = roles[C.SPANISH_ROLE_NAME if model == "open_es" else C.ENGLISH_ROLE_NAME]
        talk = PermissionOverwrite(view_channel=True, send_messages=True, add_reactions=True)
        # @everyone's overwrite hides the channel from the team too, so the team is re-allowed.
        return {everyone: PermissionOverwrite(view_channel=False), language: talk, team: talk}
    if model == "readonly":
        return {everyone: PermissionOverwrite(send_messages=False, add_reactions=False), team: team_post}
    if model == "announce":
        return {everyone: PermissionOverwrite(send_messages=False, add_reactions=True), team: team_post}
    if model == "open":
        return {everyone: PermissionOverwrite(send_messages=True, add_reactions=True)}
    if model == "creative":
        return {
            everyone: PermissionOverwrite(send_messages=True, add_reactions=True, attach_files=True)
        }
    if model == "team_only":
        return {
            everyone: PermissionOverwrite(view_channel=False),
            team: PermissionOverwrite(view_channel=True, send_messages=True, read_message_history=True),
        }
    raise ValueError(f"Unknown permission model: {model}")


def category_model(category):
    """A category carries the permissions of its channels when they all agree; otherwise the
    stricter 'readonly' (START HERE mixes readonly and announce)."""
    models = {ch.model for ch in category.channels}
    return models.pop() if len(models) == 1 else "readonly"


# ---------------------------------------------------------------------------------------
# AutoMod
# ---------------------------------------------------------------------------------------


def build_automod_rules():
    """[(name, trigger)] for the 8 rules. Pure: builds discord.py objects, calls no API."""
    kw = AutoModTrigger
    kind = discord.AutoModRuleTriggerType
    return [
        ("profanity and slurs (preset)", kw(
            type=kind.keyword_preset,
            presets=discord.AutoModPresets(profanity=True, sexual_content=True, slurs=True),
        )),
        ("custom words ES/PT", kw(type=kind.keyword, keyword_filter=C.PROFANITY)),
        ("contact info", kw(
            type=kind.keyword,
            keyword_filter=C.CONTACT_KEYWORDS,
            regex_patterns=C.CONTACT_REGEX,
        )),
        ("safety phrases", kw(type=kind.keyword, keyword_filter=C.SAFETY_PHRASES)),
        ("links", kw(
            type=kind.keyword, keyword_filter=C.LINK_KEYWORDS, allow_list=C.LINK_ALLOW_LIST
        )),
        ("server invites", kw(type=kind.keyword, keyword_filter=C.INVITE_KEYWORDS)),
        ("spam", kw(type=kind.spam)),
        ("mention spam", kw(type=kind.mention_spam, mention_limit=5, mention_raid_protection=True)),
    ]


def automod_actions(mod_log_id):
    return [
        AutoModRuleAction(custom_message=C.AUTOMOD_BLOCK_MESSAGE),
        AutoModRuleAction(channel_id=mod_log_id),
    ]


# ---------------------------------------------------------------------------------------
# Content rendering and checks (all offline)
# ---------------------------------------------------------------------------------------


def render(text, channel_ids):
    """Fill the <#{channel}> placeholders. Keys can contain hyphens, str.format allows that."""
    return text.format(**channel_ids)


def all_named_texts(channel_ids):
    named = {}
    for channel, texts in PINS.items():
        for i, text in enumerate(texts):
            named[f"pin:{channel}[{i}]"] = render(text, channel_ids)
    for channel, topic in C.TOPICS.items():
        named[f"topic:{channel}"] = topic
    return named


def validate_offline():
    fake_ids = {name: 0 for name in ALL_CHANNEL_NAMES}
    problems = C.check_content(all_named_texts(fake_ids))
    missing_topics = [n for n in ALL_CHANNEL_NAMES if n not in C.TOPICS]
    problems += [f"no topic for #{n}" for n in missing_topics]
    # Constructing the AutoMod objects surfaces bad arguments before we touch the API.
    build_automod_rules()
    # Every permission model must resolve (a typo in STRUCTURE would otherwise fail mid-run).
    stand_in_roles = {spec.name: object() for spec in ROLES}
    for cat in STRUCTURE:
        overwrites_for(category_model(cat), object(), stand_in_roles)
        for ch in cat.channels:
            overwrites_for(ch.model, object(), stand_in_roles)
    problems += [f"onboarding default channel #{n} is not in STRUCTURE"
                 for n in ONBOARDING_DEFAULT_CHANNELS if n not in ALL_CHANNEL_NAMES]
    return problems


def manual_checklist():
    return [
        "Server Settings > Safety Setup: turn ON 'Raid protection' and 'DM spam protection'.",
        "Server Settings > Roles: give Valentino the 'Kami Team' role (the script cannot, he has to "
        "be in the server first). Both moderators need 2FA on their Discord accounts.",
        "Server Settings > Safety Setup > 'Require 2FA for moderator actions': only the owner can "
        "turn this on, the bot cannot.",
        "Server Settings > Community > Overview: check the rules and updates channels are set, and "
        "that the server is NOT listed in Discovery.",
        "Server Settings > Onboarding: open it once and check the 'Idioma / Language' question is "
        "there and Onboarding is ON. Then join with a second account and pick each option.",
        "Server Settings > Integrations: make sure no bots or webhooks other than ours are present.",
        "Create the invite you will actually share (set it to your soft-launch audience). Do not "
        "post invites in public places yet (spec 010, soft launch).",
        "Test with a second account that just joined: see 'Verification' in the spec. AutoMod "
        "allow-list behaviour for links is the part most worth testing.",
        "Discord Developer Portal: regenerate the bot token and remove the bot from the server.",
    ]


def print_plan():
    print("== PLAN ==")
    print("Guild settings: verification=high, media filter=all members, notifications=mentions only,")
    print("                Community on (rules=#rules, updates=#announcements), safety alerts + joins=#mod-log")
    print("@everyone: view, send, history, react, change nickname. Nothing else.")
    for role in ROLES:
        print(f"Role {role.name!r}: colour {role.color:#08x}, hoist={role.hoist}, "
              f"mentionable={role.mentionable}")
    for cat in STRUCTURE:
        print(f"[{cat.name}] ({category_model(cat)})")
        for ch in cat.channels:
            extras = f", slowmode {ch.slowmode}s" if ch.slowmode else ""
            pins = f", {len(PINS[ch.name])} pinned message(s)" if ch.name in PINS else ""
            print(f"   #{ch.name:<16} {ch.model:<9}{extras}{pins}")
    print("AutoMod rules (block + alert in #mod-log, Team role exempt):")
    for name, trigger in build_automod_rules():
        print(f"   {AUTOMOD_PREFIX}{name}  [{trigger.type.name}]")
    print(f"Onboarding: required question {C.ONBOARDING_TITLE!r} -> "
          f"{', '.join(t for t, _ in C.ONBOARDING_OPTIONS)}; default channels: "
          f"{', '.join('#' + n for n in ONBOARDING_DEFAULT_CHANNELS)}")
    print(f"Webhooks (-> {WEBHOOKS_FILE.name}, gitignored): {', '.join(WEBHOOK_CHANNELS)}")
    print()


# ---------------------------------------------------------------------------------------
# The build (talks to Discord)
# ---------------------------------------------------------------------------------------


class Report:
    def __init__(self):
        self.done = []
        self.failed = []

    def ok(self, text):
        self.done.append(text)
        print(f"  ok   {text}")

    def fail(self, text, error):
        message = f"{text}: {error}"
        self.failed.append(message)
        print(f"  FAIL {message}")


def find_channel(channels, name, kind):
    wanted = name.casefold()
    for ch in channels:
        if isinstance(ch, kind) and ch.name.casefold() == wanted:
            return ch
    return None


async def sync_roles(guild, report):
    print("Roles...")
    await guild.default_role.edit(permissions=EVERYONE_PERMISSIONS, reason="Kami setup: whitelist baseline")
    report.ok("@everyone reduced to the safe baseline")
    roles = {}
    for spec in ROLES:
        role = discord.utils.get(guild.roles, name=spec.name)
        fields = dict(
            colour=discord.Colour(spec.color),
            hoist=spec.hoist,
            mentionable=spec.mentionable,
            permissions=spec.permissions,
            reason="Kami setup",
        )
        if role is None:
            role = await guild.create_role(name=spec.name, **fields)
            report.ok(f"role {spec.name} created")
        else:
            await role.edit(**fields)
            report.ok(f"role {spec.name} updated")
        roles[spec.name] = role
        await asyncio.sleep(PAUSE)
    return roles


async def sync_structure(guild, roles, wipe, report):
    print("Channels...")
    existing = list(await guild.fetch_channels())
    planned_cats = {c.name.casefold() for c in STRUCTURE}
    planned_chs = {n.casefold() for n in ALL_CHANNEL_NAMES}

    stray = [
        ch for ch in existing
        if (isinstance(ch, discord.CategoryChannel) and ch.name.casefold() not in planned_cats)
        or (not isinstance(ch, discord.CategoryChannel) and ch.name.casefold() not in planned_chs)
    ]
    voice = [ch for ch in stray if isinstance(ch, (discord.VoiceChannel, discord.StageChannel))]
    if stray and not wipe:
        names = ", ".join(f"{ch.name} ({ch.type.name})" for ch in stray)
        print(f"  WARNING: channels outside the plan, left alone: {names}")
        if voice:
            print("  WARNING: voice/stage channels exist. This server is text-only by design (spec 010):"
                  " delete them by hand or re-run with --wipe.")
    if stray and wipe:
        names = ", ".join(f"{ch.name} ({ch.type.name})" for ch in stray)
        print(f"  --wipe will DELETE: {names}")
        typed = input(f"  Type the server name ({guild.name}) to confirm: ").strip()
        if typed != guild.name:
            raise SystemExit("Server name did not match; nothing was deleted.")
        # Children first: Discord refuses to delete a category that still has channels.
        for ch in sorted(stray, key=lambda c: isinstance(c, discord.CategoryChannel)):
            await ch.delete(reason="Kami setup --wipe")
            report.ok(f"deleted {ch.name}")
            await asyncio.sleep(PAUSE)
        existing = list(await guild.fetch_channels())

    created = {}
    categories = {}
    for cat_spec in STRUCTURE:
        cat_over = overwrites_for(category_model(cat_spec), guild.default_role, roles)
        category = find_channel(existing, cat_spec.name, discord.CategoryChannel)
        if category is None:
            category = await guild.create_category(cat_spec.name, overwrites=cat_over, reason="Kami setup")
            report.ok(f"category {cat_spec.name} created")
        else:
            await category.edit(overwrites=cat_over, reason="Kami setup")
            report.ok(f"category {cat_spec.name} updated")
        categories[cat_spec.name] = category
        await asyncio.sleep(PAUSE)

        for spec in cat_spec.channels:
            over = overwrites_for(spec.model, guild.default_role, roles)
            topic = C.TOPICS[spec.name]
            channel = find_channel(existing, spec.name, discord.TextChannel)
            if channel is None:
                channel = await guild.create_text_channel(
                    spec.name, category=category, topic=topic, slowmode_delay=spec.slowmode,
                    overwrites=over, reason="Kami setup",
                )
                report.ok(f"#{spec.name} created")
            else:
                await channel.edit(
                    category=category, topic=topic, slowmode_delay=spec.slowmode,
                    overwrites=over, reason="Kami setup",
                )
                report.ok(f"#{spec.name} updated")
            created[spec.name] = channel
            await asyncio.sleep(PAUSE)
    return created, categories


async def sync_order(client, guild, channels, categories, report):
    """Put categories and the channels inside them in the order written in STRUCTURE. Creating in
    order is not enough on a re-run, when a channel already exists somewhere else."""
    print("Order...")
    payload = []
    for i, cat_spec in enumerate(STRUCTURE):
        payload.append({"id": categories[cat_spec.name].id, "position": i})
        for j, spec in enumerate(cat_spec.channels):
            # No parent_id: Discord refuses to change more than one parent per call, and the
            # channels are already in the right category (sync_structure put them there).
            payload.append({"id": channels[spec.name].id, "position": j})
    try:
        await client.http.bulk_channel_update(guild.id, payload, reason="Kami setup: order")
        report.ok("categories and channels put in the planned order")
    except discord.HTTPException as error:
        report.fail("reorder channels", error)


async def sync_onboarding(client, guild, channels, roles, report):
    """The language question. Discord does not tell a server which language a member uses, so the
    member picks one when joining and gets the matching role, which unlocks that category."""
    print("Onboarding...")
    route = discord.http.Route("GET", "/guilds/{guild_id}/onboarding", guild_id=guild.id)
    previous = {}
    try:
        old = await client.http.request(route)
        previous = {p["title"]: p for p in old.get("prompts", [])}
    except discord.HTTPException as error:
        print(f"  (could not read the current onboarding, creating from scratch: {error.status})")

    # Discord wants an id on every prompt and option, even new ones: the dashboard makes them up
    # client-side as snowflakes. Same here, from the current time.
    fresh_ids = iter(range(int(discord.utils.time_snowflake(discord.utils.utcnow())), 1 << 62))
    es, en = roles[C.SPANISH_ROLE_NAME], roles[C.ENGLISH_ROLE_NAME]
    option_roles = [[es.id], [en.id], [es.id, en.id]]
    old_prompt = previous.get(C.ONBOARDING_TITLE, {})
    old_options = {o["title"]: o for o in old_prompt.get("options", [])}
    options = []
    for (title, description), role_ids in zip(C.ONBOARDING_OPTIONS, option_roles):
        option = {
            "title": title, "description": description,
            "role_ids": [str(r) for r in role_ids], "channel_ids": [],
        }
        # Keep the existing id, so a re-run updates the option instead of duplicating it.
        option["id"] = old_options[title]["id"] if title in old_options else str(next(fresh_ids))
        options.append(option)
    prompt = {
        "type": 0, "title": C.ONBOARDING_TITLE, "single_select": True, "required": True,
        "in_onboarding": True, "options": options,
    }
    prompt["id"] = old_prompt["id"] if "id" in old_prompt else str(next(fresh_ids))

    default_ids = [str(channels[name].id) for name in ONBOARDING_DEFAULT_CHANNELS]
    put = discord.http.Route("PUT", "/guilds/{guild_id}/onboarding", guild_id=guild.id)
    last_error = None
    # mode 0 counts only the default channels; mode 1 (advanced) also counts the question's channels.
    for mode in (0, 1):
        body = {"prompts": [prompt], "default_channel_ids": default_ids, "enabled": True, "mode": mode}
        try:
            await client.http.request(put, json=body, reason="Kami setup: language onboarding")
            report.ok(f"onboarding enabled: language question (mode {mode}), {len(default_ids)} default channels")
            return
        except discord.HTTPException as error:
            last_error = error
    report.fail("onboarding (do it in Server Settings > Onboarding: one required question "
                "'Idioma / Language' giving the Español / English roles)", last_error)


async def sync_guild_settings(guild, channels, report):
    """Two passes: safety levels first (Community requires them), the rest once the channels exist."""
    print("Guild settings...")
    guild = await guild.edit(
        verification_level=discord.VerificationLevel.high,
        explicit_content_filter=discord.ContentFilter.all_members,
        default_notifications=discord.NotificationLevel.only_mentions,
        reason="Kami setup: safety baseline",
    )
    report.ok("verification high, media scanned for all members, notifications mentions-only")
    await asyncio.sleep(PAUSE)

    try:
        guild = await guild.edit(
            community=True,
            rules_channel=channels["rules"],
            public_updates_channel=channels["announcements"],
            preferred_locale=discord.Locale.latin_american_spanish,
            reason="Kami setup: Community",
        )
        report.ok("Community enabled (rules + updates channels set)")
    except discord.HTTPException as error:
        report.fail("enable Community (do it in Server Settings > Enable Community)", error)
    await asyncio.sleep(PAUSE)

    try:
        guild = await guild.edit(
            system_channel=channels["mod-log"],
            safety_alerts_channel=channels["mod-log"],
            raid_alerts_disabled=False,
            reason="Kami setup: mod-log gets joins and safety alerts",
        )
        report.ok("joins and safety alerts go to #mod-log")
    except discord.HTTPException as error:
        report.fail("set system/safety-alert channel to #mod-log", error)
    return guild


async def sync_screening(client, guild, report):
    """Rules screening ('accept the rules before you can talk'). Bots may be refused here; if so
    it lands in the manual checklist."""
    body = {
        "enabled": True,
        "description": C.SCREENING_DESCRIPTION,
        "form_fields": [{
            "field_type": "TERMS",
            "label": "Read and agree to the server rules / Lee y acepta las reglas",
            "values": C.SCREENING_RULES,
            "required": True,
        }],
    }
    route = discord.http.Route("PATCH", "/guilds/{guild_id}/member-verification", guild_id=guild.id)
    try:
        await client.http.request(route, json=body)
        report.ok("rules screening enabled")
    except discord.HTTPException as error:
        report.fail("rules screening (do it in Server Settings > Safety Setup / Community > "
                    "'Require members to accept rules')", error)


async def sync_automod(guild, mod_log, team, report):
    print("AutoMod...")
    existing = {rule.name: rule for rule in await guild.fetch_automod_rules()}
    for name, trigger in build_automod_rules():
        full_name = AUTOMOD_PREFIX + name
        fields = dict(
            event_type=discord.AutoModRuleEventType.message_send,
            trigger=trigger,
            actions=automod_actions(mod_log.id),
            enabled=True,
            exempt_roles=[team],
            exempt_channels=[],
            reason="Kami setup",
        )
        try:
            if full_name in existing:
                await existing[full_name].edit(name=full_name, **fields)
                report.ok(f"AutoMod '{full_name}' updated")
            else:
                await guild.create_automod_rule(name=full_name, **fields)
                report.ok(f"AutoMod '{full_name}' created")
        except discord.HTTPException as error:
            report.fail(f"AutoMod '{full_name}'", error)
        await asyncio.sleep(PAUSE)


async def sync_pins(client, channels, report):
    print("Pinned messages...")
    ids = {name: ch.id for name, ch in channels.items()}
    for channel_name, texts in PINS.items():
        channel = channels[channel_name]
        mine = [m async for m in channel.pins(limit=None, oldest_first=True)
                if m.author.id == client.user.id]
        for i, raw in enumerate(texts):
            text = render(raw, ids)
            if i < len(mine):
                if mine[i].content != text:
                    await mine[i].edit(content=text)
                    report.ok(f"#{channel_name}: pinned message {i + 1} edited")
            else:
                message = await channel.send(text, allowed_mentions=discord.AllowedMentions.none())
                await message.pin()
                report.ok(f"#{channel_name}: pinned message {i + 1} posted")
            await asyncio.sleep(PAUSE)
        if len(mine) > len(texts):
            print(f"  WARNING: #{channel_name} has {len(mine) - len(texts)} extra pinned bot "
                  "message(s) not in the plan; left alone.")


async def sync_webhooks(channels, report):
    print("Webhooks...")
    saved = {}
    for name in WEBHOOK_CHANNELS:
        channel = channels[name]
        hook = next((w for w in await channel.webhooks() if w.name == WEBHOOK_NAME), None)
        if hook is None:
            hook = await channel.create_webhook(name=WEBHOOK_NAME, reason="Kami setup")
            report.ok(f"webhook for #{name} created")
        else:
            report.ok(f"webhook for #{name} already exists")
        saved[name] = hook.url
        await asyncio.sleep(PAUSE)
    WEBHOOKS_FILE.write_text(json.dumps(saved, indent=2), encoding="utf-8")
    print(f"  saved to {WEBHOOKS_FILE.name} (gitignored: these URLs are secrets, never commit them)")


async def build(client, guild_id, args):
    report = Report()
    guild = await client.fetch_guild(guild_id)
    print(f"Server: {guild.name} ({guild.id})")

    roles = await sync_roles(guild, report)
    team = roles[C.TEAM_ROLE_NAME]
    channels, categories = await sync_structure(guild, roles, args.wipe, report)
    await sync_order(client, guild, channels, categories, report)
    guild = await sync_guild_settings(guild, channels, report)
    await sync_screening(client, guild, report)
    await sync_onboarding(client, guild, channels, roles, report)
    await sync_automod(guild, channels["mod-log"], team, report)
    await sync_pins(client, channels, report)
    await sync_webhooks(channels, report)

    if args.invite:
        invite = await channels["welcome"].create_invite(
            max_age=0, max_uses=0, unique=False, reason="Kami setup: controlled invite"
        )
        print(f"\nInvite to #welcome: {invite.url}")

    print("\n== RESULT ==")
    print(f"{len(report.done)} steps ok, {len(report.failed)} failed")
    for line in report.failed:
        print(f"  FAIL {line}")
    print("\n== DO BY HAND ==")
    for i, line in enumerate(manual_checklist(), 1):
        print(f" {i}. {line}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawTextHelpFormatter)
    parser.add_argument("--dry-run", action="store_true", help="print the plan, call no API")
    parser.add_argument("--wipe", action="store_true",
                        help="delete channels that are not in the plan (asks to retype the server name)")
    parser.add_argument("--invite", action="store_true", help="create one permanent invite to #welcome")
    args = parser.parse_args()

    problems = validate_offline()
    if problems:
        print("Content problems, nothing was sent:")
        for p in problems:
            print(f"  - {p}")
        sys.exit(1)

    print_plan()
    if args.dry_run:
        print("Dry run: content is within Discord's limits and every AutoMod rule builds. "
              "No API call was made.")
        return

    from dotenv import load_dotenv  # only needed for a real run

    load_dotenv(HERE / ".env")
    token = os.getenv("DISCORD_TOKEN")
    guild_id = os.getenv("GUILD_ID")
    if not token or not guild_id:
        sys.exit("DISCORD_TOKEN and GUILD_ID must be set in tools/discord-setup/.env (see .env.example)")

    async def run():
        client = discord.Client(intents=discord.Intents.none())
        await client.login(token)
        try:
            await build(client, int(guild_id), args)
        finally:
            await client.close()

    asyncio.run(run())


if __name__ == "__main__":
    main()
