# Uniform resource group typography

Audited every prefab under Assets/SqadSort and Assets/Vendor/MoneySort for MoneyText, LivesText and LifeTimerText. Before: mixed Lilita/Baloo fonts, sizes 26.35–49.56 and mixed autosize settings. The same named resource labels also occur in the victory wallet.

All 19 labels across 7 prefabs now use static Baloo 2 ExtraBold, font size 36, the existing shared white/purple stroke material, no autosizing, and no wrapping. Min/max fields also match 36. The old fitting helper preserves this resource type role instead of restoring its previous disparate sizes. No View code writes the typography.

before.json and after.json verify every original RectTransform rect and scale is unchanged. Final audit asserts all 19 labels share font and size. Observed Home → Shop → Home → Gameplay at 1080×1920; runtime binding retains fontSize 36 and autosize false for `5`, `Full`, and `180`. groups-comparison.jpg shows those three screen states. This does not establish layout fit for unusually long currency strings or infinite-life timer formats; those retain their existing boxes.

Final Unity Console errors: zero; Editor idle. Engine-free gate: 481 passed, zero failed (Logs/gate/resource-text-final.log).

Skipped local EditMode suite: no persistent Editor-side code change.

Skipped automated PlayMode suite: prohibited locally; CI runtime gate unverified.

Skipped keyed Slow leg: override declares no KEYED_TEST.
