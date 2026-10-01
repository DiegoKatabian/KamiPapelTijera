# Spec 010 - Kami Paper Scissors Discord community

**Status** (2026-10-01): script, text and docs built, and **run against the real server**: 51 steps ok, 0
failed, then re-read from Discord (permissions, 8 AutoMod rules, pins, Community and the rules gate all
match) and re-run once to confirm it creates nothing twice. Still to do: Diego's by-hand checklist and the
new-member test with a second account (see `tasks.md`). Changes from the first draft are in
"Implementation notes" at the end.
**Owner**: Diego (setup script, moderation, legal) + Valentino (moderation, visuals).
**Reference**: Hexwalls' `Tools/DiscordSetup/` (separate repo). Reused as a starting point, not as-is.

## Goal

A dedicated Discord server for **Kami Paper Scissors**, for talking about the game, paper
crafts and art in general, with channels where kids can post their fan art.

**Minors are expected members.** Every decision below is judged first by "is this safe for a
13-year-old", then by "is this pleasant for the community". Moderation capacity is two people
(Diego and Valentino), so the design prefers *rules the server enforces by itself* (permissions,
AutoMod) over rules that need a human watching.

## Non-goals (v1)

- No voice, video or stage channels (the main grooming and recording risk; text only).
- No persistent community bot, no ModMail, no reaction roles. The setup is a one-shot script.
- No Portuguese channels (the game is localized es/en/pt, but the community is es + en; add later
  if pt speakers show up).
- No Server Onboarding flow, no monetization, no events.

## Language

Separate chat per language, as decided by Diego (most of the community speaks Spanish).

- **Spanish and English each get their own category** with the same channels. A member sees only the
  category of their language, and picks it when joining (see "Language gating" below).
- **Shared, bilingual**: START HERE, THE GAME and CREATE, visible to everyone. Rules/welcome are posted
  ES then EN as two messages (Discord's 2000-character limit). Announcements and devlogs are posted by
  the team in both languages in the same message. Art is language-neutral, so fan art is not split.
- Spanish is neutral Latin American (tuteo), warm and short, readable by kids. Not Rioplatense.
- Community-facing text is in Spanish/English by design. This is product content, not repo code:
  the repo language rule (English for code, comments, docs) still holds for the script and this spec.

## Server structure

Name: **Kami Paper Scissors**.

| Category | Channel | Who can write | Notes |
|---|---|---|---|
| START HERE | `#welcome` | team | ES + EN. Rules gate (Community rules screening) |
| | `#rules` | team | full ES + EN, plus how to report |
| | `#announcements` | team | Community "updates" channel |
| THE GAME | `#what-is-kami` | team | ES + EN explainer |
| | `#devlog` | team (everyone reacts) | ES + EN |
| ESPAÑOL | `#general-es` | everyone | slowmode 5s |
| | `#presentaciones` | everyone | slowmode 30s |
| | `#ayuda-del-juego` | everyone | |
| ENGLISH | `#general-en` | everyone | slowmode 5s |
| | `#introductions` | everyone | slowmode 30s |
| | `#game-help` | everyone | |
| CREATE | `#fan-art` | everyone (attachments ON) | slowmode 60s |
| | `#paper-crafts` | everyone (attachments ON) | slowmode 60s |
| | `#art-general` | everyone (attachments ON) | slowmode 60s |
| TEAM (private) | `#mod-log` | team | AutoMod alerts land here |
| | `#team-chat` | team | |

Roles: **Kami Team** (hoisted, mentionable; Diego + Valentino; moderator permissions, not
Administrator on the bot), **Paper Artist** (cosmetic, no permissions; the team awards it for
fan art, a kid-visible "you did something good" signal) and **Español** / **English** (language roles,
no permissions, given by Onboarding; they only unlock their category).

## Language gating (added 2026-10-01)

Discord does not tell a server which language a member's app uses, so it cannot be detected. The member
picks it, with Discord's own **Server Onboarding** (no hosted bot):

- One **required, single-choice** question, "Idioma / Language": *Español* (role Español), *English*
  (role English), *Ambos / Both* (both roles).
- ESPAÑOL and ENGLISH are hidden from `@everyone` and visible to their role and to Kami Team (the
  team needs both to moderate). START HERE, THE GAME and CREATE stay visible to everyone and are the
  Onboarding default channels, so a member who skips the question still sees the rules and the art.
- Members can change their answer any time in **Channels & Roles**. The welcome and rule 8 say so.
- It only applies to people who join after it is on. The owner sees every channel regardless.

## Safety design

### 1. Access and age

- Written rule: **13+** (Discord's own floor; anyone under the minimum age in their country cannot
  join). Stated in `#welcome`, `#rules` and the invite description.
- **Rules screening** on: nobody can write before accepting the rules.
- Verification level **High** (must be on Discord 5+ minutes and a member 10+ minutes). We
  deliberately do not use Highest: it requires a verified phone number, which excludes kids.
- Explicit media filter on **scan media from all members**.
- One controlled invite link. `@everyone` loses `Create Invite`.

### 2. Protection from adults (most important)

Written in `#rules` and binding on the team:

- **The team never DMs a member, and never asks for photos, real names, school, address, age
  confirmation or any other personal data.** Everything happens in public channels.
- Moderators are adults with 2FA on. No moderator handles a minor one-to-one in private.
- No voice/video (see non-goals).
- Members are told, in kid-friendly words in both languages, that if anything makes them
  uncomfortable they should tell a trusted adult and use Discord's **Report** on the message.
  Reports to us: **diego.katabian@kimmiarts.com**, and `@Kami Team` in any channel.
- There is no channel kids write reports into: without a bot, any such channel is either public or
  unread. Mail + `@Kami Team` + Discord Report is the path.

### 3. Content and AutoMod

All rules block the message, show the member a friendly explanation, and alert `#mod-log`. The
Team role is exempt.

Discord limits (per server): 6 keyword rules, 1 preset rule, 1 spam rule, 1 mention-spam rule (we use 5 + 1 + 1 + 1).

| Rule | What it does |
|---|---|
| Preset | Discord's profanity + sexual content + slurs |
| Custom words | ES / EN / PT bad-word list, in `content.py` (curated by us) |
| Contact info | regex: emails, phone numbers, "my insta/snap/tiktok/whatsapp"-style handles |
| Safety phrases | ES/EN/PT phrases nobody should send a child; block + alert (added 2026-10-01) |
| Links | blocks all links except a small allow-list (kimmiarts.com, Steam page; GIF sites dropped, see notes) |
| Invites | blocks `discord.gg` / `discord.com/invite` (stops kids being moved to other servers) |
| Spam | Discord's spam detection |
| Mention spam | limit 5 mentions per message |

`@everyone` permission baseline (denied server-wide, re-allowed only where listed): attach files,
embed links, external emoji/stickers, external apps, create threads, mention @everyone, TTS.
Attachments are allowed **only in CREATE**. No images or files in chat channels.

### 4. Fan art rules

- Only **your own** work; credit anything you based on. Nothing hateful, sexual or violent.
- No photos of faces, no personal info in photos (names on paper, school uniforms, house
  numbers). Sign with your nickname.
- Posting art here does **not** let us repost it elsewhere. If the team wants to feature a piece
  (social media, trailer, the game), we ask first, in public, and for under-18 artists a parent or
  guardian must say yes by email. *(Diego owns the legal wording; this is the intent, not legal text.)*
- **Decision to confirm**: AI-generated images. Default proposed: not allowed in CREATE, to protect
  the point of the space (kids showing what they made).

### 5. Moderation playbook (short, lives in `docs/discord/moderation.md` once built)

- Ladder: friendly reminder -> 10-minute timeout -> 24h timeout -> ban. Always public-room
  warnings first, no private "talks".
- **Immediate ban + report to Discord Trust & Safety** (and to local authorities where
  appropriate) for: grooming behaviour, adults asking minors for private contact or photos,
  sexual content involving minors, doxxing, threats. Screenshot and note message IDs *before*
  deleting, so there is evidence.
- A member who says something suggesting they are in danger or self-harming: reply publicly with
  care, tell them to talk to a trusted adult, mention local emergency services, and tell the other
  mod. Do not try to counsel in private.
- Both mods check in on a rhythm they can keep. If neither can cover a period (travel, crunch),
  turn on slowmode in chat channels instead of leaving it unwatched.

## The setup script

`tools/discord-setup/` (this repo; Python 3.10+, `discord.py` 2.x, REST only, one-shot).

```
tools/discord-setup/
  setup.py          # builds everything
  content.py        # all server text (ES + EN) and the AutoMod word lists
  requirements.txt
  .env.example      # DISCORD_TOKEN, GUILD_ID
```

Differences from Hexwalls' script:

- **Never wipes by default.** Hexwalls deletes every channel first. Here `--wipe` is opt-in, deletes
  only channels outside the plan, and asks to retype the server name.
- **Idempotent**: roles, categories, channels and AutoMod rules are matched by name and updated,
  not duplicated. Re-running fixes drift.
- **`--dry-run`** prints the whole plan without calling the API.
- Also configures: verification level, explicit media filter, default notifications (mentions
  only), Community features (rules and announcements channels), the 8 AutoMod rules, slowmodes
  and the `@everyone` permission baseline.
- Pins the ES + EN `#welcome`, `#rules`, `#what-is-kami`.
- Webhooks for `#announcements`, `#devlog` (gitignored JSON), as in Hexwalls.
- `.env` and the webhooks file go in `.gitignore`.

### What the script cannot do (checklist for Diego, in the UI)

- **Create the server** (Diego creates it empty; the bot must not own it).
- **Require 2FA for moderator actions** (owner-only setting).
- Anything Discord rejects for bots: rules screening and the raid-protection / DM-spam toggles are
  attempted and, if refused, reported at the end of the run as a click-list.

Bot token: create the application, invite with `Administrator` for the one run, then **regenerate
the token** and remove the bot (it is not needed afterwards).

## Verification

I can compile and dry-run from here, but I cannot see the real server. So:

1. `--dry-run` output reviewed by Diego before the real run.
2. After the run, test with a **second account** that has just joined (new-member state): it must
   not be able to (a) post anywhere before accepting the rules, (b) post a link, an email or a
   phone number, (c) attach a file in `#general-es`/`#general-en`, (d) see TEAM, (e) create an
   invite, (f) DM-spam-mention `@everyone`. It must be able to attach in `#fan-art`.
3. Trigger each AutoMod rule once and confirm the alert lands in `#mod-log`.
4. Diego signs off on the ES and EN rules text (and the legal paragraph) before the server is made
   public.

## Decisions taken (2026-09-30)

- Separate ES and EN chat; shared bilingual info and CREATE.
- Moderation by Diego and Valentino only.
- Report address: diego.katabian@kimmiarts.com. *Note*: it is a personal-name address published to
  kids. A role alias (e.g. `safety@kimmiarts.com` forwarding to it) would be easy to swap in later
  without touching anything else; only the text in `content.py` mentions it.
- Server name: **Kami Paper Scissors**.

## Open questions - resolved 2026-10-01 (Diego: "defaults")

1. AI-generated images in CREATE: **not allowed** (in the rules, ES and EN).
2. Art shared across languages: **one CREATE for both**.
3. Legal paragraph on featuring minors' art: the text in `content.py` (`RULES_*_2`, "About your
   art") is **my draft of the intent, not legal text**. Diego words the final version before the
   server goes public (tasks 3.C).
4. Launch: **soft launch**, close circles only for about a week, then public (tasks 3.D). Safer
   for a server with kids and two moderators, and the invite is not shared until the rules have
   been tested.

## Implementation notes (what changed from the draft, and why)

- **Where**: `tools/discord-setup/` (this repo uses a lowercase `tools/`). Moderation guide:
  `docs/discord/moderation.md`.
- **8 AutoMod rules, not 7**: the draft's "contact info" rule was split so the phrases nobody
  should send a child ("how old are you", "don't tell your parents"...) are their own rule, named
  "safety phrases". They are the ones worth a human look in `#mod-log`. All rules are named
  `Kami: ...` so the script finds them again.
- **GIFs are off in v1** (draft allowed tenor.com/giphy.com): `@everyone` has no Embed Links, so a
  GIF link would show as raw text, and allowing embeds would also unfurl whatever a link points
  to. Revisit if the kids ask for GIFs. The link allow-list is `kimmiarts.com` and the Steam page.
- **Known gap**: a bare domain with no `http(s)://` and no `www.` (e.g. `example.com`) is not
  blocked. Regex for bare domains was left out on purpose: it hits ordinary typing ("bueno.es").
  Discord's allow-list may not apply to regex, so it would also block our own domain.
- **`@everyone` is a whitelist** (view, send, history, react, nickname). Everything else is off
  server-wide, which is simpler to audit than a blacklist and is why there is no voice, no threads
  and no invite creation.
- **`--wipe` deletes every channel not in the plan** (not "everything"), after retyping the server
  name. A brand-new server has a default `general` and a voice channel; the script warns about
  them if `--wipe` is not given. It never deletes anything otherwise.
- **`#mod-log` also receives joins and Discord's safety alerts** (system channel + safety alerts
  channel), so a raid or a suspicious join shows up where the mods already look.
- **No report channel for kids** (as decided): mail, `@Kami Team` and Discord's Report only.
- **Robustness**: every step that Discord may refuse for a bot (Community, rules screening,
  individual AutoMod rules, safety channels) is caught, reported under `FAIL` and added to the
  "do by hand" list instead of stopping the run.
- **What is verified and what is not**: content limits, filter behaviour on sample sentences, call
  signatures and idempotency (mocks), and **on the real server**: every call accepted, settings read
  back from Discord, a second run created nothing. **Not verified**: how AutoMod actually behaves on
  messages (the link allow-list above all) and the new-member test. Those are tasks 3.A-3.B.
- **Language gating (2026-10-01, Diego: defaults)**: see the section above. Defaults taken: "Both" is an
  option, the question is required. Verified on the real server by reading it back: onboarding enabled
  (mode 0, 8 default channels), each language category visible only to its role plus Kami Team.
  Discord accepted it without the "5 public writable channels" minimum I feared. Not verified: the
  real new-member experience (task 3.A2).
- **Channel order is enforced** on every run (positions only; Discord refuses to move several channels
  between categories in one call). Tested live: scrambled on purpose, the script restored it.
- **Kami is 13** (Diego): fixed in `content.py` and `docs/GDD.md` (version 0.4).
- Two things only the real API revealed, both fixed: reordering must not send `parent_id`, and a new
  Onboarding prompt/option needs a client-made snowflake `id`.
