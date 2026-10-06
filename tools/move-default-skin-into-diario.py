#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Moves the detective pieces out of Kami's `default` skin into `FullSkins/Kami Diario` (Atlas 13).

Why it exists: Atlas 13 exports `default` with three attachments (the coat `tapado` and the two
`22_leg_muslo_detective` thighs). Spine always consults `default` after the active skin, so every
outfit would draw them: Kami Libro 1 (Level 1) got the detective coat on top of her normal clothes.
Kami Gear composes her look from outfit + scissors + shoes, so `default` has to stay empty. Moving the
entries into Diario changes nothing for Level 2 (Diario is always on there) and cleans up Level 1.

A re-export from Spine overwrites skeleton.json: run this again on the new export if Valen hasn't
moved them at the source yet. Idempotent. The move is done as text, so the rest of the file is
untouched.

Usage:  python tools/move-default-skin-into-diario.py "Assets/2D/Kami Spine/Atlas 13/skeleton.json"
"""
import io
import json
import sys

DEFAULT_HEAD = '\t{\n\t\t"name": "default",\n\t\t"attachments": {\n'
DIARIO_HEAD = '\t{\n\t\t"name": "FullSkins/Kami Diario",\n\t\t"transform": [ "Bun-Detective", "DetectiveBow" ],\n\t\t"attachments": {\n'


def main(path):
    text = io.open(path, encoding="utf-8", newline="").read()
    if "\r\n" in text:
        sys.exit("unexpected CRLF line endings in the export")
    data = json.loads(text)
    skins = {s["name"]: s for s in data["skins"]}
    if "default" not in skins or "FullSkins/Kami Diario" not in skins:
        sys.exit("the export has no `default` or `FullSkins/Kami Diario` skin: update this script")
    if not skins["default"].get("attachments"):
        print("`default` is already empty: nothing to do")
        return
    clash = set(skins["default"]["attachments"]) & set(skins["FullSkins/Kami Diario"]["attachments"])
    if clash:
        sys.exit("Diario already has slots %s: merge by hand" % sorted(clash))

    start = text.index(DEFAULT_HEAD) + len(DEFAULT_HEAD)
    diario_at = text.index(DIARIO_HEAD)
    end = text.index("\t\t}\n\t},\n", start)  # closes `default`'s attachments, then the skin
    if end > diario_at:
        sys.exit("`default` is not laid out the way this script expects: update it")
    body = text[start:end]  # the entries, each already indented for a skin's attachments

    # `default` becomes empty, its entries go first in Diario's attachments
    text = text[:start] + text[end:]
    diario_at = text.index(DIARIO_HEAD) + len(DIARIO_HEAD)
    text = text[:diario_at] + body.rstrip("\n") + ",\n" + text[diario_at:]

    check = json.loads(text)
    moved = {s["name"]: s for s in check["skins"]}
    assert not moved["default"].get("attachments"), "default still has attachments"
    assert set(skins["default"]["attachments"]) <= set(moved["FullSkins/Kami Diario"]["attachments"])
    io.open(path, "w", encoding="utf-8", newline="").write(text)
    print("moved %d slots from `default` to `FullSkins/Kami Diario`" % len(skins["default"]["attachments"]))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
