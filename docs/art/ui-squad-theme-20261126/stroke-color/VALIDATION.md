# Stroke color correction

Sampled dominant dark stroke pixels from approved reference-Settings.png, excluding white faces and bright backgrounds. Authored role colors in Unity: purple #500450, green #004000, blue #0004C4. Both outline and hard downward underlay use the same sampled role color; stroke geometry is unchanged. Body cream outline is unchanged.

Observed settled gameplay Settings at 1080×1920; close succeeded. Final Unity Console error query: 0. Engine-free gate: 481 passed, 0 failed (Logs/gate/ui-stroke-color-final.log). Comparison: compare-color.jpg.

Skipped local EditMode suite: no persistent Editor-side change.

Skipped automated PlayMode suite: prohibited locally; CI runtime evidence unverified.

Skipped keyed Slow leg: no KEYED_TEST declared in override.
