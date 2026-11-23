"""Add motion parenting to the editable .blend handoff without changing exported FBX files."""
from pathlib import Path
import sys
import bpy

pack = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
for name in ('locked-house-purple', 'locked-house-pink'):
    path = pack / 'models' / name / (name + '.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    for child_name, parent_name in (('Shackle', 'Padlock'), ('Padlock', 'Door')):
        child = bpy.data.objects[child_name]
        parent = bpy.data.objects[parent_name]
        world = child.matrix_world.copy()
        child.parent = parent
        child.matrix_world = world
        bpy.context.view_layer.update()
    bpy.context.scene['FBX_motion_parenting'] = 'Shackle -> Padlock -> Door, world transforms preserved'
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    print('ASSEMBLED ' + name, flush=True)
