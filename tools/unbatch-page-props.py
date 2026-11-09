"""Removes ONLY the Batching Static flag (bit 4 of m_StaticEditorFlags) from everything under the level pages' folders
(spec 025 D37, Diego's OK 2026-11-06): a statically batched renderer reports the combined mesh, so the paper page turn
can't copy it. Navigation Static and every other flag stay.

Covers, under each PageScrollerManager's pagesToToggle folders:
  - scene-owned GameObjects,
  - prefab-instance overrides of m_StaticEditorFlags,
  - prefab assets instantiated there whose own GameObjects carry the flag (and the prefabs they nest).

    python tools/unbatch-page-props.py          # report only
    python tools/unbatch-page-props.py --apply  # write
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402

SCENES = ['Assets/Scenes/Nivel1_KamiPapelTijera.unity', 'Assets/Scenes/Level2_Newspaper.unity']
PSM_SCRIPT = '62efb52a7ac66f84798a8b8f482d9cb2'
BATCH = 4

GUID_TO_PATH = {}


def guid_map():
    if GUID_TO_PATH:
        return GUID_TO_PATH
    for base, _, files in os.walk(ua.path('Assets')):
        for f in files:
            if f.endswith('.prefab.meta'):
                p = os.path.join(base, f)
                m = re.search(r'guid: (\w+)', open(p, encoding='utf-8', errors='ignore').read(300))
                if m:
                    GUID_TO_PATH[m.group(1)] = os.path.relpath(p[:-5], ROOT).replace('\\', '/')
    return GUID_TO_PATH


def blocks(txt):
    return list(re.finditer(r'^--- !u!(\d+) &(-?\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)', txt, re.S | re.M))


def page_folders(txt, psm_prefab_guid):
    """GameObject fileIDs of the page folders (scene component or prefab-instance overrides of pagesToToggle)."""
    m = re.search(r'guid: ' + PSM_SCRIPT + r', type: 3\}.*?pagesToToggle:\n((?:  - \{fileID: \d+\}\n)+)', txt, re.S)
    if m:
        return [int(x) for x in re.findall(r'fileID: (\d+)', m.group(1))]
    ids = []
    for mm in re.finditer(r'propertyPath: pagesToToggle\.Array\.data\[\d+\]\n      value: *\n      objectReference: \{fileID: (\d+)\}', txt):
        ids.append(int(mm.group(1)))
    return ids


def analyse(rel, apply):
    txt = open(ua.path(rel), encoding='utf-8').read()
    bl = blocks(txt)
    go_tf = {}      # GO id -> Transform id
    tf_parent = {}  # Transform id -> parent Transform id
    tf_go = {}
    inst_parent = {}  # PrefabInstance id -> parent Transform id
    inst_src = {}
    stripped_tf_inst = {}  # stripped Transform id -> PrefabInstance id
    for b in bl:
        t, i, stripped, body = int(b.group(1)), int(b.group(2)), b.group(3), b.group(4)
        if t == 4:
            if stripped:
                pi = re.search(r'm_PrefabInstance: \{fileID: (\d+)\}', body)
                if pi:
                    stripped_tf_inst[i] = int(pi.group(1))
                continue
            g = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1))
            f = int(re.search(r'm_Father: \{fileID: (\d+)\}', body).group(1))
            tf_parent[i] = f
            tf_go[i] = g
            go_tf[g] = i
        elif t == 1001:
            p = re.search(r'm_TransformParent: \{fileID: (\d+)\}', body)
            inst_parent[i] = int(p.group(1)) if p else 0
            s = re.search(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)', body)
            inst_src[i] = s.group(1) if s else None
    folders = set(page_folders(txt, None))
    # a page folder may itself be a prefab instance root (Level 1: Objetos_Pagina_N.prefab): resolve its GO through
    # stripped GameObjects
    folder_tfs = set()
    for b in bl:
        t, i, stripped, body = int(b.group(1)), int(b.group(2)), b.group(3), b.group(4)
        if i in folders:
            if t == 1 and not stripped:
                folder_tfs.add(go_tf.get(i))
            elif t == 1 and stripped:
                pi = int(re.search(r'm_PrefabInstance: \{fileID: (\d+)\}', body).group(1))
                for st, sp in stripped_tf_inst.items():
                    if sp == pi:
                        folder_tfs.add(st)
                folder_tfs.add(('inst', pi))

    def under_page(tf_id, depth=0):
        while tf_id and depth < 200:
            if tf_id in folder_tfs:
                return True
            if tf_id in tf_parent:
                tf_id = tf_parent[tf_id]
            elif tf_id in stripped_tf_inst:
                pi = stripped_tf_inst[tf_id]
                if ('inst', pi) in folder_tfs:
                    return True
                tf_id = inst_parent.get(pi, 0)
            else:
                return False
            depth += 1
        return False

    changes = []
    prefabs = set()
    # scene-owned GameObjects
    for b in bl:
        t, i, stripped, body = int(b.group(1)), int(b.group(2)), b.group(3), b.group(4)
        if t == 1 and not stripped:
            fm = re.search(r'm_StaticEditorFlags: (\d+)', body)
            if fm and int(fm.group(1)) & BATCH and i in go_tf and under_page(go_tf[i]):
                name = re.search(r'm_Name: (.*)', body).group(1)
                changes.append(('go', b, int(fm.group(1)), name))
    # prefab instances under pages: overrides + the prefab assets
    for pi, parent in inst_parent.items():
        if not under_page(parent) and ('inst', pi) not in folder_tfs:
            continue
        if inst_src.get(pi):
            prefabs.add(inst_src[pi])
    mods = list(re.finditer(r'(--- !u!1001 &(\d+)\nPrefabInstance:.*?)(?=^--- !u!|\Z)', txt, re.S | re.M))
    override_count = 0
    for m in mods:
        pi = int(m.group(2))
        if not (under_page(inst_parent.get(pi, 0)) or ('inst', pi) in folder_tfs):
            continue
        for om in re.finditer(r'propertyPath: m_StaticEditorFlags\n      value: (\d+)', m.group(1)):
            if int(om.group(1)) & BATCH:
                override_count += 1
    print(f'[unbatch-page-props] {rel}: {len(folders)} page folders, {len(changes)} scene objects and '
          f'{override_count} prefab-instance overrides with Batching Static, {len(prefabs)} prefabs used on pages')
    if apply:
        new = txt
        for kind, b, flags, name in changes:
            body = b.group(0)
            new = new.replace(body, body.replace(f'm_StaticEditorFlags: {flags}', f'm_StaticEditorFlags: {flags & ~BATCH}', 1), 1)
        for m in mods:
            pi = int(m.group(2))
            if not (under_page(inst_parent.get(pi, 0)) or ('inst', pi) in folder_tfs):
                continue
            block = m.group(1)
            fixed = re.sub(r'(propertyPath: m_StaticEditorFlags\n      value: )(\d+)',
                           lambda mm: mm.group(1) + str(int(mm.group(2)) & ~BATCH), block)
            if fixed != block:
                new = new.replace(block, fixed, 1)
        if new != txt:
            if len(re.findall(r'^--- !u!', new, re.M)) != len(bl):
                raise RuntimeError('block count changed')
            open(ua.path(rel), 'w', encoding='utf-8', newline='\n').write(new)
    return prefabs


def prefab_closure(guids):
    gm = guid_map()
    seen = set()
    todo = list(guids)
    while todo:
        g = todo.pop()
        if g in seen or g not in gm:
            continue
        seen.add(g)
        txt = open(ua.path(gm[g]), encoding='utf-8', errors='ignore').read()
        for s in re.findall(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)', txt):
            todo.append(s)
    return seen


def fix_prefabs(guids, apply):
    gm = guid_map()
    total = 0
    for g in sorted(prefab_closure(guids), key=lambda x: gm.get(x, '')):
        rel = gm[g]
        txt = open(ua.path(rel), encoding='utf-8').read()
        n = 0

        def sub(mm):
            nonlocal n
            v = int(mm.group(2))
            if v & BATCH:
                n += 1
                return mm.group(1) + str(v & ~BATCH)
            return mm.group(0)

        new = re.sub(r'(m_StaticEditorFlags: )(\d+)', sub, txt)
        new = re.sub(r'(propertyPath: m_StaticEditorFlags\n      value: )(\d+)', sub, new)
        if n:
            total += n
            print(f'  {rel}: {n} flags')
            if apply:
                open(ua.path(rel), 'w', encoding='utf-8', newline='').write(new)
    print(f'[unbatch-page-props] prefabs used on pages: {total} Batching Static flags {"removed" if apply else "found"}')


def main():
    apply = '--apply' in sys.argv
    prefabs = set()
    for s in SCENES:
        prefabs |= analyse(s, apply)
    fix_prefabs(prefabs, apply)


if __name__ == '__main__':
    main()
