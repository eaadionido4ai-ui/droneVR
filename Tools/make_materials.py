#!/usr/bin/env python3
"""Writes the four materials DRONE VR uses (Unity/Assets/DRONEVR/Resources/DRONEVR/Materials).

    python3 Tools/make_materials.py

They are plain Universal Render Pipeline materials, written as text so the project needs no hand work in the
Unity editor. You can change them in Unity afterwards; run this again only if you want them back as written.
  DronePalette  lit, matte, drawn from both sides; its picture is the palette of flat colors
  DronePicture  unlit, cut out where the picture is see-through (signs)
  DroneFlat     unlit, one plain color (panels, buttons, markers); also shows the drone camera's picture on the screen
  DroneWater    unlit, see-through (the river and the flood)
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import guid_of, ASSETS

LIT = '933532a4fcc9baf4fa0491de14d08ed7'      # Universal Render Pipeline/Lit
UNLIT = '650dd9526735d5b46b79224bc6e94025'    # Universal Render Pipeline/Unlit
OUT = os.path.join(ASSETS, 'DRONEVR', 'Resources', 'DRONEVR', 'Materials')

HEAD = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-7058409539637385116
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  version: 10
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:{keywords}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap:
    RenderType: {rendertype}
  disabledShaderPasses:{disabled}
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {texture}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {texture}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:{floats}
    m_Colors:
    - _BaseColor: {color}
    - _Color: {color}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _SpecColor: {{r: 0.2, g: 0.2, b: 0.2, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""


def lst(items):
    return ' []' if not items else ''.join('\n  - ' + i for i in items)


def floats(d):
    return ''.join('\n    - %s: %s' % (k, d[k]) for k in sorted(d))


def write(name, shader, keywords, queue, rendertype, disabled, texture, f, color):
    os.makedirs(OUT, exist_ok=True)
    text = HEAD.format(name=name, shader=shader, keywords=lst(keywords), queue=queue, rendertype=rendertype, disabled=lst(disabled),
                       texture=texture, floats=floats(f), color=color)
    open(os.path.join(OUT, name + '.mat'), 'w', encoding='utf-8', newline='\n').write(text)


def main():
    palette = '{fileID: 2800000, guid: %s, type: 3}' % guid_of('DRONEVR/Resources/DRONEVR/Textures/palette.png')
    none = '{fileID: 0}'
    white = '{r: 1, g: 1, b: 1, a: 1}'
    opaque = dict(_Surface=0, _Blend=0, _SrcBlend=1, _DstBlend=0, _SrcBlendAlpha=1, _DstBlendAlpha=0, _ZWrite=1, _AlphaClip=0, _Cutoff=0.5,
                  _Cull=0, _QueueOffset=0, _BlendOp=0, _AlphaToMask=0)
    lit = dict(opaque, _Smoothness=0, _Glossiness=0, _Metallic=0, _WorkflowMode=1, _SpecularHighlights=0, _EnvironmentReflections=0,
               _ReceiveShadows=0, _BumpScale=1, _OcclusionStrength=1, _GlossMapScale=0, _SmoothnessTextureChannel=0)
    write('DronePalette', LIT, ['_ENVIRONMENTREFLECTIONS_OFF', '_RECEIVE_SHADOWS_OFF', '_SPECULARHIGHLIGHTS_OFF'], -1, 'Opaque',
          ['MOTIONVECTORS'], palette, lit, white)
    write('DronePicture', UNLIT, ['_ALPHATEST_ON'], 2450, 'TransparentCutout', ['MOTIONVECTORS'], none,
          dict(opaque, _AlphaClip=1, _AlphaToMask=1), white)
    write('DroneFlat', UNLIT, [], -1, 'Opaque', ['MOTIONVECTORS'], none, opaque, white)
    water = dict(opaque, _Surface=1, _SrcBlend=5, _DstBlend=10, _SrcBlendAlpha=1, _DstBlendAlpha=10, _ZWrite=0)
    write('DroneWater', UNLIT, ['_SURFACE_TYPE_TRANSPARENT'], 3000, 'Transparent', ['MOTIONVECTORS', 'DepthOnly', 'SHADOWCASTER'], none,
          water, '{r: 0.3, g: 0.5, b: 0.62, a: 0.8}')
    print('materials written to', OUT)


if __name__ == '__main__':
    main()
