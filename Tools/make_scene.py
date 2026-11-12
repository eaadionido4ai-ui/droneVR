#!/usr/bin/env python3
"""Writes DRONE VR's main scene, and the build settings, from the SIMPLE template's FPS player scene.

    python3 Tools/make_scene.py

The template scene (Unity/Assets/Scenes/GAMA/Templates/Main Scene - FPS Player.unity) has what every SIMPLE
game needs: the Connection Manager, a Game Manager, the FPS Player and a light. This keeps all of it, with
three changes:
  - the Game Manager runs DroneSimulationManager (DRONE VR's SimulationManager);
  - the Connection Manager reads the IP address chosen in the template's IP Menu (fixedProperties off);
  - a DRONE VR object runs DroneApp, which builds the barangay, the operations center and the missions.
The scene is saved as Unity/Assets/DRONEVR/Scenes/Main Scene.unity: the template's Startup Menu opens a
scene by the name "Main Scene". The build settings list the Startup Menu, the IP Menu, this scene and the
End of Game Menu. You can also open the scene in Unity and change it there.
"""
import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import guid_of, ASSETS

SRC = os.path.join(ASSETS, 'Scenes', 'GAMA', 'Templates', 'Main Scene - FPS Player.unity')
OUT_DIR = os.path.join(ASSETS, 'DRONEVR', 'Scenes')
OUT = os.path.join(OUT_DIR, 'Main Scene.unity')

MANAGER_TRANSFORM = '649356473'       # the "Manager" object's Transform
GAME_MANAGER_PI = '653474307'         # the template's Game Manager (a prefab instance), replaced
GAME_MANAGER_STRIPPED = '653474308'
FPS_PLAYER_GO = '1303823318'          # the FPS Player's GameObject in the scene
CONNECTION_PI = '409981439'

GM_GO, GM_TR, GM_MB = '700000001', '700000002', '700000003'
APP_GO, APP_TR, APP_MB = '700000011', '700000012', '700000013'


def blocks(text):
    parts = re.split(r'(?m)^(--- !u!\d+ &-?\d+(?: stripped)?\n)', text)
    head = parts[0]
    out = []
    for i in range(1, len(parts), 2):
        hdr = parts[i]
        m = re.match(r'--- !u!(\d+) &(-?\d+)', hdr)
        out.append((m.group(1), m.group(2), hdr, parts[i + 1]))
    return head, out


def game_object(gid, tid, mid, name):
    return ('--- !u!1 &%s\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
            '  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: %s}\n  - component: {fileID: %s}\n'
            '  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n') % (gid, tid, mid, name)


def transform(tid, gid, father):
    return ('--- !u!4 &%s\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
            '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %s}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n'
            '  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n'
            '  m_Father: {fileID: %s}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n') % (tid, gid, father)


def behaviour(mid, gid, script_guid, fields):
    return ('--- !u!114 &%s\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
            '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %s}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n%s') % (mid, gid, script_guid, fields)


def main():
    text = open(SRC, encoding='utf-8').read()
    head, bl = blocks(text)
    keep = []
    for cls, fid, hdr, body in bl:
        if fid in (GAME_MANAGER_PI, GAME_MANAGER_STRIPPED):
            continue
        if fid == MANAGER_TRANSFORM:
            body = body.replace('  - {fileID: %s}\n' % GAME_MANAGER_STRIPPED, '  - {fileID: %s}\n' % GM_TR)
        if fid == CONNECTION_PI:
            body = re.sub(r'(propertyPath: fixedProperties\n      value: )1', r'\g<1>0', body)
        if cls == '1660057539':  # the scene's roots: add the DRONE VR object
            body = body.rstrip('\n') + '\n  - {fileID: %s}\n' % APP_TR
        keep.append(hdr + body)
    sim = guid_of('DRONEVR/Scripts/Runtime/DroneSimulationManager.cs')
    app = guid_of('DRONEVR/Scripts/Runtime/DroneApp.cs')
    gm_fields = ('  primaryRightHandButton: {fileID: -6885188074247442836, guid: c348712bda248c246b8c49b3db54643f, type: 3}\n'
                 '  player: {fileID: %s}\n  Ground: {fileID: 0}\n') % FPS_PLAYER_GO
    extra = [game_object(GM_GO, GM_TR, GM_MB, 'Game Manager'), transform(GM_TR, GM_GO, MANAGER_TRANSFORM), behaviour(GM_MB, GM_GO, sim, gm_fields),
             game_object(APP_GO, APP_TR, APP_MB, 'DRONE VR'), transform(APP_TR, APP_GO, '0'), behaviour(APP_MB, APP_GO, app, '')]
    # the roots block must stay last
    roots = [k for k in keep if k.startswith('--- !u!1660057539')]
    rest = [k for k in keep if not k.startswith('--- !u!1660057539')]
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write(head + ''.join(rest + extra + roots))
    print('scene written:', os.path.relpath(OUT, ASSETS))
    write_build_settings()


def write_build_settings():
    path = os.path.join(os.path.dirname(ASSETS), 'ProjectSettings', 'EditorBuildSettings.asset')
    text = open(path, encoding='utf-8').read()
    scenes = [
        ('Assets/Scenes/GAMA/Menu/Startup Menu.unity', 'fba6476e9c9b14afe9a8c87e93375ad1'),
        ('Assets/Scenes/GAMA/Menu/IP Menu.unity', 'a6898870ea3a74b8eaf4ae0815720f93'),
        ('Assets/DRONEVR/Scenes/Main Scene.unity', guid_of('DRONEVR/Scenes/Main Scene.unity')),
        ('Assets/Scenes/GAMA/Menu/End of Game Menu.unity', '931173f81a7164b55a0e2f094ac25ae8'),
    ]
    lst = ''.join('  - enabled: 1\n    path: %s\n    guid: %s\n' % s for s in scenes)
    text = re.sub(r'(?s)(  m_Scenes:\n).*?(  m_configObjects:)', lambda m: m.group(1) + lst + m.group(2), text)
    open(path, 'w', encoding='utf-8', newline='\n').write(text)
    print('build settings: ' + ', '.join(os.path.basename(s[0]) for s in scenes))


if __name__ == '__main__':
    main()
