"""Package generated source art; does not edit image pixels or import Unity assets."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/art/mockup-assets-20261119'
SOURCE = Path('C:/Users/lymin/.codex/generated_images/01a1f2a2-04d2-7e93-aaf9-f3878f342b3e')
FILES = {
 'reference/mockup.png': 'exec-b959d3fa-7f05-4d59-a83c-651f67718233.png',
 'models/character-purple/blueprint.png': 'exec-079b6997-fd96-4b0e-87b1-92e228612e2a.png',
 'models/queue-four/blueprint.png': 'exec-3a8039d6-0e2c-47ac-8ab3-18b801479cbd.png',
 'models/waiting-board/blueprint.png': 'exec-0acd07c7-c1ac-4a37-a301-bf6cb4d0177f.png',
 'textures/courtyard-background.png': 'exec-65ebbfef-7949-4068-b683-60196353501a.png',
 'ui/ui-atlas.png': 'exec-1851afc6-aa06-4ef3-b7ac-bb9d696a0f5d.png',
}

def write_json(path, data):
 path.parent.mkdir(parents=True, exist_ok=True)
 path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def main():
 if any((OUT/'models').glob('*/*.fbx')):
  raise SystemExit('3D models are present. Use Tools/ModelGen/finalize_asset_pack.py; do not reset this package to the 2D-only stage.')
 inventory = []
 for relative, source in FILES.items():
  target = OUT / relative
  target.parent.mkdir(parents=True, exist_ok=True)
  shutil.copy2(SOURCE / source, target)
  with Image.open(target) as im:
   alpha = im.getchannel('A') if im.mode == 'RGBA' else None
   inventory.append({'path': relative, 'size': list(im.size), 'mode': im.mode,
    'alpha_extrema': list(alpha.getextrema()) if alpha else None,
    'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
  if relative.endswith('/blueprint.png') and not target.with_suffix('.png.gen.json').exists():
   write_json(target.with_suffix('.png.gen.json'), {
    'status': 'pending-user-review', 'generator': 'built-in image_gen',
    'purpose': '3D visual design reference; not a mesh',
    'source_mockup': '../../reference/mockup.png',
    'validation': 'Visually inspected. Harness proportions/silhouette checks not executed.',
    'notes': 'Some elevations retain stylized perspective. Numeric request dimensions win over the picture.'})

 # Pixel rectangles are metadata only; the original transparent atlas is preserved.
 with Image.open(OUT / 'ui/ui-atlas.png') as im:
  w, h = im.size
  windows = [
   ('undo', (0, 0, .5, .25)), ('shuffle', (.5, 0, 1, .25)),
   ('add-slot', (0, .25, .5, .5)), ('restart', (.5, .25, 1, .5)),
   ('settings', (0, .5, .5, .75)), ('button-empty', (.5, .5, 1, .75)),
   ('level-plate', (0, .75, .68, 1)), ('count-badge', (.68, .75, 1, 1))]
  rects = []
  for name, (x0, y0, x1, y1) in windows:
   x, y, r, b = round(x0*w), round(y0*h), round(x1*w), round(y1*h)
   rects.append({'id': name, 'rect_top_left': [x, y, r-x, b-y],
    'rect_unity_bottom_left': [x, h-b, r-x, b-y], 'pivot': [.5, .5]})
  write_json(OUT / 'ui/sprite-rects.json', {'texture': 'ui-atlas.png', 'size': [w,h],
   'pixels_per_unit': 1, 'sprites': rects,
   'notes': 'Atlas source art only. Rectangles are coarse slice windows; retain alpha. No Unity importer run.'})
  assert im.mode == 'RGBA' and im.getchannel('A').getextrema() == (0,255), 'Atlas must have real transparency'

 palette = {'purple':'B63AF2','blue':'168DFA','green':'80DF0B','orange':'FF8A10',
  'yellow':'FFE134','red':'F12C36','pink':'FF67C5','brown':'A95D36',
  'cyan':'29D9EB','hidden':'A5A4AE','white':'F5F0F7','ink':'251F35',
  'tray-rim':'EFEAF2','tray-floor':'B8B6C2','waiting-rim':'F2CEDD','gold':'FFD23F'}
 characters = [
  ('purple','Curled antennae; eyelashes; confident smile'),
  ('blue','Round dark glasses; thoughtful expression'),
  ('green','White daisy crown; cheerful expression'),
  ('orange','Spiky quiff; arched eyebrows; mischievous smile'),
  ('yellow','Small quiff; worried eyebrows'),
  ('red','Heavy angled brows; angry expression'),
  ('pink','Pink bow; eyelashes; friendly smile'),
  ('brown','Curled quiff; friendly expression'),
  ('cyan','Small tuft; relaxed cheeky expression'),
  ('hidden','Gray body; sleeping eyes; question-mark face marking')]
 write_json(OUT / 'design-tokens.json', {'palette_srgb': palette,
  'character_variants': [{'id': x, 'color': palette[x], 'design': note, 'status':'planned-3d'} for x,note in characters],
  'model_contract': {'format':'FBX', 'units':'metres at scale 1', 'unity_front':'+Z', 'unity_up':'+Y',
   'pivot':'bottom centre', 'mesh_not_sprite':True, 'animation_clips':'not authored',
   'character_budget_triangles':6000, 'queue_budget_triangles':2500,
   'rig_world_scale':'Integration decision; not baked into metre-scale source models'},
  'shared_character_parts':['Body','Eye_L','Eye_R','Mouth','Arm_L','Arm_R','Foot_L','Foot_R','Accessory']})

 requests = [
  ('character-purple','Purple cube creature; separate eyes, arms, feet and curled antennae.',
   {'width':1.0,'height':1.15,'length':.8},6000,
   ['Body','Eye_L','Eye_R','Mouth','Arm_L','Arm_R','Foot_L','Foot_R','Accessory']),
  ('queue-four','Open-entry four-character tray, ivory rounded U-shaped rim and inset gray floor.',
   {'width':1.28,'height':.26,'length':4.6},2500,['Floor','Rim']),
  ('waiting-board','Seven-cell stepped waiting tray; modular floor, perimeter and open queue ports.',
   {'width':4.6,'height':.26,'length':2.3},5000,['Floor','Rim'])]
 for name, subject, size, budget, parts in requests:
  request = {'schema':'model-gen-request/v1', 'id':'mockup-v1-'+name,
   'subject':subject, 'size':size, 'palette':palette, 'max_tris':budget,
   'parts':[{'name':p,'note':'Keep named part separate; pivot at its motion anchor.'} for p in parts],
   'references':[{'path':str((OUT/'reference/mockup.png').relative_to(ROOT)).replace('\\','/'),'role':'style'}],
   'out':str((OUT/'models'/name).relative_to(ROOT)).replace('\\','/'),
   'notes':'Blueprint is pending user approval. Do not build yet. No FBX has been produced.',
   'consumer':'Future Unity import, outside this art-only handoff.'}
  request_path = ROOT/'Tools/ModelGen/requests'/('mockup-v1-'+name+'.json')
  if not request_path.exists():
   write_json(request_path, request)
 write_json(OUT / 'asset-manifest.json', {'status':'2d-source-and-3d-design-review',
  'generation_mode':'built-in image_gen', 'files':inventory,
  'models_delivered':[], 'models_pending':['character family','queue-four','waiting-board'],
  'unity_import_performed':False})
 (OUT / 'README.md').write_text('''# Mockup asset pack\n\nStatus: **2D source art + 3D references awaiting approval. No FBX/GLB or rig is delivered yet.**\n\n## Available files\n\n- `textures/courtyard-background.png`: clean background plate.\n- `ui/ui-atlas.png`: eight controls on real alpha transparency.\n- `ui/sprite-rects.json`: eight pixel slice windows, including Unity bottom-left coordinates. The last row is intentionally not a uniform two-column slice.\n- `models/character-purple/blueprint.png`: four-view character form reference.\n- `models/queue-four/blueprint.png`: open-entry 3D queue reference.\n- `models/waiting-board/blueprint.png`: stepped waiting tray reference.\n- `design-tokens.json`: ten planned character variants, materials, dimensions and mesh contract.\n- `prompts.json`: exact generation prompts and mode.\n- `reference/mockup.png`: source style reference.\n\nThese are newly generated assets derived from the mockup, not pixels extracted from the original game.\n\n## Real 3D scope\n\nCharacters and both container types must be real meshes, not baked character/board sprites. The shared body has independent eyes, mouth, arms, feet and accessory parts. Ten planned variants: purple, blue, green, orange, yellow, red, pink, brown, cyan and masked gray. FBX convention: metres, Y up, +Z front, bottom-centre pivot. Sleeping/awake face states and animation are future mesh/rig work, not present in these images.\n\nThe waiting tray is modular: floor cells and edge/corner pieces follow occupancy, with no wall blocking a queue port. Its blueprint illustrates one seven-cell layout. Do not bake that single layout into every level.\n\n## Review and limitations\n\nThe image sheets are visual references, not dimension-verified CAD. Some queue elevations retain perspective. Request dimensions govern modelling. Model-gen silhouette, triangle, pivot and export checks have not been run. Blender was not found in the inspected standard locations/PATH; prepare a Blender runtime before model build.\n\nThe project skill `.claude/skills/pf-model-gen/SKILL.md` explicitly requires approval of the blueprint before `build`. All blueprint provenance remains `pending-user-review`; do not treat the generated images as completed models.\n\nNo Unity assets, scenes, prefabs or game code changed by this handoff.\n\nSkipped: Unity import and visual integration.\nSkipped: Unity EditMode / PlayMode suites and batchmode.\nSkipped: engine-free game gate (art-only package; no gameplay/source change).\n''', encoding='utf-8')
 archive = OUT.parent / (OUT.name+'.zip')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for path in sorted(OUT.rglob('*')):
   if path.is_file(): z.write(path, path.relative_to(OUT))
 print(json.dumps({'folder':str(OUT),'zip':str(archive),'images':len(inventory),
  'ui_alpha_verified':True,'mesh_files':0},ensure_ascii=False))

if __name__ == '__main__':
 main()
