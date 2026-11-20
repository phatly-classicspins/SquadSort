# Models

Actual FBX meshes; no character or container is a billboard.

| Asset | Triangles | W × H × D (m) |
|---|---:|---|
| character-blue | 4856 | 1.025 × 0.846 × 0.817 |
| character-brown | 4088 | 1.025 × 1.026 × 0.788 |
| character-cyan | 4040 | 1.025 × 1.050 × 0.788 |
| character-green | 4808 | 1.025 × 1.002 × 0.788 |
| character-hidden | 2408 | 1.025 × 0.846 × 0.725 |
| character-orange | 4196 | 1.025 × 1.078 × 0.788 |
| character-pink | 4640 | 1.025 × 0.963 × 0.788 |
| character-purple | 5972 | 1.025 × 1.149 × 0.788 |
| character-red | 3668 | 1.025 × 0.846 × 0.788 |
| character-yellow | 3908 | 1.025 × 1.059 × 0.788 |
| queue-four | 2208 | 1.280 × 0.260 × 4.600 |
| waiting-board | 3426 | 4.600 × 0.260 × 2.300 |
| waiting-cell | 188 | 1.150 × 0.040 × 1.150 |
| waiting-corner-inner | 372 | 0.349 × 0.260 × 0.349 |
| waiting-corner-outer | 372 | 0.229 × 0.260 × 0.229 |
| waiting-edge | 300 | 1.150 × 0.260 × 0.120 |

All 16 passed the model-gen geometry checks and FBX re-import checks.

The purple base outline score is 0.892. Queue and waiting sample scores are 0.606 and 0.345; they did not reach the 0.88 automatic outline target. The generated reference elevations contain perspective; the container meshes were also inspected visually. These scores are not Unity runtime tests.

The waiting board uses the visually cleaner first round rather than a later round with triangulated shading artifacts. Its standing surface was aligned to the queue at 0.04 m and rechecked.

Blueprints were approved by the user. Exported meshes remain pending visual review.
