#!/usr/bin/env python3
"""Writes Unity's .meta files for everything under Unity/Assets/DRONEVR that does not have one yet.

    python3 Tools/unity_meta.py            only the missing ones
    python3 Tools/unity_meta.py --force    all of them again (import settings go back to the ones here)

Unity keeps an id (a GUID) and the import settings of each file in a .meta file next to it. Unity writes
them itself when it first sees a file, but then every computer would invent different ids. Writing them here,
from the file's path, gives every copy of the project the same ids, and lets the models and textures arrive
with the settings DRONE VR needs (see the comments at each kind below).
"""
import hashlib, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, 'Unity', 'Assets')
BASE = os.path.join(ASSETS, 'DRONEVR')


def guid_of(rel):
    """The id of the asset at this path (relative to Assets, with forward slashes)."""
    return hashlib.md5(('dronevr/' + rel.replace(os.sep, '/')).encode('utf-8')).hexdigest()


TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

FOLDER = "fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n" + TAIL
DEFAULT = "fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n" + TAIL
SCRIPT = ("fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n"
          "  executionOrder: 0\n  icon: {{instanceID: 0}}\n" + TAIL)
TEXT = "fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n" + TAIL
ASMDEF = "fileFormatVersion: 2\nguid: {guid}\nAssemblyDefinitionImporter:\n  externalObjects: {{}}\n" + TAIL
NATIVE = "fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {main}\n" + TAIL
PLAIN = "fileFormatVersion: 2\nguid: {guid}\n"

# Textures. Three kinds:
#   picture  signs: kept at their own size, edges do not wrap, alpha kept clean
#   tile     pictures that repeat (none yet)
#   palette  the sheet of flat colors: exact colors, no smoothing between squares
TEXTURE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: {mip}
    sRGBTexture: 1
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
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: {filter}
    aniso: {aniso}
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nPOTScale: 0
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
  alphaIsTransparency: {alpha}
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
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: {compression}
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
""" + TAIL
TEX_KIND = {
    'picture': dict(mip=1, filter=1, aniso=4, wrap=1, alpha=1, compression=1),
    'tile': dict(mip=1, filter=1, aniso=4, wrap=0, alpha=0, compression=1),
    'palette': dict(mip=0, filter=0, aniso=1, wrap=1, alpha=0, compression=0),
}

# Models. No cameras, lights, animation or generated colliders; the flat-colored parts use the project's
# palette material (the code sets every material again when the game starts, so this is for the editor).
MODEL = """fileFormatVersion: 2
guid: {guid}
ModelImporter:
  serializedVersion: 21300
  internalIDToNameTable: []
  externalObjects:
  - first:
      type: UnityEngine:Material
      assembly: UnityEngine.CoreModule
      name: DronePalette
    second: {{fileID: 2100000, guid: {palette}, type: 2}}
  materials:
    materialImportMode: 2
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 1
    motionNodeName:
    rigImportErrors:
    rigImportWarnings:
    animationImportErrors:
    animationImportWarnings:
    animationRetargetingWarnings:
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useSRGBMaterialColor: 1
    sortHierarchyByName: 1
    importVisibility: 1
    importBlendShapes: 0
    importCameras: 0
    importLights: 0
    nodeNameCollisionStrategy: 1
    fileIdsGeneration: 2
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 0
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.001
    optimizeBones: 1
    meshOptimizationFlags: -1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName:
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {{instanceID: 0}}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 0
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidExtraRootOnlyWhenUsingAvatar: 1
  remapMaterialsIfMaterialImportModeIsNone: 0
  additionalBone: 0
""" + TAIL

PALETTE_MAT = 'DRONEVR/Resources/DRONEVR/Materials/DronePalette.mat'
# scripts that must wake up after others: the Game Manager after the template's Connection Manager (which it
# asks for the player's id in Awake), and DRONE VR's app after both
EXEC_ORDER = {'DroneSimulationManager.cs': 100, 'DroneApp.cs': 200}


def meta_for(path, rel):
    g = guid_of(rel)
    if os.path.isdir(path):
        return FOLDER.format(guid=g)
    ext = os.path.splitext(path)[1].lower()
    name = os.path.splitext(os.path.basename(path))[0]
    if ext == '.cs':
        return SCRIPT.format(guid=g).replace('executionOrder: 0', 'executionOrder: %d' % EXEC_ORDER.get(os.path.basename(path), 0))
    if ext in ('.json', '.txt', '.md', '.csv', '.gaml'):
        return TEXT.format(guid=g)
    if ext == '.asmdef':
        return ASMDEF.format(guid=g)
    if ext == '.mat':
        return NATIVE.format(guid=g, main=2100000)
    if ext == '.unity':
        return DEFAULT.format(guid=g)
    if ext == '.png':
        kind = 'palette' if name == 'palette' else 'picture'
        return TEXTURE.format(guid=g, **TEX_KIND[kind])
    if ext == '.fbx':
        return MODEL.format(guid=g, palette=guid_of(PALETTE_MAT))
    return PLAIN.format(guid=g)


def main():
    force = '--force' in sys.argv
    made = kept = 0
    seen = {}
    for dirpath, dirnames, filenames in os.walk(BASE):
        dirnames.sort()
        for n in sorted(dirnames) + sorted(filenames):
            if n.endswith('.meta') or n.startswith('.'):
                continue
            path = os.path.join(dirpath, n)
            rel = os.path.relpath(path, ASSETS)
            g = guid_of(rel)
            if g in seen:
                raise SystemExit('two files got the same id: %s and %s' % (seen[g], rel))
            seen[g] = rel
            meta = path + '.meta'
            if os.path.exists(meta) and not force:
                kept += 1
                continue
            with open(meta, 'w', encoding='utf-8', newline='\n') as f:
                f.write(meta_for(path, rel))
            made += 1
    # the DRONEVR folder itself
    meta = BASE + '.meta'
    if not os.path.exists(meta) or force:
        open(meta, 'w', encoding='utf-8', newline='\n').write(FOLDER.format(guid=guid_of('DRONEVR')))
        made += 1
    # a .meta whose file is gone would only confuse Unity
    gone = 0
    for dirpath, dirnames, filenames in os.walk(BASE):
        for n in filenames:
            if n.endswith('.meta') and not os.path.exists(os.path.join(dirpath, n[:-5])):
                os.remove(os.path.join(dirpath, n)); gone += 1
    print('meta files: %d written, %d kept, %d removed' % (made, kept, gone))


if __name__ == '__main__':
    main()
