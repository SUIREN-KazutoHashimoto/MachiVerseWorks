#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: check-user-facing-visual-goldens.sh <root-dir> <artifact-dir>" >&2
  exit 2
fi

ROOT_DIR="$1"
ARTIFACT_DIR="$2"
GOLDEN_DIR="$ROOT_DIR/src/view/tests/visual/user-facing-golden"
MANIFEST="$ROOT_DIR/src/view/tests/visual/user-facing/manifest.json"
SCENES=(world-overview dense-urban road-interchange railway street-activity)
LEGACY_REFERENCE_COMMIT="5715ca26d1a7525d89a93c35540f926a720e5386"
PAUSE_AT_TICK=60

python - "$MANIFEST" "$LEGACY_REFERENCE_COMMIT" "$PAUSE_AT_TICK" <<'PY'
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
expected_commit = sys.argv[2]
expected_pause_tick = int(sys.argv[3])
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
expected_scenes = ["world-overview", "dense-urban", "road-interchange", "railway", "street-activity"]
actual_scenes = [scene.get("id") for scene in manifest.get("scenes", [])]
if manifest.get("schemaVersion") != 1:
    raise SystemExit("VQ-0 user-facing manifest schemaVersion must be 1.")
if manifest.get("legacyReference", {}).get("commit") != expected_commit:
    raise SystemExit("VQ-0 Legacy reference commit is not pinned to the reviewed Legacy baseline.")
if actual_scenes != expected_scenes:
    raise SystemExit(f"VQ-0 scene contract mismatch: expected {expected_scenes}, actual {actual_scenes}.")
runtime = manifest.get("runtime", {})
if runtime.get("pauseAtTick") != expected_pause_tick:
    raise SystemExit(f"VQ-0 fixed simulation tick mismatch: expected {expected_pause_tick}, actual {runtime.get('pauseAtTick')}.")
capture = manifest.get("capture", {})
viewport = capture.get("viewport", {})
if viewport != {"width": 1920, "height": 1080, "devicePixelRatio": 1}:
    raise SystemExit(f"VQ-0 viewport contract mismatch: {viewport}.")
if capture.get("renderer") != "SwiftShader" or capture.get("fontFamily") != "Noto Sans CJK JP":
    raise SystemExit("VQ-0 fixed renderer/font contract is missing.")
PY

# The simulation is paused at one exact tick before capture. Keep a small rendering-only
# tolerance for browser rasterization while treating the scene state itself as deterministic.
export MVW_VISUAL_CHANNEL_THRESHOLD="${MVW_USER_FACING_VISUAL_CHANNEL_THRESHOLD:-8}"
export MVW_VISUAL_MAX_CHANGED_RATIO="${MVW_USER_FACING_VISUAL_MAX_CHANGED_RATIO:-0.005}"

if [[ "${MVW_UPDATE_USER_FACING_GOLDEN:-0}" == "1" ]]; then
  export MVW_UPDATE_VISUAL_GOLDEN=1
fi

for scene in "${SCENES[@]}"; do
  bash "$ROOT_DIR/scripts/check-visual-regression.sh" \
    "$ROOT_DIR" \
    "$ARTIFACT_DIR" \
    "$scene" \
    "$GOLDEN_DIR/$scene.png"
done

echo "User-facing VQ-0 Golden comparison passed for ${#SCENES[@]} scenes."
