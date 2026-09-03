#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/view-phase03-e2e"
GOLDEN_FILE="$ROOT_DIR/src/web/tests/visual/golden/view-physical-world.png"
WEB_PORT=5187
SERVER_PORT=5094
WEB_PID=""
SERVER_PID=""
mkdir -p "$ARTIFACT_DIR"; rm -rf "$ARTIFACT_DIR"/*

cleanup() {
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
  if [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null; then kill "$WEB_PID" 2>/dev/null || true; wait "$WEB_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM

find_chrome() {
  local candidate
  for candidate in google-chrome-stable google-chrome chromium chromium-browser; do
    if command -v "$candidate" >/dev/null 2>&1; then command -v "$candidate"; return 0; fi
  done
  echo "View Phase 3 E2E requires Chrome or Chromium in PATH." >&2
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
  dotnet restore "$ROOT_DIR/MachiVerseWorks.slnx" 2>&1 | tee "$ARTIFACT_DIR/dotnet-restore.log"
  dotnet build "$ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore 2>&1 | tee "$ARTIFACT_DIR/dotnet-build.log"
  npm --prefix "$ROOT_DIR/src/web" ci
  npm --prefix "$ROOT_DIR/src/web" run lint
  npm --prefix "$ROOT_DIR/src/web" test
  npm --prefix "$ROOT_DIR/src/web" run build
fi
npm --prefix "$ROOT_DIR/src/web" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/view-phase03-e2e.html"

env Server__Port="$SERVER_PORT" Simulation__TickRate=30 Simulation__Seed=29027 Simulation__SpatialCellSize=4096 Server__SnapshotRate=2 Server__MaximumSubscriptionCellCount=524288 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" Simulation__InitialAgentCount=0 dotnet run --project "$ROOT_DIR/src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$ARTIFACT_DIR/server.log" 2>&1 & SERVER_PID=$!
wait_http "http://127.0.0.1:$SERVER_PORT/health"

URL="http://127.0.0.1:$WEB_PORT/tests/browser/view-phase03-e2e.html?server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$URL" "$ARTIFACT_DIR/browser.html" "$ARTIFACT_DIR/chrome.log"
grep -Fq 'data-status="passed"' "$ARTIFACT_DIR/browser.html" || { cat "$ARTIFACT_DIR/browser.html" >&2; cat "$ARTIFACT_DIR/server.log" >&2; exit 1; }

node "$ROOT_DIR/scripts/run-headless-visual-e2e.mjs" "$CHROME" "$URL" "$ARTIFACT_DIR" "view-physical-world"
bash "$ROOT_DIR/scripts/check-visual-regression.sh" "$ROOT_DIR" "$ARTIFACT_DIR" "view-physical-world" "$GOLDEN_FILE"

extract_metric() {
  local name="$1"
  grep -o "data-${name}=\"[^\"]*\"" "$ARTIFACT_DIR/browser.html" | head -n 1 | cut -d'"' -f2
}

{
  echo "frame_time_ms=$(extract_metric frame-time-ms)"
  echo "draw_calls=$(extract_metric draw-calls)"
  echo "geometries=$(extract_metric geometries)"
  echo "textures=$(extract_metric textures)"
  echo "geometry_bytes=$(extract_metric geometry-bytes)"
  echo "terrain_triangles=$(extract_metric terrain-triangles)"
  echo "water_samples=$(extract_metric water-samples)"
  echo "feature_segments=$(extract_metric feature-segments)"
  echo "toponym_labels=$(extract_metric toponym-labels)"
} | tee "$ARTIFACT_DIR/rendering-baseline.txt"

cat "$ARTIFACT_DIR/browser.html"
echo "View Phase 3 Physical World Rendering E2E + visual regression passed."
