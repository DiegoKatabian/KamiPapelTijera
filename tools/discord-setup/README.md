# Discord setup (Kami Paper Scissors)

Builds the whole server from `setup.py` + `content.py`: roles, channels, permissions, safety
settings, AutoMod and the pinned rules. One-shot and idempotent: running it again repairs drift.
Design: `specs/010-discord-community/spec.md`. How to moderate: `docs/discord/moderation.md`.

## Run it

1. **Create the bot** (5 min): <https://discord.com/developers/applications> -> New Application ->
   "KamiSetup" -> Bot tab -> Reset Token, copy it. No privileged intents are needed.
2. **Invite it** to the (empty) server: OAuth2 -> URL Generator -> scope `bot`, permission
   `Administrator` -> open the URL. This is for one run only; step 6 removes it.
   If no URL shows up: it is at the very bottom of the page, and it is not generated while the
   Bot tab has "Requires OAuth2 Code Grant" on (turn it off). Fallback that always works, with the
   Application ID from General Information:
   `https://discord.com/oauth2/authorize?client_id=YOUR_APPLICATION_ID&scope=bot&permissions=8`
3. **Server id**: Discord -> Settings -> Advanced -> Developer Mode on, then right click the
   server name -> Copy Server ID.
4. ```bash
   cd tools/discord-setup
   pip install -r requirements.txt
   cp .env.example .env   # then fill DISCORD_TOKEN and GUILD_ID
   python setup.py --dry-run
   ```
   Read the plan. It needs no token and makes no API call.
5. ```bash
   python setup.py --wipe
   ```
   `--wipe` deletes channels that are not in the plan (a new server has a default `general` and a
   voice channel, and this server is text-only). It asks you to type the server name first. Without
   `--wipe` nothing is ever deleted; the script only warns.
6. Do the **DO BY HAND** list it prints, **regenerate the token** (Bot tab) and **kick the bot**.

`--invite` also creates one permanent invite to `#welcome`.

## What it builds, in one line

Roles (Team, Paper Artist, Español, English), 6 categories, 16 channels in a fixed order, the safety
settings, 8 AutoMod rules, the pinned rules and the **Onboarding** language question that unlocks the
ESPAÑOL or ENGLISH category.

## What it never does

- Never deletes anything unless you pass `--wipe` and retype the server name.
- Never puts the token or the webhook URLs in git: `.env` and `discord-webhooks.json` are ignored.
- Never makes the bot the owner. You create the server; the bot only configures it.

## Changing things

Words, regexes, rule text, the report address: `content.py`. Channels, slowmode, permission
models, AutoMod rules: `setup.py` (`STRUCTURE`, `build_automod_rules`). Then re-run. Anything you
edit by hand on those objects in Discord is overwritten by the next run.

## Needs

Python 3.10+, `discord.py` >= 2.4 (written and checked against 2.7), `python-dotenv`.
