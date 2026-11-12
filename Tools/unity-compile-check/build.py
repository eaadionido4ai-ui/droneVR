#!/usr/bin/env python3
"""Compiles DRONE VR's Unity scripts without the Unity editor, to catch mistakes early.

    python3 Tools/unity-compile-check/build.py <Unity editor Data folder> <folder of unpacked packages> <build folder>

It compiles, with the C# compiler that ships with Unity, the packages the scripts use (from their sources),
then the project's own scripts (Assembly-CSharp: the SIMPLE template's scripts and DRONE VR's), against
the Unity 6000.3.8f1 engine's reference assemblies. It does not run anything: Unity is still needed to play.

The editor's Data folder comes from a Unity 6000.3.8f1 install (Editor/Data), and the packages from the .tgz
files in Editor/Data/Resources/PackageManager/Editor. Without a Unity install, both can be taken from the
unityci/editor:ubuntu-6000.3.8f1-base-3.2.2 Docker image (its /opt/unity/Editor/Data).
"""
import json, os, re, subprocess, sys, glob

DATA, PKGS, OUT = [os.path.abspath(a) for a in sys.argv[1:4]]
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, 'Unity', 'Assets')
os.makedirs(OUT, exist_ok=True)
CSC = ['dotnet', os.path.join(DATA, 'DotNetSdkRoslyn', 'csc.dll')]
ENGINE = sorted(glob.glob(os.path.join(DATA, 'Managed', 'UnityEngine', 'UnityEngine*.dll')))
EDITOR = [os.path.join(DATA, 'Managed', 'UnityEditor.dll')] + sorted(glob.glob(os.path.join(DATA, 'Managed', 'UnityEngine', 'UnityEditor*.dll')))
NETSTD = [os.path.join(DATA, 'NetStandard', 'ref', '2.1.0', 'netstandard.dll')] + sorted(glob.glob(os.path.join(DATA, 'NetStandard', 'compat', '2.1.0', 'shims', 'netstandard', '*.dll'))) + [os.path.join(DATA, 'NetStandard', 'compat', '2.1.0', 'shims', 'netfx', 'mscorlib.dll')]

# the packages in the project (Unity/Packages/packages-lock.json), with their versions, for the asmdefs' versionDefines
LOCK = json.load(open(os.path.join(ROOT, 'Unity', 'Packages', 'packages-lock.json')))['dependencies']
VERSIONS = {k: v['version'] for k, v in LOCK.items()}
VERSIONS.setdefault('com.unity.ugui', '2.0.0')
# packages this check does not build (their code paths stay switched off)
SKIP_PACKAGES = {'com.unity.xr.hands', 'com.unity.xr.openxr'}

GLOBAL = ['UNITY_6000_3_8', 'UNITY_6000_3', 'UNITY_6000', 'UNITY_64', 'NET_STANDARD_2_0', 'NET_STANDARD', 'NET_STANDARD_2_1', 'NETSTANDARD', 'NETSTANDARD2_1',
          'ENABLE_INPUT_SYSTEM', 'ENABLE_VR', 'ENABLE_XR_MODULE', 'ENABLE_MONO', 'ENABLE_PHYSICS', 'ENABLE_UNITYEVENTS', 'ENABLE_AUDIO', 'ENABLE_CLOTH', 'ENABLE_WEBCAM',
          'CSHARP_7_3_OR_NEWER', 'CSHARP_7_OR_LATER', 'ENABLE_RUNTIME_GI', 'UNITY_ANDROID', 'UNITY_POST_PROCESSING_STACK_V2', 'USE_INPUT_SYSTEM_POSE_CONTROL',
          'USE_STICK_CONTROL_THUMBSTICKS', 'DOTWEEN', 'UNITY_INCLUDE_TESTS', 'ENABLE_UNITY_COLLECTIONS_CHECKS']
for y in range(2017, 2024):
    for m in (1, 2, 3, 4):
        GLOBAL.append('UNITY_%d_%d_OR_NEWER' % (y, m))
for m in range(0, 4):
    GLOBAL.append('UNITY_6000_%d_OR_NEWER' % m)
for v in ('5_3', '5_4', '5_5', '5_6'):
    GLOBAL.append('UNITY_%s_OR_NEWER' % v)


def vtuple(v):
    return tuple(int(x) for x in re.findall(r'\d+', v)[:3]) + (0,) * (3 - len(re.findall(r'\d+', v)[:3]))


def satisfied(name, expr):
    if name.startswith('com.unity.modules.'):
        return True
    if name in SKIP_PACKAGES or name not in VERSIONS:
        return False
    if not expr:
        return True
    have = vtuple(VERSIONS[name])
    m = re.match(r'^\s*([\[\(])?\s*([^,\]\)]*)\s*(?:,\s*([^\]\)]*))?\s*([\]\)])?\s*$', expr)
    if not m:
        return True
    lo, hi = m.group(2), m.group(3)
    if lo and have < vtuple(lo):
        return False
    if m.group(1) == '(' and lo and have == vtuple(lo):
        return False
    if hi:
        if m.group(4) == ')' and have >= vtuple(hi):
            return False
        if m.group(4) == ']' and have > vtuple(hi):
            return False
    return True


def sources(folder, exclude_dirs):
    out = []
    for dp, dn, fn in os.walk(folder):
        if any(dp == e or dp.startswith(e + os.sep) for e in exclude_dirs):
            continue
        parts = dp.split(os.sep)
        if any(p.endswith('~') or p in ('Tests',) for p in parts[len(folder.split(os.sep)):]):
            continue
        out += [os.path.join(dp, f) for f in fn if f.endswith('.cs')]
    return sorted(out)


def asmdef_dirs(root):
    found = []
    for dp, dn, fn in os.walk(root):
        for f in fn:
            if f.endswith('.asmdef'):
                found.append((dp, json.load(open(os.path.join(dp, f), encoding='utf-8-sig'))))
    return found


BUILT = {}


PRECOMPILED = []  # dlls that ship inside the packages (added by build_package)


def compile_asm(name, files, refs, defines, unsafe=False, editor=False, extra_refs=()):
    out = os.path.join(OUT, name + '.dll')
    # a package that is already built, from sources that have not changed, is not built again
    if PKGS in (files[0] if files else '') or DATA in (files[0] if files else ''):
        if os.path.exists(out) and os.path.getmtime(out) > max(os.path.getmtime(f) for f in files):
            BUILT[name] = out
            return True
    rsp = os.path.join(OUT, name + '.rsp')
    args = ['-target:library', '-out:' + out, '-nologo', '-nostdlib+', '-noconfig', '-langversion:9.0', '-nowarn:0169,0414,0649,0618,0067,0612,0672,1701,1702,0108,0114,0162,0168,0219,0251,0252,0253,0659,0661,0693,1591,8632,8669,8600,0109',
            '-define:' + ';'.join(sorted(set(defines + (['UNITY_EDITOR', 'UNITY_EDITOR_64', 'UNITY_EDITOR_LINUX'] if editor else [])))), '-deterministic']
    if unsafe:
        args.append('-unsafe')
    for r in NETSTD + ENGINE + (EDITOR if editor else []) + list(extra_refs) + PRECOMPILED:
        args.append('-r:' + r)
    # every assembly built so far is offered as a reference (asmdefs name some of theirs by GUID)
    for r in BUILT:
        if r != name:
            args.append('-r:' + BUILT[r])
    args += files
    with open(rsp, 'w') as f:
        f.write('\n'.join('"%s"' % a if ' ' in a and not a.startswith('-') else a for a in args))
    p = subprocess.run(CSC + ['@' + rsp], capture_output=True, text=True)
    errors = [l for l in p.stdout.splitlines() if ': error ' in l]
    if p.returncode != 0:
        print('FAILED %s (%d files): %d errors' % (name, len(files), len(errors)))
        for l in errors[:60]:
            print('   ', l.replace(ROOT + '/', '').replace(PKGS + '/', ''))
        return False
    BUILT[name] = out
    print('built  %s (%d files)' % (name, len(files)))
    return True


def build_package(pkg_root, wanted, editor=False):
    """Builds the assemblies named in wanted, found under pkg_root, in that order."""
    for dll in glob.glob(os.path.join(pkg_root, '**', '*.dll'), recursive=True):
        rel = os.path.relpath(dll, pkg_root)
        if 'Editor' not in rel and 'Tests' not in rel and '~' not in rel and '.Runtime' not in rel and 'CodeGen' not in rel and dll not in PRECOMPILED and os.path.basename(dll) not in [os.path.basename(x) for x in PRECOMPILED]:
            PRECOMPILED.append(dll)
    dirs = asmdef_dirs(pkg_root)
    all_dirs = [d for d, _ in dirs]
    by_name = {a['name']: (d, a) for d, a in dirs}
    for name in wanted:
        if name not in by_name:
            print('missing asmdef', name); continue
        d, a = by_name[name]
        excl = [x for x in all_dirs if x != d and x.startswith(d + os.sep)]
        files = sources(d, excl)
        defines = GLOBAL + [v['define'] for v in a.get('versionDefines', []) if satisfied(v['name'], v.get('expression', ''))]
        compile_asm(name, files, a.get('references', []), defines, a.get('allowUnsafeCode', False), editor)


P = lambda n: os.path.join(PKGS, n, 'package')
UGUI = os.path.join(DATA, 'Resources', 'PackageManager', 'BuiltInPackages', 'com.unity.ugui')
build_package(P('com.unity.mathematics-1.3.3'), ['Unity.Mathematics'])
build_package(P('com.unity.burst-1.8.27'), ['Unity.Burst'])
build_package(P('com.unity.collections-2.6.2'), ['Unity.Collections'])
build_package(UGUI, ['UnityEngine.UI', 'Unity.InternalAPIEngineBridge.004', 'Unity.TextMeshPro'])
build_package(P('com.unity.inputsystem-1.18.0'), ['Unity.InputSystem', 'Unity.InputSystem.ForUI'])
build_package(P('com.unity.xr.legacyinputhelpers-2.1.13'), ['UnityEngine.SpatialTracking', 'UnityEngine.XR.LegacyInputHelpers'])
build_package(P('com.unity.xr.core-utils-2.5.3'), ['Unity.XR.CoreUtils'])
build_package(P('com.unity.xr.management-4.5.3'), ['Unity.XR.Management'])
build_package(P('com.unity.xr.interaction.toolkit-3.3.1'), ['Unity.XR.Interaction.Toolkit'])

# the project's own assemblies that have an asmdef (not the tests, not the XR Hands samples: this check does not build XR Hands)
for d, a in asmdef_dirs(ASSETS):
    pass
order = ['endel.nativewebsocket', 'Unity.XR.Interaction.Toolkit.Samples.StarterAssets', 'Unity.XR.Interaction.Toolkit.Samples.DeviceSimulator', 'DroneVR.Core']
dirs = {a['name']: (d, a) for d, a in asmdef_dirs(ASSETS)}
for name in order:
    d, a = dirs[name]
    excl = [x for x, _ in asmdef_dirs(ASSETS) if x != d and x.startswith(d + os.sep)]
    files = [f for f in sources(d, excl) if os.sep + 'Editor' + os.sep not in f]
    defines = GLOBAL + [v['define'] for v in a.get('versionDefines', []) if satisfied(v['name'], v.get('expression', ''))]
    compile_asm(name, files, [], defines, a.get('allowUnsafeCode', False))

# Assembly-CSharp: everything under Assets that is not in an asmdef folder and not in an Editor folder.
# Left out: SceneTransition.cs needs the Universal Render Pipeline package, which this check does not build.
LEFT_OUT = [os.path.join(ASSETS, 'Scripts', 'Utilities', 'SceneTransition.cs')]
asm = [d for d, _ in asmdef_dirs(ASSETS)]
files = [f for f in sources(ASSETS, asm + [os.path.join(ASSETS, 'Samples')]) if os.sep + 'Editor' + os.sep not in f and f not in LEFT_OUT]
plugins = [os.path.join(ASSETS, 'Plugins', 'websocket-sharp.dll'), os.path.join(PKGS, 'com.unity.nuget.newtonsoft-json-3.2.2', 'package', 'Runtime', 'Newtonsoft.Json.dll')]
plugins += glob.glob(os.path.join(ASSETS, 'Plugins', 'Demigiant', 'DOTween', '*.dll'))
compile_asm('Assembly-CSharp', files, [], GLOBAL, True, False, plugins)
editor_files = [f for f in sources(ASSETS, asm + [os.path.join(ASSETS, 'Samples')]) if os.sep + 'Editor' + os.sep in f]
if editor_files and 'Assembly-CSharp' in BUILT:
    compile_asm('Assembly-CSharp-Editor', editor_files, [], GLOBAL, True, True, plugins)
print('ok' if 'Assembly-CSharp' in BUILT and ('Assembly-CSharp-Editor' in BUILT or not editor_files) else 'NOT OK')
