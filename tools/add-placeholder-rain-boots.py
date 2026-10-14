#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Adds a PLACEHOLDER rain-boots skin to Kami's Atlas 13 Spine export (spec 011 / spec 015).

Why it exists: Kami Gear equips the rain boots when Kami gets `botasAgua` (Tiburcio's quest, the P cheat),
and the Level 1 demo ships before Valen's real boots are drawn. The skin is built only from images already
in the atlas, so there is no new art and no atlas repack:
- shoes: each shoe's white _OL_ (paper border) silhouette, drawn at the shoe's own size and tinted yellow:
  a flat yellow rubber boot that still sits inside its white paper border.
- the _OL_ border slots: copied from the base shoes. Feet owns those slots, so the skin has to bring them back.
- gaiters (polaina): a copy of the outfit's gaiter mesh, tinted yellow: the boot shaft. Gaiters are not
  Feet-owned slots (the outfits draw them), so they are overridden here and come back when the boots come off.
- buckles (ebilla): left out. Feet owns those slots, so equipping the skin clears them.

Atlas 13 slots are named `KamiLibro/...`; the attachments inside keep the plain names.

A re-export of Kami from Spine overwrites skeleton.json and drops this skin. Run this again on the new
export until Valen's real boots are in it, then point `Gear_RainBoots` at hers. The skin is inserted as text
after the last skin, in the export's own style, so the rest of the file is untouched.

Usage:  python tools/add-placeholder-rain-boots.py "Assets/2D/Kami Spine/Atlas 13/skeleton.json"
"""
import io, json, sys

SKIN = "Zapatos/Zapatos_RainBoots_Placeholder"
YELLOW_FLAT = "ffd23cff"       # over a white silhouette: bright rubber yellow
YELLOW_ON_GAITER = "ffe646ff"  # over the light-beige gaiter texture: lands close to the shoes' yellow

SLOT_PREFIX = "KamiLibro/"
SHOES = ["25_leg_zapato_front", "30_leg_zapato_back"]
BORDERS = ["25_OL_leg_zapato_front", "30_OL_leg_zapato_back"]
GAITERS = ["26_leg_polaina_front", "26_leg_polaina_back"]

SHOE_SOURCE_SKIN = "Zapatos/Zapatos_Base"
GAITER_SOURCE_SKIN = "FullSkins/Kami Libro 1"


def outline_of(attachment):
    number, rest = attachment.split("_", 1)
    return number + "_OL_" + rest


def one_line(obj):
    return json.dumps(obj, ensure_ascii=False, separators=(", ", ": "))


def find_entry(skins, skin_name, attachment):
    slot = SLOT_PREFIX + attachment
    entry = skins.get(skin_name, {}).get(slot, {}).get(attachment)
    if entry is None:
        sys.exit("no '%s' attachment for slot '%s' in skin '%s': the export changed, update this script"
                 % (attachment, slot, skin_name))
    return dict(entry)


def main(path):
    text = io.open(path, encoding="utf-8", newline="").read()
    if "\r\n" in text:
        sys.exit("unexpected CRLF line endings in the export")
    data = json.loads(text)
    if any(s["name"] == SKIN for s in data["skins"]):
        sys.exit("the export already has a '%s' skin: nothing to do" % SKIN)

    skins = {s["name"]: s.get("attachments", {}) for s in data["skins"]}

    entries = {}  # slot -> (attachment name, attachment dict)
    for attachment in SHOES:
        shoe = find_entry(skins, SHOE_SOURCE_SKIN, attachment)
        shoe["path"] = outline_of(attachment)  # the white silhouette, stretched to the shoe's own size
        shoe["color"] = YELLOW_FLAT
        entries[SLOT_PREFIX + attachment] = (attachment, shoe)
    for attachment in BORDERS:
        entries[SLOT_PREFIX + attachment] = (attachment, find_entry(skins, SHOE_SOURCE_SKIN, attachment))
    for attachment in GAITERS:
        gaiter = find_entry(skins, GAITER_SOURCE_SKIN, attachment)
        gaiter["color"] = YELLOW_ON_GAITER
        entries[SLOT_PREFIX + attachment] = (attachment, gaiter)

    body = ["\t{", '\t\t"name": "%s",' % SKIN, '\t\t"attachments": {']
    slots = list(entries)
    for i, slot in enumerate(slots):
        attachment, value = entries[slot]
        body.append('\t\t\t"%s": {' % slot)
        body.append('\t\t\t\t"%s": %s' % (attachment, one_line(value)))
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
