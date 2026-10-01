# Tasks: Kami Paper Scissors Discord community (spec 010)

No Unity work: the deliverables are a Python script, text and a moderation guide. Phases run in
order. Phase 1 (everything that can be built and checked from this repo) is done. Phase 2 is the
part only Diego can do, because it needs his Discord account, and Phase 3 is the real-world test.

## Phase 1 - Script, text and docs (done 2026-10-01, checked offline)

- [x] **1.A** **Server text** (`tools/discord-setup/content.py`): welcome, rules, what-is-kami (ES
  + EN), channel topics, rules-screening lines, AutoMod word lists and regexes, the report address
  in one constant (`REPORT_EMAIL`).
- [x] **1.B** **Setup script** (`tools/discord-setup/setup.py`): roles, `@everyone` whitelist,
  categories and channels with explicit overwrites, guild safety settings, Community, rules
  screening, 8 AutoMod rules, pins, webhooks. Idempotent; `--dry-run`, `--wipe` (confirms the server
  name), `--invite`.
- [x] **1.C** **Offline checks**: `--dry-run` (limits + every AutoMod rule builds); the filters
  tested against 26 innocent sentences (game vocabulary included) and 16 bad ones; the build run
  twice against signature-checked mocks (second run creates nothing); every call signature bound to
  the real discord.py 2.7.
- [x] **1.D** **Docs**: `docs/discord/moderation.md` (the playbook), `tools/discord-setup/README.md`,
  `CLAUDE.md` pointer, this spec updated with what changed from the draft.

- [x] **1.E** **Language gating** (2026-10-01): Español/English roles, hidden language categories,
  Onboarding question, channel ordering, Kami's age fixed to 13. Applied to the real server and read
  back; reorder tested by scrambling and re-running.

## Phase 2 - Diego builds the server (needs his Discord account, ~15 min)

- [x] **2.A** Application and bot created, invited with `Administrator` for the one run (Diego).
- [x] **2.B** Ran 2026-10-01 with `--wipe` (it removed only the 4 default channels): 51 steps ok, 0
  failed. Read back from Discord and re-run once: nothing created twice. Token passed as an env var,
  never written to disk.
- [ ] **2.C** Do the **"DO BY HAND"** list the script prints at the end (raid protection, DM-spam
  protection, 2FA for moderators, Valentino's role) and anything under `FAIL` in the result.
- [ ] **2.D** Regenerate the bot token (it was pasted in chat, so treat it as exposed) and remove the
  bot from the server. **Do this even if 2.C is not finished.**

## Phase 3 - Test and open (needs a second account)

- [ ] **3.A** The new-member test from the spec, with an alt account that has just joined. The one
  most worth doing is the link rule: it must block `https://example.com` and let
  `https://kimmiarts.com` through. If the allow-list does not behave, drop the allow-list entries in
  `content.py` (everything is blocked, still safe) and re-run.
- [ ] **3.A2** In the same alt-account test, join and answer the language question with each option:
  *Español* must show only ESPAÑOL, *English* only ENGLISH, *Both* both; START HERE, THE GAME and CREATE
  must show in all three. Then change the answer in Channels & Roles.
- [ ] **3.B** Trigger each AutoMod rule once; confirm the alert lands in `#mod-log`.
- [ ] **3.C** Diego signs off the ES and EN rules text, including the paragraph about featuring
  minors' art (his legal call), then Valentino reads `docs/discord/moderation.md`.
- [ ] **3.D** **Soft launch**: invite close circles only for about a week (spec decision), tune the
  word lists from what `#mod-log` shows, then make the invite public.

## Parallelism

Phase 1 was a single sequential job. Phases 2 and 3 are Diego's, in order.
