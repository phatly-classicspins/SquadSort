# Loading fill correction

## Follow-up: actual scene overflow

The initial prefab preview did not validate scene overrides. The user's real Loading scene retained four ProgressionFill RectTransform overrides: width delta +387.8937, height delta -37.0902, anchored X 211.83597 and Y 7.8871. At 60% this produced a 731.09×19.91 fill outside the 606×85 frame. Reverted only that RectTransform's four overrides through Unity and saved Loading.unity.

The real Loading CanvasScaler also had referencePixelsPerUnit 100 while the fill sprite uses PPU 1, which elongated the sliced caps even after containment was corrected. Authored referencePixelsPerUnit 1 on the real canvas/scaler. Final scene diff: four override entries removed and one scaler value changed.

Observed the actual saved Loading.unity as the Play start scene, without a replacement prefab fixture: natural hold at 60%, then LoadingScreenView.CompleteAsync to 100%. Final screenshots are RealScene-Final-60.png and RealScene-Final-100.png; earlier RealScene captures precede the ruler correction. Fill stays contained with correctly rounded caps. Editor start-scene setting restored to null and the original Master workspace restored afterward.

The real-scene observation logged four `DOTWEEN ► Couldn't load Modules system` errors, a plugin error also observed in earlier sessions; origin not resolved by this layout correction. Captures and completion still executed. Final engine-free log: Logs/gate/loading-scene-ruler-final.log. No full boot integration pass or automated runtime-suite claim is made.

The LoadingUI prefab used a sliced fill with asymmetric vertical offsets (21 bottom / 15 top). SetProgress also rewrote its right offset using fixed 22/44/15 values as progress changed. This coupled the cosmetic inset to progress and displaced the fill within the frame.

Authored a FillArea child in Unity, inset 17 reference pixels horizontally and 14 vertically within the existing 606×85 frame. The sliced fill stretches within that 572×57 area, with zero offsets. SetProgress clamps to [0,1], updates fill visibility and percentage, and changes only the horizontal anchor for sliced images. All inset values live in the prefab. Existing fill sprite, frame, screen positioning and loading flow remain intact.

Observed the real prefab in a callback-free runtime preview at 1080×1920, named states 0%, 10%, 50%, 100%. Widths: 0, 57.2, 286, 572 reference pixels; height stays 57 and both offsets stay zero. Empty fill is hidden. Captures show rounded ends and symmetric containment at full progress. This tests the visual surface, not timing of the full boot sequence.

Final Unity Console error query: zero; Editor idle and compiled. Engine-free gate: 481 passed, zero failed (Logs/gate/loading-fill-final.log).

Skipped local EditMode suite: no persistent Editor-side code change.

Skipped automated PlayMode suite: prohibited locally; CI runtime gate unverified.

Skipped keyed Slow leg: override declares no KEYED_TEST.
