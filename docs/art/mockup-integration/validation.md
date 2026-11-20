# Integration validation — 2026-11-20

- Unity 6000.3.13f1 imported and compiled the integration; final visual capture is `preview.png` at 1080×1920.
- Observed existing saved level 2 and a bounded preview of level 8 through the existing GameplayScreen. The preview call did not modify saved progression.
- Selected an awake buddy through EventSystem raycast (Buddy 3, Hit), then tapped plaza cell (0,0). The session reported MovesMade=1. Existing motion and selection remained functional.
- Final visual review: complete top controls and level plate, three distinct booster icons with badges, garden ground, readable open/sleeping faces, real 3D trays open toward the waiting area, no character-floor clipping in the sampled views. Disabled boosters intentionally dim via the existing HUD binding.
- Final engine-free gate passed source greps, Domain/Application/Gen type checks and 266/266 tests. Used the existing temporary config override pointing at SqadSort.SkuHeadlessTests.csproj; the checked-in gate config still names MyGame.SkuHeadlessTests.csproj.
- Framework codegen completed; subsequent automatic generation reported 0 written, 8 unchanged. The existing registrar synchronized all model prefab addresses. Prefab names use underscores to satisfy generated C# identifier constraints.
- EditMode attempted via Tools/pf/pf-tests --auto using a local HTTP transport adapter because npx is unavailable. It found no test class in Game.Tests.Editor; no EditMode pass is claimed. That folder currently contains category/capture helpers rather than game test fixtures.

Skipped: local PlayMode suite (repository rule; CI-owned and unverified).
Skipped: player build / device validation (CI-owned and unverified).
Skipped: keyed Slow gate leg (no KEYED_TEST configured).
Not run: an exhaustive level sweep. Only levels 2 and 8 were visually sampled.

Existing warnings include missing music_home audio, no AudioListener, absent Firebase service files and obsolete TMP API usage in unrelated views. Earlier codegen/name and bridge-probe errors were corrected; final runtime checks use the post-fix Console sequence boundary.
