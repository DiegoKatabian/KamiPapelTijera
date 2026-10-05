#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Adds a PLACEHOLDER `feet/rain-boots` skin to Kami's Spine export (spec 011, stand-in for #83).

Why it exists: Kami Gear equips the rain boots when Kami gets `botasAgua` (the P cheat, Tiburcio's
quest), but Valen's real `feet/rain-boots` skin isn't drawn yet. This builds a stand-in only from
images already in the atlas, so there is no new art and no atlas repack:
- shoes: each shoe's white _OL_ (paper border) silhouette, drawn at the shoe's own size and tinted
  yellow: a flat yellow rubber boot that still sits inside its white paper border.
- gaiters (polaina): a copy of the gaiter mesh, tinted yellow: the boot shaft.
- buckle (ebilla) + its outline: transparent copies. While the shoes live in the `default` skin, an
  entry there can be replaced but never removed, so the placeholder hides the buckle by overriding its
  key with an invisible copy. Valen's real skin just leaves the buckle out.

A re-export of Kami from Spine overwrites skeleton.json and drops this skin. If #83 isn't in that
export yet, run this again on the new export. The skin is inserted as text after the last skin, in
the export's own style, so the rest of the file is untouched.

Usage:  python tools/add-placeholder-rain-boots.py "Assets/2D/Kami Spine/Atlas 12 Spine4.2/skeleton.json"
"""
import io, json, sys

SKIN = "feet/rain-boots"
YELLOW_FLAT = "ffd23cff"       # over a white silhouette: bright rubber yellow
YELLOW_ON_GAITER = "ffe646ff"  # over the light-beige gaiter texture: lands close to the shoes' yellow
INVISIBLE = "ffffff00"

SHOES = ["25_leg_zapato_front", "30_leg_zapato_back"]
GAITERS = ["26_leg_polaina_front", "26_leg_polaina_back"]
BUCKLES = ["23_leg_zapato_ebilla_front", "28_leg_zapato_ebilla_back",
           "23_OL_leg_zapato_ebilla_front", "28_OL_leg_zapato_ebilla_back"]

# where the shoes are drawn: `default` today, `outfit/default` once Valen restructures the skins (#141)
SOURCE_SKINS = ["outfit/default", "default"]


def outline_of(slot):
    number, rest = slot.split("_", 1)
    return number + "_OL_" + rest


def one_line(obj):
    return json.dumps(obj, ensure_ascii=False, separators=(", ", ": "))


def find_entry(skins, slot):
    for name in SOURCE_SKINS:
        attachments = skins.get(name, {})
        if slot in attachments and slot in attachments[slot]:
            return dict(attachments[slot][slot])
    sys.exit("no '%s' attachment for slot '%s' in %s: the export changed, update this script"
             % (slot, slot, " or ".join(SOURCE_SKINS)))


def main(path):
    text = io.open(path, encoding="utf-8", newline="").read()
    if "\r\n" in text:
        sys.exit("unexpected CRLF line endings in the export")
    data = json.loads(text)
    if any(s["name"] == SKIN for s in data["skins"]):
        sys.exit("the export already has a '%s' skin: nothing to do" % SKIN)

    skins = {s["name"]: s.get("attachments", {}) for s in data["skins"]}

    entries = {}
    for slot in SHOES:
        shoe = find_entry(skins, slot)
        shoe["path"] = outline_of(slot)  # the white silhouette, stretched to the shoe's own size
        shoe["color"] = YELLOW_FLAT
        entries[slot] = shoe
    for slot in GAITERS:
        gaiter = find_entry(skins, slot)
        gaiter["color"] = YELLOW_ON_GAITER
        entries[slot] = gaiter
    for slot in BUCKLES:
        buckle = find_entry(skins, slot)
        buckle["color"] = INVISIBLE
        entries[slot] = buckle

    body = ["\t{", '\t\t"name": "%s",' % SKIN, '\t\t"attachments": {']
    slots = list(entries)
    for i, slot in enumerate(slots):
        body.append('\t\t\t"%s": {' % slot)
        body.append('\t\t\t\t"%s": %s' % (slot, one_line(entries[slot])))
        body.append("\t\t\t}" + ("," if i < len(slots) - 1 else ""))
    body += ["\t\t}", "\t}"]

    # Spine's pretty export closes the skins array with "\t}\n],\n" right before "events"
    anchor = '\t}\n],\n"events"'
    if text.count(anchor) != 1:
        sys.exit("could not find the end of the skins array exactly once: the export format changed")
    text = text.replace(anchor, '\t},\n' + "\n".join(body) + '\n],\n"events"')

    json.loads(text)  # still valid JSON
    io.open(path, "w", encoding="utf-8", newline="").write(text)
    print("added '%s' (%d slots) to %s" % (SKIN, len(slots), path))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
