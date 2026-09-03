#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/view-phase04-e2e"
BASELINE_FILE="$ROOT_DIR/docs/development/baselines/view-phase04-rendering-baseline.json"
GOLDEN_FILE="$ROOT_DIR/src/web/tests/visual/golden/view-settlement-structure.png"
WEB_PORT=5188
WEB_PID=""
mkdir -p "$ARTIFACT_DIR"; rm -rf "$ARTIFACT_DIR"/*

cleanup() {
  if [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null; then kill "$WEB_PID" 2>/dev/null || true; wait "$WEB_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM

find_chrome() {
  local candidate
  for candidate in google-chrome-stable google-chrome chromium chromium-browser; do
    if command -v "$candidate" >/dev/null 2>&1; then command -v "$candidate"; return 0; fi
  done
  echo "View Phase 4 browser E2E requires Chrome or Chromium in PATH." >&2
  return 1
}

wait_http() {
  local url="$1"
  for ((index = 0; index < 300; index += 1)); do
    if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then return 0; fi
    sleep 0.1
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

CHROME="$(find_chrome)"
if [[ "${MVW_E2E_PREPARED:-0}" != "1" ]]; then
  npm --prefix "$ROOT_DIR/src/web" ci
  npm --prefix "$ROOT_DIR/src/web" run lint
  npm --prefix "$ROOT_DIR/src/web" run typecheck
  npm --prefix "$ROOT_DIR/src/web" test
  npm --prefix "$ROOT_DIR/src/web" run build
fi
npm --prefix "$ROOT_DIR/src/web" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!

BASELINE_URL="http://127.0.0.1:$WEB_PORT/tests/browser/view-phase04-e2e.html"
EVOLUTION_URL="http://127.0.0.1:$WEB_PORT/tests/browser/view-phase04-evolution-e2e.html"
wait_http "$BASELINE_URL"
wait_http "$EVOLUTION_URL"

node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$BASELINE_URL" "$ARTIFACT_DIR/browser.html" "$ARTIFACT_DIR/chrome.log"
grep -Fq 'data-status="passed"' "$ARTIFACT_DIR/browser.html" || { cat "$ARTIFACT_DIR/browser.html" >&2; cat "$ARTIFACT_DIR/chrome.log" >&2; exit 1; }

node "$ROOT_DIR/scripts/run-headless-visual-e2e.mjs" "$CHROME" "$BASELINE_URL" "$ARTIFACT_DIR" "view-settlement-structure"
bash "$ROOT_DIR/scripts/check-visual-regression.sh" "$ROOT_DIR" "$ARTIFACT_DIR" "view-settlement-structure" "$GOLDEN_FILE"

node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$EVOLUTION_URL" "$ARTIFACT_DIR/evolution-browser.html" "$ARTIFACT_DIR/evolution-chrome.log"
grep -Fq 'data-status="passed"' "$ARTIFACT_DIR/evolution-browser.html" || { cat "$ARTIFACT_DIR/evolution-browser.html" >&2; cat "$ARTIFACT_DIR/evolution-chrome.log" >&2; exit 1; }

extract_metric() {
  local name="$1"
  grep -o "data-${name}=\"[^\"]*\"" "$ARTIFACT_DIR/browser.html" | head -n 1 | cut -d'"' -f2
}

extract_evolution_metric() {
  local name="$1"
  grep -o "data-${name}=\"[^\"]*\"" "$ARTIFACT_DIR/evolution-browser.html" | head -n 1 | cut -d'"' -f2
}

{
  echo "draw_calls=$(extract_metric draw-calls)"
  echo "geometries=$(extract_metric geometries)"
  echo "settlements=$(extract_metric settlements)"
  echo "parcels=$(extract_metric parcels)"
  echo "buildings=$(extract_metric buildings)"
  echo "labels=$(extract_metric labels)"
  echo "road_signs=$(extract_metric road-signs)"
  echo "evolution_current_year=$(extract_evolution_metric current-year)"
  echo "evolution_settlements=$(extract_evolution_metric settlements)"
  echo "evolution_draw_calls=$(extract_evolution_metric draw-calls)"
} | tee "$ARTIFACT_DIR/rendering-baseline.txt"

node "$ROOT_DIR/scripts/check-view-phase04-rendering-baseline.mjs" "$BASELINE_FILE" "$ARTIFACT_DIR/rendering-baseline.txt"

cat "$ARTIFACT_DIR/browser.html"
cat "$ARTIFACT_DIR/evolution-browser.html"
echo "View Phase 4 Settlement & Structure Rendering browser E2E + visual regression passed (static + Phase31 evolution + checked-in baseline)."
