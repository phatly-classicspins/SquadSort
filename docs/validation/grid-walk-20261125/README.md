# Grid movement validation — 2026-11-25

Characters exit through the source tube mouth, traverse authored waiting cells using orthogonal BFS, then enter through the destination mouth. Same-tube movement stays within its lane. Occupancy and destination legality retain existing Board rules.

Animation takes 0.12 seconds per edge. Input is gated during walking; completion effects wait for actual route duration. Rebuilding the board invalidates stale arrival callbacks.

Engine-free gate: 466 passed, 1 skipped (467 total); source checks and Domain/Application/Gen type checks passed. Five new route tests cover tube mouths, a grid hole, same-tube movement, cleared destination geometry and disconnected cells. Used existing local gate config override because checked-in config points to MyGame.

One observed Play session, level 8, real EventSystem tap: 9 edges, 1.08 seconds, arrived=true, walking=0, moves=1 after a repeated tap. Maximum distance from the expected route: 0.00001145 units. See observed.json and expected.json. Repeated paused Editor samples were deduplicated by elapsed time. The observation helper initially hit a Vector3 JSON serialization error; corrected the helper and repeated the fixture in the same session. Game source was not iterated in Play Mode. Play Mode was exited.

Visual inspection: corner.png shows the first turn on the waiting grid. Capture is 1440×2560 rather than the requested 1080×1920 capture preset; aspect ratio is unchanged.

Skipped: local EditMode suite (no Editor-side change).
Skipped: local PlayMode suite (project contract prohibits it).
Skipped: keyed slow tests (KEYED_TEST not configured).
CI-owned and unverified: player build and runtime suite.
