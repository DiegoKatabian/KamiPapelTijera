#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Re-point prefab instances at a prefab VARIANT of the prefab they use today, keeping every override.

Unity gives a variant's objects fileIDs of (base id XOR the variant's own PrefabInstance id), so swapping an
instance's source means XOR-ing every override target (and every stripped block that points at it) with the new
variant's PrefabInstance id. Unity can't do that by hand any more, so this does it in text.

Usage:
  python tools/replace-prefab-instance.py <file> <old-guid> <new-guid> <new-variant-instance-id> <instance-id>...

<new-variant-instance-id> is the number after `--- !u!1001 &` in the NEW variant's .prefab file.
Only valid when the new prefab is a variant (directly) of the old one. Writes via a temp file (never truncates).
Close the scene in Unity first, or reload it afterwards.
"""
import os, re, sys

M = 0x7FFFFFFFFFFFFFFF


def xor(a, b):
    return (a ^ b) & M


def next_header(text, start):
    m = re.search(r'(?m)^--- !u!\d+ &-?\d+', text[start + 5:])
    return start + 5 + m.start() if m else len(text)


def retarget(text, inst_id, old_guid, new_guid, new_inst):
    s = text.index('--- !u!1001 &%s\n' % inst_id)
    e = next_header(text, s)
    blk = text[s:e]
    assert 'guid: ' + old_guid in blk, 'instance %s does not use %s' % (inst_id, old_guid)
    blk = re.sub(r'(target: \{fileID: )(-?\d+)(, guid: )' + old_guid,
                 lambda m: '%s%d%s%s' % (m.group(1), xor(int(m.group(2)), new_inst), m.group(3), new_guid), blk)
    blk = blk.replace('m_SourcePrefab: {fileID: 100100000, guid: ' + old_guid, 'm_SourcePrefab: {fileID: 100100000, guid: ' + new_guid)
    text = text[:s] + blk + text[e:]
    pos = 0
    while True:
        m = re.search(r'(?m)^--- !u!\d+ &-?\d+ stripped\n', text[pos:])
        if not m:
            return text
        s2 = pos + m.start()
        e2 = next_header(text, s2)
        b = text[s2:e2]
        if 'm_PrefabInstance: {fileID: %s}' % inst_id in b and 'guid: ' + old_guid in b:
            b = re.sub(r'(m_CorrespondingSourceObject: \{fileID: )(-?\d+)(, guid: )' + old_guid,
                       lambda m4: '%s%d%s%s' % (m4.group(1), xor(int(m4.group(2)), new_inst), m4.group(3), new_guid), b)
            text = text[:s2] + b + text[e2:]
            e2 = s2 + len(b)
        pos = e2


def main():
    path, old_guid, new_guid, new_inst = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
    text = open(path, encoding='utf-8', newline='').read()
    for inst in sys.argv[5:]:
        text = retarget(text, inst, old_guid, new_guid, new_inst)
        print('instance %s -> %s' % (inst, new_guid))
    tmp = path + '.tmp_write'
    with open(tmp, 'wb') as fh:
        fh.write(text.encode('utf-8'))
    os.replace(tmp, path)


if __name__ == '__main__':
    main()
