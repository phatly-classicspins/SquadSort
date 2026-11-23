# Obstacle models — approved mockup

Two newly built real 3D locked-house covers, purple and pink. The existing gray mystery character is included unchanged.

| Model | Triangles | Width × height × depth (m) |
|---|---:|---|
| character-hidden | 2408 | 1.025 × 0.846 × 0.725 |
| locked-house-pink | 9564 | 1.280 × 2.350 × 4.600 |
| locked-house-purple | 9528 | 1.280 × 2.350 × 4.600 |

Each house covers the original 1.28 × 4.6 m four-slot queue footprint. Its silhouette includes rounded roof tiles, a color-matched face medallion, a hollow arched doorway, hinged door, gold padlock and separate shackle.

## Files

- `models/<name>/<name>.fbx`: exported geometry, separate named moving parts.
- `models/<name>/<name>.blend`: editable source scene, created by reimporting the delivered FBX.
- `models/<house>/build.py`: deterministic geometry source for the project model-gen harness.
- `preview/locked-houses-studio.png` and `.blend`: actual model render and studio scene.
- `preview/door-open-pose.png` and `.blend`: static articulation demonstration, not an animation clip.
- `model-manifest.json`: hashes, dimensions, palette/axis contract and attachment instructions.

## Motion and import

Import at scale 1. Unity front is +Z, up +Y, pivot is bottom-center. Shader remapping uses `M_<palette-key>` names. Purple/pink house material names are distinct so one variant never recolors the other.

Door has its pivot at the left jamb. FBX parts are separate roots, preserving their pivots and exact geometry. In the consuming prefab parent Shackle under Padlock, then Padlock under Door, preserving world transforms. This avoids a measured Blender FBX defect with nested empty space-bake. The editable closed house .blend scenes and open-pose preview supply the authoring hierarchy. No rig or animation clip is included.

## Validation

Both new models passed model-gen size, triangle-budget, ground/center pivot, part-name, material and transform checks. All three FBX files passed reimport dimension, triangle, ground/center, material and part checks. The houses have real openings behind the doors. Visual inspection uses the rendered FBX geometry, not an AI redraw.

The approved reference is a perspective mockup. Orthographic silhouette IoU was not measured and is not claimed. Exported models remain pending user visual review.

Engine-free project gate: 304 passed, 0 failed; source greps and Domain/Application/Gen type checks passed.

Skipped: Unity import/runtime check; this request creates the model assets only.
Skipped: Unity EditMode tests; no Editor-side Unity change in this request.
Skipped: local PlayMode suite/player build; CI-owned and unverified.
Skipped: keyed Slow gate leg; no KEYED_TEST configured.
