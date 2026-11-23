"""Build both locked queue covers from the user-approved obstacle mockup."""
import importlib.util
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / 'docs/art/locked-house-models-v1'
spec = importlib.util.spec_from_file_location('model_gen', ROOT / 'Tools/pf/model-gen.py')
mg = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mg)
os.environ.setdefault('BLENDER_BIN', str(ROOT / 'Tools/ModelGen/.runtime/blender-5.0.1-windows-x64/blender.exe'))


def main():
    results = []
    palette = {
        'white': 'F5F0F7', 'gold': 'FFD23F', 'gold_shadow': 'D99912', 'ink': '251F35',
        'purple_wall': 'DAB8EC', 'purple_roof': 'AC4BE8', 'purple_roof_light': 'BE63EF',
        'purple_door': 'B558DB', 'purple_highlight': 'EFDDF8', 'purple_inset': '744098',
        'pink_wall': 'F4C4DA', 'pink_roof': 'F55BAE', 'pink_roof_light': 'FF78C3',
        'pink_door': 'E95C9E', 'pink_highlight': 'FFE5EF', 'pink_inset': 'AC3E75',
    }
    template = (ROOT / 'Tools/ModelGen/locked_house_geometry.py.txt').read_text()
    for tone in ('purple', 'pink'):
        name = 'locked-house-' + tone
        out = PACK / 'models' / name
        out.mkdir(parents=True, exist_ok=True)
        request = {
            'schema': 'model-gen-request/v1', 'id': name,
            'subject': tone.capitalize() + ' toy house cover over a four-slot queue, rounded roof tiles, arched hinged door, golden padlock, matching character medallion.',
            'size': {'width': 1.28, 'height': 2.35, 'length': 4.6},
            'palette': palette, 'max_tris': 14000,
            'parts': [{'name': n} for n in ('Body', 'Roof', 'Door', 'Padlock', 'Shackle', 'Emblem')],
            'references': [{'path': 'docs/art/mockup-integration/mockup-elements-v2.png', 'role': 'approved-design'}],
            'out': out.relative_to(ROOT).as_posix(), 'hero': {'pitch': 27, 'yaw': 22},
            'notes': 'User approved obstacle-inclusive mockup and explicitly requested the 3D model. Preserve this design; do not introduce a new design. Blender -Y front; Unity +Z front, +Y up. Bottom-center pivot, real hollow doorway, left-hinge Door anchor and separate Padlock/Shackle anchors. No animation clips. Existing 1.28 by 4.6 queue footprint wins over perspective in the concept.',
            'consumer': 'Art handoff only; Unity integration is a separate consuming change.',
        }
        source = template.replace('__TONE__', tone)
        refused = mg.script_guard(source)
        if refused:
            raise RuntimeError(refused)
        script = out / 'build.py'
        script.write_text(source)
        (ROOT / 'Tools/ModelGen/requests' / (name + '.json')).write_text(json.dumps(request, indent=2) + '\n')
        shutil.copy2(ROOT / 'docs/art/mockup-integration/mockup-elements-v2.png', out / 'approved-mockup.png')
        facts = mg.run_blender(script, request, out)
        print(json.dumps({'id': name, 'ok': facts.get('ok'), 'tris': facts.get('tris'), 'size': facts.get('size'), 'failed': [c for c in facts.get('checks', []) if not c['ok']], 'error': facts.get('script_error') or facts.get('harness_error')}), flush=True)
        if not facts.get('ok'):
            raise RuntimeError('Model checks failed; inspect ' + str(out / 'facts.json'))
        mg.views_sheet(out)
        fbx = out / (name + '.fbx')
        (out / 'model.fbx').replace(fbx)
        provenance = {
            'schema': 'model-gen-derivative/v1', 'status': 'pending-user-review',
            'approved_design': {'path': 'docs/art/mockup-integration/mockup-elements-v2.png', 'sha256': mg.sha256(ROOT / 'docs/art/mockup-integration/mockup-elements-v2.png'), 'approval': 'User: ok tạo model 3d đi'},
            'facts': mg.compact_facts(facts),
            'script': {'path': script.relative_to(ROOT).as_posix(), 'sha256': mg.sha256(script)},
            'fbx': {'path': fbx.relative_to(ROOT).as_posix(), 'sha256': mg.sha256(fbx)},
            'outline_comparison': 'Not measured: built directly against the approved perspective mockup, not an orthographic blueprint.',
        }
        (out / 'model.gen.json').write_text(json.dumps(provenance, indent=2) + '\n')
        results.append({'id': name, 'ok': True, 'tris': facts['tris'], 'size': facts['size']})
    (PACK / 'geometry-validation.json').write_text(json.dumps(results, indent=2) + '\n')


if __name__ == '__main__':
    main()
