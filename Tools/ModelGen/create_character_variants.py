"""Derive reviewed family designs from the approved body; use the model-gen harness."""
from pathlib import Path
import concurrent.futures
import hashlib
import importlib.util
import json
import os
import re
import shutil

ROOT=Path(__file__).resolve().parents[2]
PACK=ROOT/'docs/art/mockup-assets-20261119'
spec=importlib.util.spec_from_file_location('model_gen',ROOT/'Tools/pf/model-gen.py')
mg=importlib.util.module_from_spec(spec)
spec.loader.exec_module(mg)
BASE=PACK/'models/character-purple/build.py'
APPENDIX=(ROOT/'Tools/ModelGen/variant_accessories.py.txt').read_text()
SIZES={
 'blue':(.85,.82),'green':(1.01,.80),'orange':(1.08,.80),
 'yellow':(1.065,.80),'red':(.846,.80),'pink':(.963,.80),
 'brown':(1.026,.80),'cyan':(1.05,.80),'hidden':(.846,.72)}

def source_for(variant):
    text=BASE.read_text()
    text=re.sub(r'curl_profile = \[.*?mg.smooth\(accessory, angle=75\)',
                '# Accessory supplied by family appendix.',text,flags=re.S)
    text=text.replace('"purple"',repr(variant))
    text=text.replace('    lash_vertices, lash_faces = [], []',
        "    if VARIANT not in ('purple','pink'):\n        lashes=[]\n    lash_vertices, lash_faces = [], []")
    marker='    brow_controls = ['
    edits="""    if VARIANT in ('red','orange'):
        brow_path=[(.095,height+.125,.012),(.13,height+.15,.018),
                   (.19,height+.20,.020),(.24,height+.225,.010)]
    elif VARIANT == 'yellow':
        brow_path=[(.095,height+.245,.008),(.13,height+.24,.013),
                   (.19,height+.19,.013),(.24,height+.165,.004)]
"""
    text=text.replace(marker,edits+marker)
    text=text.replace('smile_controls = [',"""if VARIANT == 'red':
    smile_profile=[(-.09,.315,.008),(-.05,.35,.011),(0,.36,.012),(.055,.345,.01),(.09,.32,.007)]
elif VARIANT == 'yellow':
    smile_profile=[(-.055,.31,.007),(-.035,.34,.009),(0,.35,.01),(.035,.34,.009),(.055,.31,.007)]
elif VARIANT == 'hidden':
    smile_profile=[(-.025,.32,.006),(0,.315,.007),(.025,.32,.006)]
smile_controls = [""")
    if variant=='hidden':
        text=text.replace('make_eye(1, "L", 0.501)\nmake_eye(-1, "R", 0.487)',"""for side,suffix in ((1,'L'),(-1,'R')):
    center=side*.164
    tube_part('Eye_'+suffix,[(center-.065,-.303,.494,.009),
              (center,-.31,.473,.01),(center+.065,-.303,.494,.009)],
              7,3,(center,-.30,.49),'ink',body,cap_steps=1)
""")
    return 'VARIANT='+repr(variant)+'\n'+text+'\n'+APPENDIX

def build(variant):
    height,depth=SIZES[variant]
    out=PACK/'models'/('character-'+variant)
    out.mkdir(parents=True,exist_ok=True)
    source=source_for(variant)
    script=out/'build.py'
    script.write_text(source,encoding='utf-8')
    refused=mg.script_guard(source)
    if refused: raise RuntimeError(f'{variant}: refused {refused}')
    base_req=json.loads((ROOT/'Tools/ModelGen/requests/mockup-v1-character-purple.json').read_text())
    req={**base_req,'id':'character-'+variant,'out':out.relative_to(ROOT).as_posix(),
         'subject':f'{variant} variant derived from the approved shared body and family mockup.',
         'size':{'width':1.0,'height':height,'length':depth},
         'notes':'Family derivative. Shared approved body; dedicated accessory and expression. No rig or clips.'}
    req_path=ROOT/'Tools/ModelGen/requests'/('character-'+variant+'.json')
    req_path.write_text(json.dumps(req,indent=2)+'\n')
    facts=mg.run_blender(script,req,out)
    (out/'facts.json').write_text(json.dumps(facts,indent=2)+'\n')
    if not facts.get('ok'):
        return {'variant':variant,'ok':False,'checks':[c for c in facts.get('checks',[]) if not c['ok']],
                'error':facts.get('script_error') or facts.get('harness_error')}
    mg.views_sheet(out)
    fbx=out/(req['id']+'.fbx')
    (out/'model.fbx').replace(fbx)
    provenance={'schema':'model-gen-derivative/v1','status':'pending-user-review',
        'source_base':'models/character-purple/build.py',
        'design_reference':'reference/mockup.png','approval_text':'duyệt nha',
        'author':'Codex, geometry-only shared-body derivative',
        'facts':mg.compact_facts(facts),
        'script':{'path':script.relative_to(ROOT).as_posix(),'sha256':mg.sha256(script)},
        'fbx':{'path':fbx.relative_to(ROOT).as_posix(),'sha256':mg.sha256(fbx)}}
    (out/'model.gen.json').write_text(json.dumps(provenance,indent=2)+'\n')
    return {'variant':variant,'ok':True,'tris':facts['tris'],'size':facts['size']}

if __name__=='__main__':
    os.environ.setdefault('BLENDER_BIN',str(ROOT/'Tools/ModelGen/.runtime/blender-5.0.1-windows-x64/blender.exe'))
    results=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        for result in pool.map(build,SIZES):
            results.append(result)
            print(json.dumps(result),flush=True)
    (PACK/'models/variant-validation.json').write_text(json.dumps(results,indent=2)+'\n')
