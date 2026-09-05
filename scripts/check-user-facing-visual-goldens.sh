#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: check-user-facing-visual-goldens.sh <root-dir> <artifact-dir>" >&2
  exit 2
fi

ROOT_DIR="$1"
ARTIFACT_DIR="$2"
GOLDEN_DIR="$ROOT_DIR/src/view/tests/visual/user-facing-golden"
SCENES=(world-overview dense-urban road-interchange railway street-activity)

# Runtime entities can move by a small number of pixels between otherwise equivalent
# captures. Keep the channel threshold strict while allowing up to 0.5% changed pixels.
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
