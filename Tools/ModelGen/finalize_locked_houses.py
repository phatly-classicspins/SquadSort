"""Document and package the validated obstacle-model handoff."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / 'docs/art/locked-house-models-v1'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


audit = json.loads((PACK / 'fbx-roundtrip-validation.json').read_text())
assert len(audit) == 3 and all(row['ok'] for row in audit)
assets = []
for row in audit:
    folder = PACK / 'models' / row['asset']
    fbx = folder / (row['asset'] + '.fbx')
    assets.append({
        **row, 'fbx': fbx.relative_to(PACK).as_posix(), 'sha256': sha(fbx),
        'blend': (folder / (row['asset'] + '.blend')).relative_to(PACK).as_posix(),
        'status': 'pending-user-visual-review',
        'parts': ['Body', 'Roof', 'Door', 'Padlock', 'Shackle', 'Emblem'] if 'house' in row['asset'] else ['Body', 'Eye_L', 'Eye_R', 'Mouth', 'Arm_L', 'Arm_R', 'Foot_L', 'Foot_R', 'Accessory'],
    })
manifest = {
    'schema': 'obstacle-model-handoff/v1', 'date': '2026-11-20',
    'approved_reference': '../mockup-integration/mockup-elements-v2.png',
    'format': 'FBX 7.4, triangulated, named palette materials',
    'unity_contract': {'units': 'metres', 'import_scale': 1, 'front': '+Z', 'up': '+Y', 'pivot': 'bottom-center'},
    'attachments': [{'child': 'Shackle', 'parent': 'Padlock'}, {'child': 'Padlock', 'parent': 'Door'}],
    'attachment_rule': 'FBX moving parts are independent root meshes to avoid Blender nested-empty space-bake corruption. Attach with world transform preserved before animation. Closed .blend sources and open-pose scene can be used for authoring.',
    'animation_clips': [], 'unity_integration': 'not performed in this art-only handoff',
    'assets': assets,
}
(PACK / 'model-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
lines = ['# Obstacle models — approved mockup', '',
         'Two newly built real 3D locked-house covers, purple and pink. The existing gray mystery character is included unchanged.', '',
         '| Model | Triangles | Width × height × depth (m) |', '|---|---:|---|']
for row in audit:
    d = row['size']
    lines.append(f"| {row['asset']} | {row['tris']} | {d['width']:.3f} × {d['height']:.3f} × {d['length']:.3f} |")
lines += ['',
    'Each house covers the original 1.28 × 4.6 m four-slot queue footprint. Its silhouette includes rounded roof tiles, a color-matched face medallion, a hollow arched doorway, hinged door, gold padlock and separate shackle.', '',
    '## Files', '',
    '- `models/<name>/<name>.fbx`: exported geometry, separate named moving parts.',
    '- `models/<name>/<name>.blend`: editable source scene, created by reimporting the delivered FBX.',
    '- `models/<house>/build.py`: deterministic geometry source for the project model-gen harness.',
    '- `preview/locked-houses-studio.png` and `.blend`: actual model render and studio scene.',
    '- `preview/door-open-pose.png` and `.blend`: static articulation demonstration, not an animation clip.',
    '- `model-manifest.json`: hashes, dimensions, palette/axis contract and attachment instructions.', '',
    '## Motion and import', '',
    'Import at scale 1. Unity front is +Z, up +Y, pivot is bottom-center. Shader remapping uses `M_<palette-key>` names. Purple/pink house material names are distinct so one variant never recolors the other.', '',
    'Door has its pivot at the left jamb. FBX parts are separate roots, preserving their pivots and exact geometry. In the consuming prefab parent Shackle under Padlock, then Padlock under Door, preserving world transforms. This avoids a measured Blender FBX defect with nested empty space-bake. The editable closed house .blend scenes and open-pose preview supply the authoring hierarchy. No rig or animation clip is included.', '',
    '## Validation', '',
    'Both new models passed model-gen size, triangle-budget, ground/center pivot, part-name, material and transform checks. All three FBX files passed reimport dimension, triangle, ground/center, material and part checks. The houses have real openings behind the doors. Visual inspection uses the rendered FBX geometry, not an AI redraw.', '',
    'The approved reference is a perspective mockup. Orthographic silhouette IoU was not measured and is not claimed. Exported models remain pending user visual review.', '',
    'Engine-free project gate: 304 passed, 0 failed; source greps and Domain/Application/Gen type checks passed.', '',
    'Skipped: Unity import/runtime check; this request creates the model assets only.',
    'Skipped: Unity EditMode tests; no Editor-side Unity change in this request.',
    'Skipped: local PlayMode suite/player build; CI-owned and unverified.',
    'Skipped: keyed Slow gate leg; no KEYED_TEST configured.',
]
(PACK / 'README.md').write_text('\n'.join(lines) + '\n')
archive = ROOT / 'docs/art/locked-house-models-v1.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as target:
    for path in sorted(PACK.rglob('*')):
        if path.is_file() and path.suffix not in {'.blend1', '.log'}:
            target.write(path, path.relative_to(PACK.parent))
print(json.dumps({'zip': str(archive), 'bytes': archive.stat().st_size, 'models': len(assets)}))
