# Approved mockup integration

Source: `docs/art/mockup-assets-20261119/reference/mockup.png` and the approved asset pack.

The existing Board prefab uses real FBX characters and queue trays. The waiting area uses independent FBX floor and rim modules so the level topology stays authoritative. The seven-cell sample board is retained as an art reference; it does not replace gameplay geometry. Locked tube houses retain their existing geometry.

Shared character prefabs live in the existing LookLibrary, in domain color order: red, orange, yellow, green, dark green, cyan, blue, purple, pink, brown. Dark green reuses the flower-crown mesh with a separate material. Gray hidden characters and authored sleeping eyes preserve hidden/awake presentation. Existing EventSystem targets and movement/selection/reveal animations remain in BuddyView.

The Board prefab owns pitch 120, board tilt 32 degrees, upright blend .65, character scale 105, and top/bottom margins 315. Courtyard ground uses URP Unlit; model palette uses shared URP Lit materials with smoothness .3. HUD uses the approved atlas, tight sprite rectangles, PPU 1, no NPOT resizing, static bold text, 132px top controls and 225px boosters. HUD remains under its existing Ui host; meshes remain under the existing WorldRoot and GamePlay layer.

To reapply in Edit Mode use `SqadSort/Art/Apply Approved Mockup`. The installer preserves existing prefab GUIDs and references. Run framework codegen after introducing or renaming any prefab.

Validation results are recorded separately after the final visual check.
