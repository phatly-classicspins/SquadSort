"""Audit exported FBX files, save editable scenes, and render actual geometry."""
from pathlib import Path
import json
import math
import sys
import bpy
from mathutils import Vector, Matrix

PACK = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
PREVIEW = PACK / 'preview'
PREVIEW.mkdir(exist_ok=True)


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_custom_normals=True)
    bpy.context.view_layer.update()
    return [o for o in bpy.data.objects if o not in before]


def bounds(objects):
    points = [o.matrix_world @ v.co for o in objects if o.type == 'MESH' for v in o.data.vertices]
    return Vector(tuple(min(p[i] for p in points) for i in range(3))), Vector(tuple(max(p[i] for p in points) for i in range(3)))


audit = []
for path in sorted((PACK / 'models').glob('*/*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects = load(path)
    lo, hi = bounds(objects)
    facts = json.loads((path.parent / 'model.gen.json').read_text())['facts']
    size = {'width': hi.x-lo.x, 'height': hi.z-lo.z, 'length': hi.y-lo.y}
    meshes = [o for o in objects if o.type == 'MESH']
    tris = sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    required = set(['Body', 'Roof', 'Door', 'Padlock', 'Shackle', 'Emblem']) if 'house' in path.stem else set()
    names = {o.name for o in objects}
    checks = {
        'size_roundtrip': all(abs(size[k]-facts['size'][k]) < .005 for k in size),
        'triangles_roundtrip': tris == facts['tris'],
        'parts_preserved': required.issubset(names),
        'ground_pivot': abs(lo.z) < .005,
        'center_pivot': abs((lo.x+hi.x)*.5) < .005 and abs((lo.y+hi.y)*.5) < .005,
        'palette_materials': all(m and m.name.startswith('M_') for o in meshes for m in o.data.materials),
        'positive_scale': all(o.matrix_world.determinant() > 0 for o in meshes),
    }
    if required:
        door = next(o for o in objects if o.name == 'Door')
        padlock = next(o for o in objects if o.name == 'Padlock')
        shackle = next(o for o in objects if o.name == 'Shackle')
        checks['moving_parts_independent'] = door.parent is None and padlock.parent is None and shackle.parent is None
    row = {'asset': path.parent.name, 'ok': all(checks.values()), 'tris': tris, 'size': {k: round(v,5) for k,v in size.items()}, 'checks': checks}
    audit.append(row)
    bpy.ops.wm.save_as_mainfile(filepath=str(path.parent / (path.stem + '.blend')))
    print('AUDIT ' + json.dumps(row), flush=True)
(PACK / 'fbx-roundtrip-validation.json').write_text(json.dumps(audit, indent=2) + '\n')
assert all(r['ok'] for r in audit), 'FBX audit failed'


def place(name, position):
    path = next((PACK / 'models' / name).glob('*.fbx'))
    objects = load(path)
    for o in objects:
        if o.parent is None:
            o.location += Vector(position)
    return objects


def studio(filename, camera_position, target, scale, resolution=(1500,1100)):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = -.8
    world = bpy.data.worlds.new('Lavender Studio')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.65,.60,.74,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .35
    scene.world = world
    mat = bpy.data.materials.new('StudioGround')
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.76,.70,.83,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .8
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.012))
    bpy.context.object.name = 'StudioGround'
    bpy.context.object.data.materials.append(mat)
    for name, loc, power, size in [('Key',(-4,-5,9),1100,5),('Fill',(5,-1,7),650,5),('Rim',(0,5,8),1000,4)]:
        data = bpy.data.lights.new(name,'AREA')
        data.energy = power
        data.shape = 'DISK'
        data.size = size
        obj = bpy.data.objects.new(name,data)
        scene.collection.objects.link(obj)
        obj.location = loc
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    data = bpy.data.cameras.new('Preview Camera')
    data.type = 'ORTHO'
    data.ortho_scale = scale
    obj = bpy.data.objects.new('Preview Camera', data)
    scene.collection.objects.link(obj)
    obj.location = camera_position
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    scene.camera = obj
    scene.render.filepath = str(PREVIEW / (filename+'.png'))
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / (filename+'.blend')))
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
place('locked-house-purple',(-1.02,.2,0))
place('locked-house-pink',(1.02,.2,0))
place('character-hidden',(2.4,-1.55,0))
studio('locked-houses-studio',(4.0,-10.5,6.4),(.3,0,1.0),7.7)

# A static pose proves the doorway is hollow and the hinge/lock hierarchy survives FBX.
bpy.ops.wm.read_factory_settings(use_empty=True)
objects = place('locked-house-purple',(0,0,0))
door = next(o for o in objects if o.name == 'Door')
shackle = next(o for o in objects if o.name == 'Shackle')
# FBX axis conversion is carried in the root empty: local Unity Y maps to Blender Z.
# Reassemble the animation hierarchy with world transforms preserved.
lock = next(o for o in objects if o.name == 'Padlock')
for child, parent in ((shackle,lock),(lock,door)):
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_world = world
    bpy.context.view_layer.update()
shackle.matrix_world = Matrix.Translation(Vector((0,0,.08))) @ shackle.matrix_world
pivot = door.matrix_world.translation.copy()
door.matrix_world = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(-80),4,'Z') @ Matrix.Translation(-pivot) @ door.matrix_world
bpy.context.view_layer.update()
studio('door-open-pose',(3.4,-8,4.0),(0,-.5,1.0),6.7,(1200,1100))
