"""Renders pictures of the barangay, the operations center and the props from the Blender files, to check the
models by eye.

    python Tools/blender/render_previews.py <output folder>     (a Python with the bpy module, or Blender's own)

The barangay is drawn once for each mission's scene (its sets switched as Unity switches them, with the water
at that mission's level), from above and from where a drone would fly; then the operations center from where
the operator stands; then the props in a row.
"""
import bpy, json, os, sys, math, re
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = sys.argv[-1]
os.makedirs(OUT, exist_ok=True)

# the missions' scenes and water levels, read from the plan the models are built from
PLAN = open(os.path.join(ROOT, 'Tools', 'world-export', 'src', 'plan.js'), encoding='utf-8').read()
MISSIONS = {m.group(1): (re.findall(r"'(\w+)'", m.group(2)), [float(v) for v in re.findall(r'\[[\d.]+, ([\d.]+)\]', m.group(3))])
            for m in re.finditer(r"\{ id: '(\w+)', sets: \[([^\]]*)\], wind: [^}]*?flood: \[(.*?)\], minutes", PLAN)}


def start(blend):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, 'Blender', blend))
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 12
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 2
    scene.render.resolution_x, scene.render.resolution_y = 1280, 800
    world = bpy.data.worlds.new('W'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.85, 0.95, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = 1.0
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); sun.data.energy = 2.2; sun.rotation_euler = (0.6, 0.15, 0.7); scene.collection.objects.link(sun)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.clip_start = 0.05; cam.data.clip_end = 4000
    for o in scene.objects:
        if o.name.split('__')[0] in ('COL', 'FLOOR', 'HOT', 'ZONE'):
            o.hide_render = True
    return scene, cam


def shot(scene, cam, name, frm, to, lens=24):
    """frm and to are in DRONE VR's coordinates (x, y up, z south); Blender has (x, -z, y)."""
    cam.location = (frm[0], -frm[2], frm[1])
    d = Vector((to[0] - frm[0], -(to[2] - frm[2]), to[1] - frm[1]))
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('rendered', name)


def emit(name, color, alpha):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); mix = nt.nodes.new('ShaderNodeMixShader')
    tr = nt.nodes.new('ShaderNodeBsdfTransparent'); em = nt.nodes.new('ShaderNodeEmission')
    em.inputs[0].default_value = (*color, 1); mix.inputs[0].default_value = alpha
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
    return m


# ---------- the barangay, in each mission's scene ----------
scene, cam = start('town.blend')
bpy.ops.mesh.primitive_plane_add(size=2400)
water = bpy.context.active_object; water.name = 'Water'
water.data.materials.append(emit('water', (0.12, 0.32, 0.5), 0.8))


def mission(mid):
    sets, levels = MISSIONS[mid]
    for o in scene.objects:
        if o.name.startswith('SW__'):
            o.hide_render = o.name[4:] not in sets
    water.location = (0, 0, levels[0])


for mid in ('lipad', 'bantay', 'sagip', 'hatid'):
    mission(mid)
    shot(scene, cam, 'town_' + mid, (-40, 330, 300), (-10, 0, 0), 26)
mission('lipad')
shot(scene, cam, 'pad', (-122, 18.5, -110), (-154, 7.5, -134))
mission('sagip')
shot(scene, cam, 'flood_fpv', (-60, 32, -40), (40, 0, 60), 18)
mission('bantay')
shot(scene, cam, 'quake_fire', (-40, 22, -10), (-66, 3, -33), 22)
mission('hatid')
shot(scene, cam, 'wind_bridge', (70, 30, 90), (20, 2, 125), 22)

# ---------- the operations center ----------
scene, cam = start('eoc.blend')
scene.world.node_tree.nodes['Background'].inputs[1].default_value = 0.25
E = (-172, -6, -134)
lamp = bpy.data.objects.new('Lamp', bpy.data.lights.new('Lamp', 'AREA')); lamp.data.energy = 2500; lamp.data.size = 8
lamp.location = (E[0], -E[2], E[1] + 3.2); scene.collection.objects.link(lamp)
shot(scene, cam, 'eoc_inside', (E[0] + 0.5, E[1] + 1.6, E[2] + 4.6), (E[0] - 0.3, E[1] + 1.0, E[2] - 2), 14)
shot(scene, cam, 'eoc_dock', (E[0] + 3.5, E[1] + 1.6, E[2] + 1.5), (E[0] + 8, E[1] + 0.9, E[2] - 1.5), 16)

# ---------- the props ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
x = 0.0
for name in sorted(os.listdir(os.path.join(ROOT, 'Blender', 'props'))):
    if not name.endswith('.blend'):
        continue
    with bpy.data.libraries.load(os.path.join(ROOT, 'Blender', 'props', name), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n.startswith('M__')]
    k = 0.12 if name.startswith('gate') else 0.5 if name.startswith('boat') else 1.0
    width = 0.0
    for o in dst.objects:
        if o is None:
            continue
        scene.collection.objects.link(o)
        o.scale = (k, k, k)
        o.location = (x + (0.7 if name.startswith('gate') else 0.0), 0, 0.7 if name.startswith('gate') else 0)
        width = max(width, o.dimensions.x * k)
    x += max(0.5, width) + 0.35
scene.render.engine = 'CYCLES'; scene.cycles.samples = 12; scene.cycles.use_denoising = False
scene.render.resolution_x, scene.render.resolution_y = 1600, 500
world = bpy.data.worlds.new('W'); scene.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.9, 0.93, 0.95, 1)
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); sun.data.energy = 2.5; sun.rotation_euler = (0.9, 0.1, -0.5); scene.collection.objects.link(sun)
cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); scene.collection.objects.link(cam); scene.camera = cam
bpy.ops.mesh.primitive_plane_add(size=60); bpy.context.active_object.location = (x / 2, 0, 0)
shot(scene, cam, 'props', (x / 2, 1.6, -7.5), (x / 2, 0.5, 0), 38)
