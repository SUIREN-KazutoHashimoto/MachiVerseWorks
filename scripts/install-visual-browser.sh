#!/usr/bin/env bash
set -euo pipefail

CHROME_VERSION="${MVW_VISUAL_BROWSER_VERSION:-152.0.7977.75}"
INSTALL_ROOT="${1:-${RUNNER_TEMP:-/tmp}/machiverseworks-visual-browser}"
ARCHIVE="$INSTALL_ROOT/chrome-linux64.zip"
BROWSER_DIR="$INSTALL_ROOT/chrome-linux64"
BROWSER="$BROWSER_DIR/chrome"
DOWNLOAD_URL="https://storage.googleapis.com/chrome-for-testing-public/$CHROME_VERSION/linux64/chrome-linux64.zip"

mkdir -p "$INSTALL_ROOT"

if [[ ! -x "$BROWSER" ]] || ! "$BROWSER" --version 2>/dev/null | grep -Fq "$CHROME_VERSION"; then
  rm -rf "$BROWSER_DIR" "$ARCHIVE"
  curl --fail --location --retry 3 --output "$ARCHIVE" "$DOWNLOAD_URL"
  unzip -q "$ARCHIVE" -d "$INSTALL_ROOT"
  rm -f "$ARCHIVE"
fi

if [[ ! -x "$BROWSER" ]]; then
  echo "Pinned Chrome for Testing was not installed: $BROWSER" >&2
  exit 1
fi

ACTUAL_VERSION="$($BROWSER --version)"
if [[ "$ACTUAL_VERSION" != *"$CHROME_VERSION"* ]]; then
  echo "Pinned Chrome version mismatch: expected $CHROME_VERSION, actual $ACTUAL_VERSION" >&2
  exit 1
fi

printf '%s\n' "$BROWSER"
