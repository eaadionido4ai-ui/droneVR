"""Turns the exported glTF models into Blender files and FBX files for Unity.

Run with Blender's Python (Blender 4.2 LTS or newer), for every model or for the ones named:

    blender --background --python Tools/blender/build_models.py -- [name ...]
    python  Tools/blender/build_models.py [name ...]        (with the bpy module installed)

For each Tools/world-export/out/glb/<name>.glb it:
  1. imports the model;
  2. paints every flat-colored piece from the palette (Tools/blender/palette.png) and joins the pieces of a
     group into one object: S__<group>__... into M__<group>, W__<set>__... into SW__<set>;
  3. keeps textured pieces (T__...), invisible boxes (COL__, FLOOR__, HOT__, ZONE__) and anchors (A__, CAL__);
  4. saves Blender/<name>.blend (the props in Blender/props) and
     Unity/Assets/DRONEVR/Resources/DRONEVR/Models/<name>.fbx.

The names are explained in Tools/world-export/src/tag.js. This is the same step as TANGHALAN VR's.
"""
import bpy, json, os, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
GLB_DIR = os.path.join(ROOT, 'Tools', 'world-export', 'out', 'glb')
TEX_DIR = os.path.join(ROOT, 'Unity', 'Assets', 'DRONEVR', 'Resources', 'DRONEVR', 'Textures')
BLEND_DIR = os.path.join(ROOT, 'Blender')
FBX_DIR = os.path.join(ROOT, 'Unity', 'Assets', 'DRONEVR', 'Resources', 'DRONEVR', 'Models')
PLACES = ('town', 'eoc')
PALETTE_PNG = os.path.join(TEX_DIR, 'palette.png')
PALETTE = json.load(open(os.path.join(HERE, 'palette.json')))
MARKERS = ('COL', 'FLOOR', 'HOT', 'ZONE')


def palette_uv(hex6):
    k = PALETTE['colors'].index(hex6.lower())
    g = PALETTE['grid']
    return ((k % g + 0.5) / g, 1.0 - (k // g + 0.5) / g)


def palette_material():
    mat = bpy.data.materials.new('DronePalette')
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = 1.0
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(PALETTE_PNG)
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    mat.use_backface_culling = False
    return mat


def texture_material(name):
    """A material that shows the texture <name>.png from the Unity project, for looking at the model in Blender."""
    key = 'T_' + name
    mat = bpy.data.materials.get(key)
    if mat:
        return mat
    mat = bpy.data.materials.new(key)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = 1.0
    path = os.path.join(TEX_DIR, name + '.png')
    if os.path.exists(path):
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(path, check_existing=True)
        nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
        try:
            mat.blend_method = 'CLIP'
        except Exception:
            pass
    return mat


def collection(name):
    c = bpy.data.collections.get(name)
    if not c:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def move_to(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    return o


def build(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(GLB_DIR, name + '.glb'), merge_vertices=False, loglevel=40)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    pal = palette_material()
    groups = {}
    counts = {'pieces': 0, 'textured': 0, 'markers': 0, 'anchors': 0}
    for o in list(scene.objects):
        parts = o.name.split('__')
        kind = parts[0]
        if o.type == 'MESH' and kind in ('S', 'W') and len(parts) >= 4:
            u, v = palette_uv(parts[3][:6])
            me = o.data
            if not me.uv_layers:
                me.uv_layers.new(name='UVMap')
            uv = me.uv_layers.active.data
            flat = [u, v] * len(uv)
            uv.foreach_set('uv', flat)
            me.materials.clear()
            me.materials.append(pal)
            groups.setdefault(('M__' if kind == 'S' else 'SW__') + parts[1], []).append(o)
            counts['pieces'] += 1
        elif o.type == 'MESH' and kind == 'T':
            # glTF keeps pictures top-down; Blender and Unity read them bottom-up, so turn the UVs over
            me = o.data
            n = len(me.uv_layers.active.data)
            buf = [0.0] * (n * 2)
            me.uv_layers.active.data.foreach_get('uv', buf)
            buf[1::2] = [1.0 - v for v in buf[1::2]]
            me.uv_layers.active.data.foreach_set('uv', buf)
            o.data.materials.clear()
            o.data.materials.append(texture_material(parts[1]))
            move_to(o, collection('Textured'))
            counts['textured'] += 1
        elif o.type == 'MESH' and kind in MARKERS:
            o.data.materials.clear()
            o.display_type = 'WIRE'
            o.hide_render = True
            move_to(o, collection('Boxes'))
            counts['markers'] += 1
        elif o.type == 'EMPTY':
            o.empty_display_type = 'PLAIN_AXES'
            o.empty_display_size = 0.15
            move_to(o, collection('Anchors'))
            counts['anchors'] += 1
    world = collection('World')
    for gname in sorted(groups):
        move_to(join(groups[gname], gname), world)
    # every mesh keeps only where it is: no rotation, no scale
    bpy.ops.object.select_all(action='DESELECT')
    for o in scene.objects:
        if o.type == 'MESH':
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.select_all(action='DESELECT')
    # remove what the import left behind and is not used
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for b in list(block):
            if b.users == 0:
                block.remove(b)
    for c in list(scene.collection.children):
        if not c.all_objects:
            bpy.data.collections.remove(c)

    os.makedirs(BLEND_DIR, exist_ok=True)
    os.makedirs(FBX_DIR, exist_ok=True)
    sub = 'props' if name not in PLACES else ''
    blend = os.path.join(BLEND_DIR, sub, name + '.blend')
    os.makedirs(os.path.dirname(blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend, compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_mainfile(compress=True)
    export_fbx(os.path.join(FBX_DIR, name + '.fbx'))
    tris = sum(len(o.data.loop_triangles) if o.data.loop_triangles else sum(len(p.vertices) - 2 for p in o.data.polygons) for o in scene.objects if o.type == 'MESH' and o.name.split('__')[0] in ('M', 'SW', 'T'))
    print('%-22s %4d pieces -> %2d objects, %3d textured, %3d boxes, %3d anchors, %6d triangles' % (name, counts['pieces'], len(groups), counts['textured'], counts['markers'], counts['anchors'], tris))


def export_fbx(path):
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', use_mesh_modifiers=True,
        use_custom_props=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [a for a in sys.argv[1:] if not a.startswith('-') and not a.endswith('.py')]
    names = argv or sorted(os.path.splitext(f)[0] for f in os.listdir(GLB_DIR) if f.endswith('.glb'))
    for n in names:
        build(n)


if __name__ == '__main__':
    main()
