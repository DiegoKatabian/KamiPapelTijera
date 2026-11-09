"""Shared helpers for tools that write Unity files from Python (spec 025 D1).

What is written directly as YAML here is only what Unity reads back unchanged and what this project has done safely
before: .meta files, ScriptableObject (MonoBehaviour) assets, materials, plain prefabs. Meshes and animation clips are
NOT written here: Python computes them into JSON and a C# ScriptedImporter builds them (Assets/Editor/Generated/), so
their imports can be read back from Library/Artifacts and checked.

Every writer keeps an existing .meta (and so the GUID every reference uses) when it re-runs.
"""
import os
import re
import subprocess
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def path(rel):
    return os.path.join(ROOT, rel)


def free_guid():
    while True:
        g = uuid.uuid4().hex
        r = subprocess.call(['grep', '-rqs', g, 'Assets', 'ProjectSettings', 'Packages'], cwd=ROOT)
        if r != 0:
            return g


def guid_of(rel):
    m = path(rel) + '.meta'
    if not os.path.exists(m):
        return None
    return re.search(r'guid: (\w+)', open(m, encoding='utf-8').read()).group(1)


FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SCRIPT = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SHADER = """fileFormatVersion: 2
guid: {guid}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  userData:
  assetBundleName:
  assetBundleVariant:
"""

NATIVE = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: {main}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

PREFAB = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SCRIPTED = """fileFormatVersion: 2
guid: {guid}
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {{fileID: 11500000, guid: {importer}, type: 3}}
"""

TEXTURE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 11
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {srgb}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def ensure_meta(rel, template, **fields):
    """Writes rel.meta from a template unless it exists; returns the GUID (existing or new)."""
    existing = guid_of(rel)
    if existing:
        return existing
    g = free_guid()
    os.makedirs(os.path.dirname(path(rel)), exist_ok=True)
    open(path(rel) + '.meta', 'w', encoding='utf-8', newline='\n').write(template.format(guid=g, **fields))
    return g


def ensure_folder(rel):
    """Creates a folder (and its parents) under Assets with .meta files."""
    parts = rel.replace('\\', '/').split('/')
    for i in range(2, len(parts) + 1):
        sub = '/'.join(parts[:i])
        os.makedirs(path(sub), exist_ok=True)
        ensure_meta(sub, FOLDER)


def write(rel, text):
    os.makedirs(os.path.dirname(path(rel)), exist_ok=True)
    open(path(rel), 'w', encoding='utf-8', newline='\n').write(text)


def fmt(v):
    """A float as Unity writes it."""
    if isinstance(v, bool):
        return '1' if v else '0'
    if isinstance(v, int):
        return str(v)
    r = repr(float(v))
    if r.endswith('.0'):
        r = r[:-2]
    return r


def float_list(name, values, indent='  '):
    if not values:
        return f'{indent}{name}: []\n'
    return f'{indent}{name}:\n' + ''.join(f'{indent}- {fmt(v)}\n' for v in values)


def scriptable_object(script_guid, name, body):
    """A ScriptableObject asset (MonoBehaviour at fileID 11400000). body = the serialized fields, two-space indented."""
    return ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
            '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
            '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            f'  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n'
            + body)


def material(name, shader_guid, textures=None, floats=None, colors=None, shader_file_id=4800000, keywords=None,
             render_type='Opaque'):
    """A material asset (fileID 2100000, Unity 2021.3's serializedVersion 8). textures: {prop: (guid or None, fileID)}."""
    textures = textures or {}
    floats = floats or {}
    colors = colors or {}
    keywords = keywords or []
    nl = '\n'
    tex = ''
    for k, (g, fid) in textures.items():
        ref = f'{{fileID: {fid}, guid: {g}, type: 3}}' if g else '{fileID: 0}'
        tex += f'    - {k}:{nl}        m_Texture: {ref}{nl}        m_Scale: {{x: 1, y: 1}}{nl}        m_Offset: {{x: 0, y: 0}}{nl}'
    fl = ''.join(f'    - {k}: {fmt(v)}{nl}' for k, v in floats.items())
    co = ''.join(f'    - {k}: {{r: {fmt(c[0])}, g: {fmt(c[1])}, b: {fmt(c[2])}, a: {fmt(c[3])}}}{nl}' for k, c in colors.items())
    kw = (f'  m_ValidKeywords:{nl}' + ''.join(f'  - {k}{nl}' for k in keywords)) if keywords else f'  m_ValidKeywords: []{nl}'
    out = [
        '%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!21 &2100000', 'Material:', '  serializedVersion: 8',
        '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}', f'  m_Name: {name}',
        f'  m_Shader: {{fileID: {shader_file_id}, guid: {shader_guid}, type: 3}}',
    ]
    text = nl.join(out) + nl + kw
    text += nl.join(['  m_InvalidKeywords: []', '  m_LightmapFlags: 4', '  m_EnableInstancingVariants: 0',
                     '  m_DoubleSidedGI: 0', '  m_CustomRenderQueue: -1', '  stringTagMap:', f'    RenderType: {render_type}',
                     '  disabledShaderPasses: []', '  m_SavedProperties:', '    serializedVersion: 3']) + nl
    text += (f'    m_TexEnvs:{nl}' + tex) if tex else f'    m_TexEnvs: []{nl}'
    text += f'    m_Ints: []{nl}'
    text += (f'    m_Floats:{nl}' + fl) if fl else f'    m_Floats: []{nl}'
    text += (f'    m_Colors:{nl}' + co) if co else f'    m_Colors: []{nl}'
    text += f'  m_BuildTextureStacks: []{nl}'
    return text


URP_LIT = '933532a4fcc9baf4fa0491de14d08ed7'


def urp_lit(name, base_map_guid=None, base_color=(1, 1, 1, 1), smoothness=0.1, specular_highlights=False):
    """A URP Lit material (opaque), copied from the project's own Lit materials' property set."""
    tex = {'_BaseMap': (base_map_guid, 2800000), '_MainTex': (base_map_guid, 2800000)}
    for k in ('_BumpMap', '_DetailAlbedoMap', '_DetailMask', '_DetailNormalMap', '_EmissionMap', '_MetallicGlossMap',
              '_OcclusionMap', '_ParallaxMap', '_SpecGlossMap'):
        tex[k] = (None, 0)
    floats = {'_AlphaClip': 0, '_Blend': 0, '_BumpScale': 1, '_ClearCoatMask': 0, '_ClearCoatSmoothness': 0, '_Cull': 2,
              '_Cutoff': 0.5, '_DetailAlbedoMapScale': 1, '_DetailNormalMapScale': 1, '_DstBlend': 0,
              '_EnvironmentReflections': 1, '_GlossMapScale': 0, '_Glossiness': 0, '_GlossyReflections': 0,
              '_Metallic': 0, '_OcclusionStrength': 1, '_Parallax': 0.005, '_QueueOffset': 0, '_ReceiveShadows': 1,
              '_Smoothness': smoothness, '_SmoothnessTextureChannel': 0,
              '_SpecularHighlights': 1 if specular_highlights else 0, '_SrcBlend': 1, '_Surface': 0, '_WorkflowMode': 1,
              '_ZWrite': 1}
    colors = {'_BaseColor': base_color, '_Color': base_color, '_EmissionColor': (0, 0, 0, 1), '_SpecColor': (0.2, 0.2, 0.2, 1)}
    kws = [] if specular_highlights else ['_SPECULARHIGHLIGHTS_OFF']
    return material(name, URP_LIT, tex, floats, colors, keywords=kws)
