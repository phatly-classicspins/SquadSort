import json
from pathlib import Path
root=Path('docs/art/popup-content-review-20261127');ops={}
def prefab(name):
 if name.startswith('Settings'):return 'Assets/Vendor/MoneySort/Settings/Prefabs/SettingsUI.prefab'
 if name=='RestorePurchase':return 'Assets/SqadSort/Prefabs/UI/RestorePurchaseUI.prefab'
 for prefix in ['BoosterPurchase','BoosterUnlock','ElementUnlock','Rating']:
  if name.startswith(prefix):name=prefix;break
 if name=='ConfirmHome':name='Lose'
 return 'Assets/Vendor/MoneySort/Source/Game/Prefabs/UI/'+name+'UI.prefab'
def put(d,t,role,rect=None):
 node=t['path'].split('/',1)[1];key=(prefab(d['name']),node)
 value={'prefab':key[0],'node':node,'role':role,'rect':rect}
 if key in ops and role=='body':
  old=ops[key]['rect'];rect[1]=min(rect[1],old[1]);rect[3]=max(rect[3],old[3])
 ops[key]=value
for f in (root/'metrics').glob('*.json'):
 d=json.loads(f.read_text());gs=d['graphics']
 hs=[g for g in gs if g['sprite']=='popup-topper' or g['sprite'].startswith('header-') and g['path'].split('/')[-1] in ['BoosterHeader','TitleRibbon','Ribbon']]
 panel=max([g for g in gs if g['sprite'].startswith('popup-') and g['sprite']!='popup-topper'],key=lambda g:(g['rect'][2]-g['rect'][0])*(g['rect'][3]-g['rect'][1]),default=None)
 for t in d['texts']:
  leaf=t['path'].split('/')[-1];r=t['rect'];cx=(r[0]+r[2])/2;cy=(r[1]+r[3])/2
  if leaf in ['Title','BoosterName'] and hs:
   h=min(hs,key=lambda g:abs(sum(g['rect'][1::2])/2-cy));b=h['rect'];w=b[2]-b[0];hh=b[3]-b[1]
   face=[b[0]+w*50/515,b[1]+hh*26/236,b[0]+w*465/515,b[1]+hh*110/236] if h['sprite']=='popup-topper' else [b[0]+10,b[1]+8,b[2]-10,b[3]-8]
   put(d,t,'title',[face[0]+16,face[1]+7,face[2]-16,face[3]-7])
  elif leaf in ['Description','Message'] and panel:
   b=panel['rect'];cx=(b[0]+b[2])/2;w=min(r[2]-r[0],b[2]-b[0]-64)
   h=max(r[3]-r[1],t['preferred'][1]+16,t['ink'][3]-t['ink'][1]+16)
   put(d,t,'body',[cx-w/2,cy-h/2,cx+w/2,cy+h/2])
  elif t['overflow']:
   # Keep CTA/row geometry; let TMP fit its content instead of clipping or masking it.
   put(d,t,'fit')
(root/'content-plan.json').write_text(json.dumps(list(ops.values()),indent=2),encoding='utf-8')
print(f'{len(ops)} text adjustments across {len(set(p for p,n in ops))} prefabs')
