# Moderating the Kami Paper Scissors Discord

For Diego and Valentino. The server is for people **13 and older**, and many of them will be
kids. That is the whole reason this guide exists: the rules are simple, and the hard part is
being consistent when you are busy. Design and reasoning: `specs/010-discord-community/spec.md`.

## The five things that never change

1. **We never DM a member.** Not to be nice, not to explain a ban, not to thank an artist. If a
   member writes to you privately, answer **in a public channel** ("check your DMs" is also not
   allowed) or not at all.
2. **We never ask for personal information or photos.** Not a name, an age, a school, a city.
3. **We never handle a minor one-to-one in private.** Everything happens where the other mod can
   see it.
4. **Two sets of eyes on anything serious.** Tell the other mod before acting on a big case, unless
   a child is at risk right now (then act first, tell after).
5. **When in doubt, remove first, discuss after.** A deleted message can be restored by talking; a
   child who saw something bad cannot be un-shown it.

## What the server does by itself

- Nobody can write before accepting the rules; accounts must have been on Discord and in the
  server for a while (verification High).
- All images are scanned. Files can be attached **only in CREATE**; links and invites are
  blocked; there is no voice or video.
- AutoMod blocks profanity, sexual content, slurs, spam, mention spam, emails/phone numbers,
  "my insta/snap" style contact attempts, links, other servers' invites, and a list of phrases
  nobody should send a child ("how old are you", "send me a pic", "don't tell your parents"...).
  Every block is posted to **`#mod-log`** with the member and the message.

## Languages

Members pick *Español*, *English* or *Both* when they join and only see that language's category.
You and Valentino have **Kami Team**, which sees both, so you can moderate everything. If a member
writes in the wrong language category, remind them kindly; they can change their language in
**Channels & Roles** at any time. A member who answers nothing still sees the rules, the news and
CREATE, but no chat: if someone says "the server is empty", that is why.

## Reading `#mod-log`

It also shows joins and Discord's safety alerts. Two rhythms are enough:

- **Daily, 5 minutes**: scroll `#mod-log`. A single profanity hit is nothing. What matters is the
  **safety phrases** and **contact info** rules, and the same member hitting several rules.
- **Weekly, 20 minutes**: skim `#general-es`, `#general-en` and CREATE for what AutoMod cannot
  see (tone, picking on someone, a drawing that is not okay).

If neither of you can cover a stretch (travel, a crunch before a release), **turn slowmode up** in
the chat channels and say so in `#announcements`. An unwatched server is worse than a slow one.

## The ladder

For ordinary rule-breaking, **in public, calmly, short**:

1. Friendly reminder in the channel, citing the rule number.
2. 10-minute timeout.
3. 24-hour timeout.
4. Ban.

Skip steps for anything in the next section.

## Act immediately: ban, then report

Ban and **report to Discord (Trust & Safety)** for any of these, and consider your local
authorities too:

- an adult (or anyone) asking a child for private contact, photos, personal details, or to keep a
  secret from their parents;
- sexual content involving a minor, or a minor sexualised in any way;
- doxxing, threats, or pressure to move to another platform.

**Before you delete anything, take screenshots and copy the message links/IDs.** Deleting first
destroys the evidence. Then ban, then write what happened in `#team-chat`.

## A member who seems to be in danger or hurting themselves

Reply **in public**, kindly and briefly: tell them to talk to a trusted adult, and that if they
are in danger they should call their country's emergency number. Tell the other mod right away.
Do not try to counsel them privately; you are not equipped to, and DMs are off the table.

## Someone says they are on the team

Anyone can claim it. Real team members have the **Kami Team** role and never DM. The rules tell
members this explicitly, so a DM from "the team" is itself a red flag: ask them to report it.

## Fan art and the "feature" requests

- Only their own work, no AI, no photos of faces, no personal info visible (names on paper, school
  uniforms, house numbers). Remove the post and say why, politely, in the channel.
- Posting does **not** give us permission to reuse. If we want to show a piece, we ask **in the
  post's thread or replies, in public**. For an artist under 18 the permission must come from a
  parent or guardian **by email** to the report address, never by Discord.
- Hand out the **Paper Artist** role for work that is great or that shows effort.

## Tuning AutoMod

A rule that fires wrongly (a normal word blocked) or misses something is fixed in
`tools/discord-setup/content.py` and applied with `python setup.py`. Every re-run puts the rules
back to what the file says, so do not edit them by hand in Discord, or the next run overwrites it.
Entries without `*` match whole words only (so `puta` does not hit `computadora`); add `*` only
for multi-word phrases.

## Parents

A parent may write to the report address. Be warm and brief, answer in writing, and never ask
the child for anything. If a parent wants their child removed, remove the child's messages and
ban or kick as asked, then confirm by email.
