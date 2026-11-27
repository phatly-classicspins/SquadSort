# Coin text centering

Inspected all 7 coin label prefabs from the resource typography audit. Their text alignment was Center, but their rectangles were offset vertically (0–7 reference pixels) and horizontally relative to the uncovered purple pill area. Authored Midline alignment, zero margin, vertical position at the pill center and horizontal center between the coin silhouette plus breathing space and the pill right inset. Same font/material and size 36 preserved. No runtime layout code added.

Editor measurements: ../coin-centering.json. Art fitting helper retains the correction. Observed Home, Shop and Gameplay at 1080×1920; comparison.jpg shows before and after crops. Final Console errors: zero. Engine-free gate: 481 passed, zero failed (Logs/gate/coin-centering-final.log).

Skipped local EditMode suite: no persistent Editor-side change.

Skipped automated PlayMode suite: prohibited locally; CI runtime gate unverified.

Skipped keyed Slow leg: override has no KEYED_TEST.
