"""Writes the paper page turn's assets (spec 025 Part A) from the measured data:

  Assets/Data/PaperBook/PaperBook_Level1.asset, PaperBook_Level2.asset   (PaperBookData, from tools/data/paper-book-*.json
                                                                         and tools/data/paper-curl.json)
  Assets/Materials/Paper/PaperOverlay.mat                               (Kami/Paper Overlay)
  Assets/Prefabs/Book/PaperSheet.prefab                                 (PaperSheet: four faces + the edge)

Re-running keeps every GUID. Tuning fields of the data assets (the "Tuning" header) are rewritten with their defaults:
change the defaults here, or edit the asset and don't re-run.

    python tools/make-paper-book-assets.py
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import unity_assets as ua  # noqa: E402

DATA_DIR = 'Assets/Data/PaperBook'
LEVELS = {'Level1': 'level1', 'Level2': 'level2'}


def book_asset(level, key, curl):
    d = json.load(open(os.path.join(ROOT, 'tools', 'data', f'paper-book-{key}.json')))
    script = ua.guid_of('Assets/Scripts/Book/Paper/PaperBookData.cs')
    right = d['profiles']['right']
    left = d['profiles']['left']
    ur = d['uv_map']['right']['affine']
    ul = d['uv_map']['left']['affine']
    v3 = lambda row: f'{{x: {ua.fmt(row[0])}, y: {ua.fmt(row[1])}, z: {ua.fmt(row[2])}}}'
    body = (f'  spineX: {ua.fmt(d["spine_x"])}\n'
            f'  pageZ: {{x: {ua.fmt(d["page_z"][0])}, y: {ua.fmt(d["page_z"][1])}}}\n'
            + ua.float_list('rightOutward', [p[0] for p in right])
            + ua.float_list('rightHeight', [p[1] for p in right])
            + ua.float_list('leftOutward', [p[0] for p in left])
            + ua.float_list('leftHeight', [p[1] for p in left])
            + f'  rightUvRow0: {v3(ur[0])}\n  rightUvRow1: {v3(ur[1])}\n'
            + f'  leftUvRow0: {v3(ul[0])}\n  leftUvRow1: {v3(ul[1])}\n'
            + f'  rightSlot: {d["uv_map"]["right"]["slot"]}\n  leftSlot: {d["uv_map"]["left"]["slot"]}\n'
            + '  curl:\n'
            + f'    lead: {ua.fmt(curl["lead"])}\n    ease: {ua.fmt(curl["ease"])}\n    shape: {ua.fmt(curl["shape"])}\n'
            + f'    duration: {ua.fmt(curl["duration"])}\n    heightScale: 1\n'
            + '  cardFallSign: 1\n  sheetLift: 0.03\n  maxPieceSize: 30\n  flatThicknessRatio: 0.12\n'
            + '  flatMaxThickness: 1.5\n  cardThicknessRatio: 0.25\n  riderMinLift: 1\n  pageThicknessShare: 0.5\n')
    rel = f'{DATA_DIR}/PaperBook_{level}.asset'
    ua.ensure_folder(DATA_DIR)
    ua.write(rel, ua.scriptable_object(script, f'PaperBook_{level}', body))
    g = ua.ensure_meta(rel, ua.NATIVE, main=11400000)
    print(f'[make-paper-book-assets] {rel} ({g})')


def overlay_material():
    shader = ua.guid_of('Assets/Shaders/PaperOverlay/PaperOverlay.shader')
    if shader is None:
        shader = ua.ensure_meta('Assets/Shaders/PaperOverlay/PaperOverlay.shader', ua.SHADER)
        ua.ensure_meta('Assets/Shaders/PaperOverlay', ua.FOLDER)
    ua.ensure_folder('Assets/Materials/Paper')
    rel = 'Assets/Materials/Paper/PaperOverlay.mat'
    ua.write(rel, ua.material('PaperOverlay', shader, textures={'_MainTex': (None, 0)}, colors={'_Color': (1, 1, 1, 1)}, render_type='Transparent'))
    g = ua.ensure_meta(rel, ua.NATIVE, main=2100000)
    print(f'[make-paper-book-assets] {rel} ({g})')
    return g


def sheet_prefab():
    """Root (PaperSheet) with Front, Back and four overlays (MeshFilter + MeshRenderer) and Edge."""
    script = ua.guid_of('Assets/Scripts/Book/Paper/PaperSheet.cs')
    ids = {}
    next_id = [1000000000000000100]

    def nid(key):
        next_id[0] += 7919
        ids[key] = next_id[0]
        return ids[key]

    for k in ('root_go', 'root_tf', 'root_mb'):
        nid(k)
    faces = ['Front', 'Back', 'FrontOverlay', 'BackOverlay', 'StartOverlay', 'EndOverlay']
    for f in faces:
        for k in ('go', 'tf', 'mf', 'mr'):
            nid(f'{f}_{k}')
    nid('Edge_go')
    nid('Edge_tf')

    def go(gid, name, comps):
        c = ''.join(f'  - component: {{fileID: {x}}}\n' for x in comps)
        return (f'--- !u!1 &{gid}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
                f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n{c}'
                f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n'
                f'  m_StaticEditorFlags: 0\n  m_IsActive: 1\n')

    def tf(tid, gid, father, children):
        ch = ''.join(f'  - {{fileID: {x}}}\n' for x in children) if children else ''
        return (f'--- !u!4 &{tid}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
                f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {gid}}}\n'
                f'  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n'
                f'  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n'
                + (f'  m_Children:\n{ch}' if children else '  m_Children: []\n')
                + f'  m_Father: {{fileID: {father}}}\n  m_RootOrder: 0\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n')

    def mf(mid, gid):
        return (f'--- !u!33 &{mid}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
                f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {gid}}}\n'
                f'  m_Mesh: {{fileID: 0}}\n')

    def mr(rid, gid, shadows):
        return (f'--- !u!23 &{rid}\nMeshRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
                f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {gid}}}\n'
                f'  m_Enabled: 1\n  m_CastShadows: {shadows}\n  m_ReceiveShadows: 1\n  m_DynamicOccludee: 1\n'
                f'  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n'
                f'  m_RayTracingMode: 2\n  m_RayTraceProcedural: 0\n  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n'
                f'  m_Materials: []\n  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n'
                f'  m_StaticBatchRoot: {{fileID: 0}}\n  m_ProbeAnchor: {{fileID: 0}}\n  m_LightProbeVolumeOverride: {{fileID: 0}}\n'
                f'  m_ScaleInLightmap: 1\n  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n'
                f'  m_ImportantGI: 0\n  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 3\n  m_MinimumChartSize: 4\n'
                f'  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {{fileID: 0}}\n'
                f'  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {{fileID: 0}}\n')

    out = ['%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n']
    out.append(go(ids['root_go'], 'PaperSheet', [ids['root_tf'], ids['root_mb']]))
    children = [ids[f'{f}_tf'] for f in faces] + [ids['Edge_tf']]
    out.append(tf(ids['root_tf'], ids['root_go'], 0, children))
    out.append(f'--- !u!114 &{ids["root_mb"]}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
               f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ids["root_go"]}}}\n'
               f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n'
               f'  m_Name: \n  m_EditorClassIdentifier: \n'
               f'  _front: {{fileID: {ids["Front_mf"]}}}\n  _back: {{fileID: {ids["Back_mf"]}}}\n'
               f'  _frontOverlay: {{fileID: {ids["FrontOverlay_mf"]}}}\n  _backOverlay: {{fileID: {ids["BackOverlay_mf"]}}}\n'
               f'  _startOverlay: {{fileID: {ids["StartOverlay_mf"]}}}\n  _endOverlay: {{fileID: {ids["EndOverlay_mf"]}}}\n'
               f'  _edge: {{fileID: {ids["Edge_tf"]}}}\n')
    for f in faces:
        out.append(go(ids[f'{f}_go'], f, [ids[f'{f}_tf'], ids[f'{f}_mf'], ids[f'{f}_mr']]))
        out.append(tf(ids[f'{f}_tf'], ids[f'{f}_go'], ids['root_tf'], []))
        out.append(mf(ids[f'{f}_mf'], ids[f'{f}_go']))
        out.append(mr(ids[f'{f}_mr'], ids[f'{f}_go'], 0 if 'Overlay' in f else 1))
    out.append(go(ids['Edge_go'], 'Edge', [ids['Edge_tf']]))
    out.append(tf(ids['Edge_tf'], ids['Edge_go'], ids['root_tf'], []))
    rel = 'Assets/Prefabs/Book/PaperSheet.prefab'
    ua.ensure_folder('Assets/Prefabs/Book')
    ua.write(rel, ''.join(out))
    g = ua.ensure_meta(rel, ua.PREFAB)
    print(f'[make-paper-book-assets] {rel} ({g})')
    return g


def main():
    curl = json.load(open(os.path.join(ROOT, 'tools', 'data', 'paper-curl.json')))
    for level, key in LEVELS.items():
        book_asset(level, key, curl)
    overlay_material()
    sheet_prefab()


if __name__ == '__main__':
    main()
