import json
from pathlib import Path
root=Path('docs/art/popup-content-review-20261127')
plan=json.loads((root/'content-plan.json').read_text());by_node={}
for op in plan:
 if op['role']=='title':by_node.setdefault(op['node'],[]).append(op['prefab'])
corrections={}
for f in (root/'metrics').glob('*.json'):
 d=json.loads(f.read_text());headers=[g for g in d['graphics'] if g['sprite']=='popup-topper' or g['sprite'].startswith('header-') and g['path'].split('/')[-1] in ['BoosterHeader','TitleRibbon','Ribbon']]
 if not headers:continue
 for t in d['texts']:
  if t['path'].split('/')[-1] not in ['Title','BoosterName']:continue
  node=t['path'].split('/',1)[1];matches=[op for op in plan if op['role']=='title' and op['node']==node]
  # Root prefab name disambiguates equal paths such as Card/Header/Title.
  stem=t['path'].split('/')[0].replace('(Clone)','')+'.prefab'
  matches=[op for op in matches if op['prefab'].endswith('/'+stem)]
  if not matches:continue
  h=headers[0];r=h['rect'];w=r[2]-r[0];hh=r[3]-r[1]
  b=[r[0]+w*50/515,r[1]+hh*26/236,r[0]+w*465/515,r[1]+hh*110/236] if h['sprite']=='popup-topper' else [r[0]+10,r[1]+8,r[2]-10,r[3]-8]
  ink=t['ink'];dx=(b[0]+b[2]-ink[0]-ink[2])/2;dy=(b[1]+b[3]-ink[1]-ink[3])/2
  key=(matches[0]['prefab'],node);corrections.setdefault(key,[]).append((dx,dy))
ops=[]
for (prefab,node),values in corrections.items():
 dx=sum(v[0] for v in values)/len(values);dy=sum(v[1] for v in values)/len(values)
 if abs(dx)>1 or abs(dy)>1:
  assert max(abs(dx),abs(dy))<12
  ops.append({'prefab':prefab,'node':node,'dx':dx,'dy':dy})
(root/'optical-center-plan.json').write_text(json.dumps(ops,indent=2))
print(f'{len(ops)} small optical-center corrections')
