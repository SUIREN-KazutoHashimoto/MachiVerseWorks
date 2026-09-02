# View Phase 3 Physical World Rendering baseline

View Phase 3 records a reproducible browser-side rendering baseline for the authoritative Physical World observation published by Simulation Phase 29.

## Scenario

The CI scenario is `scripts/run-view-phase03-e2e.sh` and uses:

- deterministic Simulation seed `29027`;
- Protocol `2.17`;
- a broad `WorldEnvironmentSnapshot` subscription;
- the real browser `WorldView`, `WorldEnvironmentStore`, and `PhysicalWorldRenderer`;
- the same Terrain / SurfaceWater / GeographicFeature / NaturalToponym observations delivered by the Server.

The View does not classify terrain, infer a GeographicFeature, or generate a name in this scenario. Those semantics are supplied by Simulation and only mapped to presentation primitives.

## Baseline output

Each successful CI run uploads `.artifacts/view-phase03-e2e/rendering-baseline.txt` with these measurements:

- `frame_time_ms`: browser render-call duration for the captured frame;
- `draw_calls`: Three.js renderer draw calls for the captured frame;
- `geometries`: live Three.js geometry resource count;
- `textures`: live Three.js texture resource count;
- `geometry_bytes`: typed-array bytes owned by the Physical World geometry model;
- `terrain_triangles`: triangulated primary terrain triangle count;
- `water_samples`: delivered surface-water samples rendered as water presentation;
- `feature_segments`: delivered GeographicFeature line segments;
- `toponym_labels`: delivered natural toponyms represented by labels.

The artifact is the baseline record rather than a hard-coded performance gate because runner hardware and headless WebGL timing vary. Structural regressions are gated: the E2E requires non-empty terrain, GeographicFeature geometry, exact authoritative toponym count, positive draw/resource metrics, and absence of the legacy `GridHelper`.

## Multi-surface boundary

`terrain-geometry.ts` represents a terrain column as a collection of observed surfaces identified by role and layer. Phase 29 currently delivers the primary surface sample, while the boundary already accepts separately delivered `cavity-boundary`, `overhang`, and additional layers. The View never fabricates those missing surfaces; future Simulation/Gateway observations can populate them without replacing the rendering contract.
