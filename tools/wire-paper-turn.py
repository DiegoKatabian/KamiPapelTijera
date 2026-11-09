"""Wires the paper page turn (spec 025 Part A) into both level scenes, by YAML surgery:

  - a root GameObject "PaperTurn" with the PaperTurn component (book data, the book's renderer, the sheet prefab, the
    edge sparkles, the overlay material);
  - PageScrollerManager's `_paperPages` on and `_paperTurn` pointing at it (Level 2: the scene's own component; Level 1:
    overrides on its PageScrollerManager.prefab instance);
  - Level 1 also gets the stripped block for the book's renderer inside the LibroUVsfbx instance.

Idempotent: a scene that already has the PaperTurn object is left alone. Checks block counts and fileID uniqueness.

    python tools/wire-paper-turn.py
"""
import os
import random
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402

LIBRO_FBX = 'e04bb88841cf74a45970f15e616cb90b'
LIBRO_RENDERER = -7511558181221131132  # the MeshRenderer inside LibroUVsfbx.fbx (from its scene overrides)
PSM_SCRIPT = '62efb52a7ac66f84798a8b8f482d9cb2'


def prefab_ids(rel):
    txt = open(ua.path(rel), encoding='utf-8').read()
    return txt


def sheet_component_id():
    txt = prefab_ids('Assets/Prefabs/Book/PaperSheet.prefab')
    script = ua.guid_of('Assets/Scripts/Book/Paper/PaperSheet.cs')
    m = re.search(r'--- !u!114 &(\d+)\nMonoBehaviour:(?:(?!--- !u!).)*?guid: ' + script, txt, re.S)
    return int(m.group(1))


def glitter_root_id():
    txt = prefab_ids('Assets/Prefabs/Hoja/GlitterHoja.prefab')
    for m in re.finditer(r'--- !u!4 &(\d+)\nTransform:(.*?)(?=\n--- !u!|\Z)', txt, re.S):
        if 'm_Father: {fileID: 0}' in m.group(2):
            go = re.search(r'm_GameObject: \{fileID: (\d+)\}', m.group(2)).group(1)
            return int(go)
    raise RuntimeError('GlitterHoja root not found')


def new_id(used):
    while True:
        v = random.randint(10 ** 9, 2 ** 62)
        if v not in used:
            used.add(v)
            return v


def count_blocks(txt):
    return len(re.findall(r'^--- !u!', txt, re.M))


def ids_in(txt):
    return [int(x) for x in re.findall(r'^--- !u!\d+ &(-?\d+)', txt, re.M)]


def root_count(txt):
    return len(re.findall(r'^  m_Father: \{fileID: 0\}', txt, re.M))


def paper_turn_blocks(go, tf, mb, book_guid, renderer_id, root_order):
    sheet = sheet_component_id()
    sheet_guid = ua.guid_of('Assets/Prefabs/Book/PaperSheet.prefab')
    glitter = glitter_root_id()
    glitter_guid = ua.guid_of('Assets/Prefabs/Hoja/GlitterHoja.prefab')
    mat = ua.guid_of('Assets/Materials/Paper/PaperOverlay.mat')
    script = ua.guid_of('Assets/Scripts/Book/Paper/PaperTurn.cs')
    return (f'--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
            f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n'
            f'  - component: {{fileID: {tf}}}\n  - component: {{fileID: {mb}}}\n  m_Layer: 0\n  m_Name: PaperTurn\n'
            f'  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
            f'--- !u!4 &{tf}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
            f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n'
            f'  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n'
            f'  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n'
            f'  m_Father: {{fileID: 0}}\n  m_RootOrder: {root_order}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
            f'--- !u!114 &{mb}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
            f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n'
            f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n'
            f'  m_Name: \n  m_EditorClassIdentifier: \n'
            f'  _book: {{fileID: 11400000, guid: {book_guid}, type: 2}}\n'
            f'  _bookRenderer: {{fileID: {renderer_id}}}\n'
            f'  _sheetPrefab: {{fileID: {sheet}, guid: {sheet_guid}, type: 3}}\n'
            f'  _edgeEffect: {{fileID: {glitter}, guid: {glitter_guid}, type: 3}}\n'
            f'  _overlayMaterial: {{fileID: 2100000, guid: {mat}, type: 2}}\n'
            f'  _captureFlat: 1\n  _captureLayer: 31\n  _captureWidth: 1024\n'
            f'  _logPieces: 0\n  _pause: 0\n  _speed: 1\n  _useScrub: 0\n  _scrub: 0\n'
            f'  _drawCards: 1\n  _drawVolumes: 1\n  _drawRiders: 1\n  _drawFlat: 1\n  _gizmos: 1\n')


def wire_level2():
    rel = 'Assets/Scenes/Level2_Newspaper.unity'
    txt = open(ua.path(rel), encoding='utf-8').read()
    if 'm_Name: PaperTurn\n' in txt:
        print(f'[wire-paper-turn] {rel}: already wired')
        return
    before = count_blocks(txt)
    used = set(ids_in(txt))
    go, tf, mb = new_id(used), new_id(used), new_id(used)
    # the book renderer BookPageSpread already uses
    bps = ua.guid_of('Assets/Scripts/Book/BookPageSpread.cs')
    m = re.search(r'guid: ' + bps + r'.*?\n  _book: \{fileID: (\d+)\}', txt, re.S)
    renderer = int(m.group(1))
    book = ua.guid_of('Assets/Data/PaperBook/PaperBook_Level2.asset')
    # PageScrollerManager: the scene's own component
    psm = re.search(r'(  m_Script: \{fileID: 11500000, guid: ' + PSM_SCRIPT + r', type: 3\}(?:(?!--- !u!).)*?  DelayBetweenCamCloseUpAndCamBook: [^\n]*\n)', txt, re.S)
    if psm is None:
        raise RuntimeError('Level 2 PageScrollerManager not found')
    block = psm.group(1)
    txt = txt.replace(block, block + f'  _paperPages: 1\n  _paperTurn: {{fileID: {mb}}}\n', 1)
    txt = txt.rstrip('\n') + '\n' + paper_turn_blocks(go, tf, mb, book, renderer, root_count(txt))
    check_and_write(rel, txt, before + 3)


def wire_level1():
    rel = 'Assets/Scenes/Nivel1_KamiPapelTijera.unity'
    txt = open(ua.path(rel), encoding='utf-8').read()
    if 'm_Name: PaperTurn\n' in txt:
        print(f'[wire-paper-turn] {rel}: already wired')
        return
    before = count_blocks(txt)
    used = set(ids_in(txt))
    go, tf, mb = new_id(used), new_id(used), new_id(used)
    # the book's renderer: a stripped block inside the LibroUVsfbx instance
    inst = None
    for m in re.finditer(r'--- !u!1001 &(\d+)\nPrefabInstance:(?:(?!--- !u!).)*?m_SourcePrefab: \{fileID: 100100000, guid: (\w+), type: 3\}', txt, re.S):
        if m.group(2) == LIBRO_FBX:
            inst = int(m.group(1))
    if inst is None:
        raise RuntimeError('Level 1 book instance not found')
    renderer = (LIBRO_RENDERER ^ inst) & 0x7FFFFFFFFFFFFFFF
    stripped = ''
    if f'--- !u!23 &{renderer} stripped' not in txt:
        stripped = (f'--- !u!23 &{renderer} stripped\nMeshRenderer:\n'
                    f'  m_CorrespondingSourceObject: {{fileID: {LIBRO_RENDERER}, guid: {LIBRO_FBX}, type: 3}}\n'
                    f'  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n')
    # PageScrollerManager: overrides on its prefab instance
    psm_prefab = ua.guid_of('Assets/Prefabs/Managers/PageScrollerManager.prefab')
    ptxt = open(ua.path('Assets/Prefabs/Managers/PageScrollerManager.prefab'), encoding='utf-8').read()
    comp = re.search(r'--- !u!114 &(\d+)\nMonoBehaviour:(?:(?!--- !u!).)*?guid: ' + PSM_SCRIPT, ptxt, re.S).group(1)
    pi = re.search(r'(--- !u!1001 &(\d+)\nPrefabInstance:(?:(?!--- !u!).)*?m_Modifications:\n)((?:(?!--- !u!).)*?m_SourcePrefab: \{fileID: 100100000, guid: ' + psm_prefab + r')', txt, re.S)
    if pi is None:
        raise RuntimeError('Level 1 PageScrollerManager instance not found')
    mods = (f'    - target: {{fileID: {comp}, guid: {psm_prefab}, type: 3}}\n      propertyPath: _paperPages\n'
            f'      value: 1\n      objectReference: {{fileID: 0}}\n'
            f'    - target: {{fileID: {comp}, guid: {psm_prefab}, type: 3}}\n      propertyPath: _paperTurn\n'
            f'      value: \n      objectReference: {{fileID: {mb}}}\n')
    txt = txt.replace(pi.group(1), pi.group(1) + mods, 1)
    book = ua.guid_of('Assets/Data/PaperBook/PaperBook_Level1.asset')
    txt = txt.rstrip('\n') + '\n' + paper_turn_blocks(go, tf, mb, book, renderer, root_count(txt)) + stripped
    check_and_write(rel, txt, before + 3 + (1 if stripped else 0))


def check_and_write(rel, txt, expected_blocks):
    n = count_blocks(txt)
    ids = ids_in(txt)
    if n != expected_blocks:
        raise RuntimeError(f'{rel}: {n} blocks, expected {expected_blocks}')
    if len(ids) != len(set(ids)):
        raise RuntimeError(f'{rel}: duplicate fileIDs')
    open(ua.path(rel), 'w', encoding='utf-8', newline='\n').write(txt)
    print(f'[wire-paper-turn] {rel}: wired ({n} blocks, fileIDs unique)')


if __name__ == '__main__':
    wire_level2()
    wire_level1()
