# Mockup alignment correction — 2026-11-26

Approved references are normalized to 720×1280. Home and Settings originated in multi-panel concept boards, so normalization is per panel; this is not a claim of pixel-identical artwork.

The existing prefabs were fitted in Unity via `AlignUiToMockup.cs`: measured Home logo/Play/resources/footer, Shop awning/cards/buttons/footer and Settings body/header/controls. `measured-fits.json` records 61 fitted visual nodes. Existing dialog variants reuse the corrected body/topper assets. All 25 prefab previews are in `fitted/`; observed runtime captures are in `runtime/`.

Native control sprites have transparent corners and PPU 1. Shared dialog bodies use nine-slice borders (55,65,55,75); footer borders preserve corners. Character/logo sprites preserve aspect. No runtime positioning or color overrides were added. Footer visual selection chooses authored Home/Shop sprites and toggles selected/unselected art; captions retain localization.

Observed Home → Shop → Home → Gameplay → Settings → close at 1080×1920. Rechecked Shop after fixing a nested-prefab duplicate icon. Shop comparison uses no-op GDD offer callbacks: no purchases or progression changes. The existing editor cheat overlay remains visible in Game View captures.

Engine-free gate: 481 passed, 0 failed (`Logs/gate/ui-alignment-final.log`). Unity idle, compilation finished. Console error query returned 0 after the first observed session; after the second session it contained `DOTWEEN ► Couldn't load Modules system`. This integration does not establish the origin of that plugin error. The observed UI navigation and captures completed. The final footer correction changed prefab art only.

Skipped local EditMode suite: no persistent Editor-side code change.

Skipped automated PlayMode suite: prohibited locally by project contract; no CI PlayMode evidence available.

Skipped keyed Slow leg: project override declares no KEYED_TEST.

Remaining differences include generated-art contours/shading and live values (level, coins, lives, toggle state). The correction matches measured placement rather than asserting identical concept pixels.
