#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 4 ]]; then
  echo "Usage: check-visual-regression.sh <root-dir> <artifact-dir> <inspection-name> <golden.png>" >&2
  exit 2
fi

ROOT_DIR="$1"
ARTIFACT_DIR="$2"
INSPECTION_NAME="$3"
GOLDEN_FILE="$4"
ACTUAL_FILE="$ARTIFACT_DIR/actual/$INSPECTION_NAME.png"
EXPECTED_DIR="$ARTIFACT_DIR/expected"
DIFF_DIR="$ARTIFACT_DIR/diff"
REPORT_DIR="$ARTIFACT_DIR/comparison"

mkdir -p "$EXPECTED_DIR" "$DIFF_DIR" "$REPORT_DIR"

if [[ ! -f "$ACTUAL_FILE" ]]; then
  echo "Visual actual screenshot was not produced: $ACTUAL_FILE" >&2
  exit 1
fi

if [[ "${MVW_UPDATE_VISUAL_GOLDEN:-0}" == "1" ]]; then
  mkdir -p "$(dirname "$GOLDEN_FILE")"
  cp "$ACTUAL_FILE" "$GOLDEN_FILE"
  echo "Updated visual Golden Image: $GOLDEN_FILE"
fi

if [[ ! -f "$GOLDEN_FILE" ]]; then
  echo "Missing visual Golden Image: $GOLDEN_FILE" >&2
  echo "The actual screenshot remains in $ACTUAL_FILE. Review it before setting MVW_UPDATE_VISUAL_GOLDEN=1; never update a Golden Image only to make CI pass." >&2
  exit 1
fi

cp "$GOLDEN_FILE" "$EXPECTED_DIR/$INSPECTION_NAME.png"
node "$ROOT_DIR/scripts/compare-visual-png.mjs" \
  "$EXPECTED_DIR/$INSPECTION_NAME.png" \
  "$ACTUAL_FILE" \
  "$DIFF_DIR/$INSPECTION_NAME.png" \
  "$REPORT_DIR/$INSPECTION_NAME.json"
