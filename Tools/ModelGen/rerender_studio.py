"""Correct studio exposure without changing validated model geometry."""
from pathlib import Path
import sys
import bpy

preview=Path(sys.argv[sys.argv.index('--')+1]).resolve()/'preview'
for name in ('character-family-studio','container-studio'):
    path=preview/(name+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    scene=bpy.context.scene
    scene.view_settings.view_transform='AgX'
    scene.view_settings.exposure=-.75
    scene.render.filepath=str(preview/(name+'.png'))
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    bpy.ops.render.render(write_still=True)
