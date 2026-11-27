# Squad UI integration — 2026-11-26

Applied the approved white/pink tray and buddy theme to 25 existing prefabs using live Unity-MCP. 228 Image replacements, 150 referenced native textures; PPU 1, uncompressed, no mipmaps. Original UI anchors, pivots, local transforms, screen rectangles, hit areas and listeners were preserved. Exact 847 RectTransforms compared against the prior 720×1280 reference; no geometry differences. Next Element label auto-sized 29.7→28.1 inside its unchanged rect.

## Art and source

Source PNGs and generated native sprites: Assets/SqadSort/Content/UI/SquadTheme. Built-in imagegen created masters, backgrounds, logo and icons; prompts in asset-prompts.json. Unity-MCP slices atlas shapes, bakes structural nine-slice frames to each existing consumer size, imports and wires them. Consumers use native PNGs; illustration aspect ratio is preserved. No entire mockup is stretched over the game. The 150 native assets match their baseline target pixel dimensions. Two BoosterConfigSO icon assets were updated, including runtime icon references. Existing vendor tutorial video clips are unchanged; this pass supplies static art, not replacement animation videos.

Rerunnable editor helper: Tools/UiMockup/IntegrateSquadTheme.cs. Prefab captures: Tools/UiMockup/CaptureInstalledTheme.cs. No new runtime skin override, assembly, scene, screen, dialog, event or DI path.

Localization: existing LocKeys retained, CSV read/written with RFC 4180 parser, matching en/vi JSON values updated. Existing vi entries were English placeholders and remain English; this is not a Vietnamese translation pass. Undo/Shuffle/Remove and Hidden Buddy/House descriptions now match the GDD mechanics. Win Continue label goes through existing WinNext key. Economy and save rules were not changed.

## Runtime observation

Two bounded Play entries: first Home, Shop, Settings, Gameplay (level 20), Shuffle purchase fixture and Victory fixture; then an earned visual recheck of the corrected replay icon, restart confirmation and Remove purchase fixture. All screenshots 1080×1920. Replay opens confirmation and close cancels it; no restart confirmed. Fixtures use real prefabs/Views with no-op transaction callbacks. No purchase, reward grant, booster consumption or win was triggered. Editor exited Play Mode.

First observation found a blank replay icon after sprite remapping. Fixed source lookup against baseline node names and checked the actual visible replay icon and confirmation in the second entry. Earlier blank-icon Gameplay.png is retained as debug history; review.html uses Gameplay-ReplayFixed.png.

Shop normal state follows existing purchased/expired-offer rules, so a separate GDD fixture shows Remove Ads $5.99 / 1500 / each skill×1 and Starter $1.99 /2500 /each skill×2 /2h. Game catalog was already correct. Static prefab captures deliberately are not economy evidence: some unbound sample text remains old until the controller binds data. The old RentSlot source UI/flow is retained; implementing a replacement GDD RV Container flow is outside this art swap.

## Validation

Engine-free gate: 481 passed, 0 failed, 0 skipped. Final log Logs/gate/ui-theme-final-20261126.log. Existing gate config override points to the real SqadSort test project because checked-in default names MyGame. Existing slow ShippedLevelTests.LevelIsSolvableWithoutBoosters(69) takes about 4.7s; no new tests were added.

Unity 6000.3.13f1: Console Error count 0 before and during both observations. Import validation: 150 referenced sprite textures, 0 invalid PPU/type settings. Removed 18 unused trial textures through AssetDatabase. Original base-layout geometry intact. Unity emits trailing spaces in serialized empty prefab override values; those were not hand-edited as YAML.

Skipped: local EditMode suite (no persistent Editor-side code change).
Skipped: local PlayMode automated suite (project contract prohibits it).
Skipped: keyed Slow leg (KEYED_TEST not configured).
Skipped: player build.
CI-owned/unverified: runtime automated tests and build. No .github/workflows or other CI entry found in this checkout; PlayMode has no observed merge gate. Screenshots are bounded visual evidence, not a suite pass.
