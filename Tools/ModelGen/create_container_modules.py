"""Build reusable pieces from the approved waiting-tray profile via model-gen."""
from pathlib import Path
import importlib.util
import json
import os

ROOT=Path(__file__).resolve().parents[2]
PACK=ROOT/'docs/art/mockup-assets-20261119'
spec=importlib.util.spec_from_file_location('model_gen',ROOT/'Tools/pf/model-gen.py')
mg=importlib.util.module_from_spec(spec)
spec.loader.exec_module(mg)
os.environ.setdefault('BLENDER_BIN',str(ROOT/'Tools/ModelGen/.runtime/blender-5.0.1-windows-x64/blender.exe'))

SCRIPTS={
 'waiting-cell':((1.15,.04,1.15),"mg.box('Floor',(1.15,1.15,.04),loc=(0,0,.02),bevel=.018,segments=3,color='tray_floor')"),
 'waiting-edge':((1.15,.26,.12),"obj=mg.box('Rim',(1.15,.12,.26),loc=(0,0,.13),bevel=.045,segments=4,color='waiting_rim')\nmg.smooth(obj,angle=40)"),
}
for name,radius in (('waiting-corner-outer',.23),('waiting-corner-inner',.35)):
    source=f'''import math
radius={radius}
inner=radius-.12
points=[(radius*math.cos(math.pi*i/24)-radius/2,radius*math.sin(math.pi*i/24)-radius/2) for i in range(13)]
points += [(inner*math.cos(math.pi*i/24)-radius/2,inner*math.sin(math.pi*i/24)-radius/2) for i in range(12,-1,-1)]
obj=mg.profile('Rim',points,.26,axis='Z',loc=(0,0,.13),bevel=.012,segments=2,color='waiting_rim')
mg.smooth(obj,angle=40)
'''
    SCRIPTS[name]=((radius,.26,radius),source)

def main():
    results=[]
    base=json.loads((ROOT/'Tools/ModelGen/requests/mockup-v1-waiting-board.json').read_text())
    for name,(dimensions,source) in SCRIPTS.items():
        out=PACK/'models'/name
        out.mkdir(parents=True,exist_ok=True)
        source += "\nfor material in mg.materials.values():\n    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.35\n"
        script=out/'build.py'; script.write_text(source)
        refused=mg.script_guard(source)
        if refused: raise RuntimeError(refused)
        w,h,d=dimensions
        req={**base,'id':name,'out':out.relative_to(ROOT).as_posix(),
             'size':{'width':w,'height':h,'length':d},'max_tris':2500,
             'parts':[{'name':'Floor' if name=='waiting-cell' else 'Rim'}],
             'subject':'Reusable '+name+' derived from the approved tray profile.',
             'notes':'Source art module, not a complete Unity prefab. Omit edges where a queue port opens.'}
        (ROOT/'Tools/ModelGen/requests'/f'{name}.json').write_text(json.dumps(req,indent=2)+'\n')
        facts=mg.run_blender(script,req,out)
        row={'id':name,'ok':facts.get('ok'),'tris':facts.get('tris'),'size':facts.get('size'),
             'failed':[c for c in facts.get('checks',[]) if not c['ok']],
             'error':facts.get('script_error') or facts.get('harness_error')}
        results.append(row); print(json.dumps(row),flush=True)
        if facts.get('ok'):
            mg.views_sheet(out)
            fbx=out/(name+'.fbx'); (out/'model.fbx').replace(fbx)
            (out/'model.gen.json').write_text(json.dumps({'schema':'model-gen-derivative/v1',
                'status':'pending-user-review','source_design':'models/waiting-board/blueprint.png',
                'author':'Codex, derived from approved tray geometry',
                'facts':mg.compact_facts(facts),
                'script':{'path':script.relative_to(ROOT).as_posix(),'sha256':mg.sha256(script)},
                'fbx':{'path':fbx.relative_to(ROOT).as_posix(),'sha256':mg.sha256(fbx)}},indent=2)+'\n')
    (PACK/'models/module-validation.json').write_text(json.dumps(results,indent=2)+'\n')

if __name__=='__main__': main()
