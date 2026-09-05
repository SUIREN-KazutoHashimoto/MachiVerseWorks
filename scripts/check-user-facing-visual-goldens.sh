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

mapfile -t SCENES < <(python - "$MANIFEST" <<'PY'
import json
import re
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
if manifest.get("schemaVersion") != 1:
    raise SystemExit("VQ-0 user-facing manifest schemaVersion must be 1.")
legacy = manifest.get("legacyReference", {})
legacy_commit = legacy.get("commit", "")
if not re.fullmatch(r"[0-9a-f]{40}", legacy_commit):
    raise SystemExit("VQ-0 Legacy reference commit must be a pinned full SHA.")
if not legacy.get("release"):
    raise SystemExit("VQ-0 Legacy reference release must be pinned.")
scenes = [scene.get("id") for scene in manifest.get("scenes", [])]
if len(scenes) != 5 or any(not isinstance(scene, str) or not scene for scene in scenes) or len(set(scenes)) != len(scenes):
    raise SystemExit(f"VQ-0 manifest must define exactly five unique scene ids: {scenes}.")
runtime = manifest.get("runtime", {})
for key in ("simulationSeed", "tickRate", "pauseAtTick", "snapshotRate"):
    if not isinstance(runtime.get(key), int) or runtime[key] <= 0:
        raise SystemExit(f"VQ-0 runtime.{key} must be a positive integer.")
if runtime.get("defaultWorldBootstrap") is not True:
    raise SystemExit("VQ-0 must capture the normal default-world bootstrap runtime.")
capture = manifest.get("capture", {})
viewport = capture.get("viewport", {})
if viewport != {"width": 1920, "height": 1080, "devicePixelRatio": 1}:
    raise SystemExit(f"VQ-0 viewport contract mismatch: {viewport}.")
if capture.get("renderer") != "SwiftShader" or capture.get("fontFamily") != "Noto Sans CJK JP" or not capture.get("fontPackageVersion"):
    raise SystemExit("VQ-0 fixed renderer/font contract is missing.")
if not re.fullmatch(r"Chrome for Testing \d+\.\d+\.\d+\.\d+", capture.get("browser", "")):
    raise SystemExit("VQ-0 browser must pin an exact Chrome for Testing version.")
for scene in scenes:
    print(scene)
PY
)

if [[ "${#SCENES[@]}" -ne 5 ]]; then
  echo "Failed to load the five VQ-0 scenes from $MANIFEST" >&2
  exit 1
fi

# The simulation is paused at the manifest-defined exact tick before capture. Keep a small
# rendering-only tolerance for browser rasterization while treating scene state as deterministic.
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

echo "User-facing VQ-0 Golden comparison passed for ${#SCENES[@]} manifest-defined scenes."
