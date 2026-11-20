"""Blender-side FBX re-import audit and studio previews of the exported assets."""
from pathlib import Path
import json
import math
import sys
import bpy
from mathutils import Vector

PACK=Path(sys.argv[sys.argv.index('--')+1]).resolve()
PREVIEW=PACK/'preview'
PREVIEW.mkdir(exist_ok=True)

def bounds(objects):
    coordinates=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
    lo=Vector(tuple(min(v[i] for v in coordinates) for i in range(3)))
    hi=Vector(tuple(max(v[i] for v in coordinates) for i in range(3)))
    return lo,hi

def load(path):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True)
    bpy.context.view_layer.update()
    return [o for o in bpy.data.objects if o not in before]

results=[]
for path in sorted((PACK/'models').glob('*/*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects=load(path)
    lo,hi=bounds(objects)
    dimensions={'width':hi.x-lo.x,'height':hi.z-lo.z,'length':hi.y-lo.y}
    provenance=json.loads((path.parent/'model.gen.json').read_text())
    expected=provenance['facts']['size']
    expected_tris=provenance['facts']['tris']
    meshes=[o for o in objects if o.type=='MESH']
    tris=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    names={o.name for o in objects}
    required={o['name'] for o in provenance['facts']['objects']}
    checks={
      'meshes_present':bool(meshes),
      'dimensions_match_export':all(abs(dimensions[k]-expected[k])<max(.002,expected[k]*.01) for k in expected),
      'triangles_match_export':tris==expected_tris,
      'parts_preserved':required.issubset(names),
      'ground_pivot':abs(lo.z)<.005,
      'palette_materials':all(slot.material and slot.material.name.startswith('M_') for o in meshes for slot in o.material_slots),
    }
    row={'asset':path.parent.name,'path':path.relative_to(PACK).as_posix(),
         'ok':all(checks.values()),'checks':checks,'meshes':len(meshes),'tris':tris,
         'size':{k:round(v,5) for k,v in dimensions.items()},
         'root_transforms':[{'name':o.name,'rotation_xyz':[round(v,5) for v in o.rotation_euler],
                             'scale':[round(v,5) for v in o.scale]} for o in objects if not o.parent]}
    results.append(row)
    print('AUDIT',json.dumps(row),flush=True)
(PACK/'fbx-roundtrip-validation.json').write_text(json.dumps(results,indent=2)+'\n')
assert len(results)==16 and all(r['ok'] for r in results),'FBX re-import audit failed; see report'

def setup(camera_position,target,scale,filename):
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1280
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
    scene.view_settings.exposure=-.75
    scene.render.image_settings.file_format='PNG'
    world=bpy.data.worlds.new('StudioWorld')
    world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.65,.62,.72,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
    scene.world=world
    material=bpy.data.materials.new('StudioGround')
    material.diffuse_color=(.82,.79,.87,1)
    material.use_nodes=True
    material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.82,.79,.87,1)
    material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015))
    ground=bpy.context.object; ground.name='StudioGround'; ground.data.materials.append(material)
    for name,loc,power,size in [('Key',(-3,-4,7),650,5),('Fill',(4,-1,5),450,4),('Rim',(1,4,6),650,4)]:
        data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
        light=bpy.data.objects.new(name,data); scene.collection.objects.link(light)
        light.location=loc; light.rotation_euler=(-Vector(loc)).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('PreviewCamera'); data.type='ORTHO'; data.ortho_scale=scale
    camera=bpy.data.objects.new('PreviewCamera',data); scene.collection.objects.link(camera)
    camera.location=camera_position
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera=camera
    scene.render.filepath=str(PREVIEW/filename)
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW/(Path(filename).stem+'.blend')))
    bpy.ops.render.render(write_still=True)

def place(path,location,rotation=0):
    imported=load(path)
    collection=bpy.data.collections.new(path.parent.name)
    bpy.context.scene.collection.children.link(collection)
    for obj in imported:
        for old in list(obj.users_collection): old.objects.unlink(obj)
        collection.objects.link(obj)
    for obj in imported:
        if obj.parent is None:
            obj.location += Vector(location)
            obj.rotation_euler.z += rotation
    return imported

bpy.ops.wm.read_factory_settings(use_empty=True)
names=['purple','blue','green','orange','yellow','red','pink','brown','cyan','hidden']
for index,name in enumerate(names):
    folder=PACK/'models'/('character-'+name)
    path=next(folder.glob('*.fbx'))
    x=(index%4-1.5)*1.65
    y=(1-index//4)*1.8
    place(path,(x,y,0))
setup((2.8,-7.5,8.7),(0,0,.35),8.2,'character-family-studio.png')

bpy.ops.wm.read_factory_settings(use_empty=True)
place(next((PACK/'models/queue-four').glob('*.fbx')),(-2.9,0,0),-.25)
place(next((PACK/'models/waiting-board').glob('*.fbx')),(1.3,.25,0),0)
for index,name in enumerate(['waiting-cell','waiting-edge','waiting-corner-outer','waiting-corner-inner']):
    place(next((PACK/'models'/name).glob('*.fbx')),(-.5+index*1.3,-2.15,0),0)
setup((6,-9,8),(0,0,0),10.5,'container-studio.png')
print('All 16 FBX files passed round-trip audit. Studio previews rendered.',flush=True)
