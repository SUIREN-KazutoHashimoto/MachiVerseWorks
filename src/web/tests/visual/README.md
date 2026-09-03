# View Visual Regression

This directory stores the approved Golden Images used by the View browser E2E jobs.

The visual checks intentionally run on the same deterministic View fixtures that already perform structural and numeric assertions. This keeps image regression coverage paired with renderer diagnostics instead of treating screenshots as the only source of truth.

The canonical View Golden Images are captured at FHD (`1920x1080`) with device pixel ratio 1 so CI compares the same full-resolution presentation that is reviewed manually.

## Covered inspection points

- `view-physical-world.png`: View Phase 3 physical-world rendering (terrain, water, geographic features, and natural toponyms).
- `view-settlement-structure.png`: View Phase 4 settlement/structure rendering (settlements, districts, parcels, buildings, POIs, labels, and road signs).

## Failure artifacts

Each View E2E artifact contains, where applicable:

- `expected/<name>.png`
- `actual/<name>.png`
- `diff/<name>.png`
- `comparison/<name>.json`
- `diagnostics/<name>.json`
- browser HTML and Chrome logs

`diagnostics/<name>.json` includes the existing fixture's structural/rendering metrics and the captured canvas dimensions.

## Updating a Golden Image

Golden Images must never be refreshed just to make CI pass. First review the View change and the CI `actual`/`diff` artifacts. If the visual change is intentional and approved, run the relevant View E2E locally with:

```bash
MVW_UPDATE_VISUAL_GOLDEN=1 bash scripts/run-view-phase03-e2e.sh
MVW_UPDATE_VISUAL_GOLDEN=1 bash scripts/run-view-phase04-e2e.sh
```

Then review and commit the changed PNG files as normal source changes. CI never sets `MVW_UPDATE_VISUAL_GOLDEN`, so missing or changed baselines fail rather than silently updating themselves.

The comparator defaults to an 8/255 per-channel noise threshold and a maximum changed-pixel ratio of 0.1%. These can be overridden for investigation with `MVW_VISUAL_CHANNEL_THRESHOLD` and `MVW_VISUAL_MAX_CHANGED_RATIO`; repository baselines should keep the defaults unless a deliberate policy change is reviewed.
