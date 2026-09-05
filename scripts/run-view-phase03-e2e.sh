#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/view-phase03-e2e"
RUNTIME_ARTIFACT_DIR="$ARTIFACT_DIR/runtime-user-view"
USER_FACING_ARTIFACT_DIR="$ARTIFACT_DIR/user-facing"
GOLDEN_FILE="$ROOT_DIR/src/view/tests/visual/golden/view-physical-world.png"
RUNTIME_GOLDEN_FILE="$ROOT_DIR/src/view/tests/visual/golden/view-runtime-integrated.json"
WEB_PORT=5187
SERVER_PORT=5094
USER_FACING_PAUSE_TICK=60
WEB_PID=""
SERVER_PID=""
mkdir -p "$ARTIFACT_DIR"; rm -rf "$ARTIFACT_DIR"/*
mkdir -p "$RUNTIME_ARTIFACT_DIR" "$USER_FACING_ARTIFACT_DIR"

stop_server() {
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
    kill "$SERVER_PID" 2>/dev/null || true
    wait "$SERVER_PID" 2>/dev/null || true
  fi
  SERVER_PID=""
}

cleanup() {
  stop_server
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

if [[ -n "${MVW_VISUAL_BROWSER:-}" ]]; then
  CHROME="$MVW_VISUAL_BROWSER"
  [[ -x "$CHROME" ]] || { echo "MVW_VISUAL_BROWSER is not executable: $CHROME" >&2; exit 1; }
else
  CHROME="$(find_chrome)"
fi

if [[ "${MVW_E2E_PREPARED:-0}" != "1" ]]; then
  dotnet restore "$ROOT_DIR/MachiVerseWorks.slnx" 2>&1 | tee "$ARTIFACT_DIR/dotnet-restore.log"
  dotnet build "$ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore 2>&1 | tee "$ARTIFACT_DIR/dotnet-build.log"
  npm --prefix "$ROOT_DIR/src/view" ci
  npm --prefix "$ROOT_DIR/src/view" run lint
  npm --prefix "$ROOT_DIR/src/view" test
  npm --prefix "$ROOT_DIR/src/view" run build
fi
VITE_SERVER_URL="ws://127.0.0.1:$SERVER_PORT/ws" npm --prefix "$ROOT_DIR/src/view" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/view-phase03-e2e.html"

env Server__Port="$SERVER_PORT" Simulation__TickRate=30 Simulation__Seed=29027 Simulation__SpatialCellSize=4096 Simulation__DefaultWorldBootstrap__Enabled=false Server__SnapshotRate=2 Server__MaximumSubscriptionCellCount=524288 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" Simulation__InitialAgentCount=0 dotnet run --project "$ROOT_DIR/src/server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$ARTIFACT_DIR/server-renderer.log" 2>&1 & SERVER_PID=$!
wait_http "http://127.0.0.1:$SERVER_PORT/health"

URL="http://127.0.0.1:$WEB_PORT/tests/browser/view-phase03-e2e.html?server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$URL" "$ARTIFACT_DIR/browser.html" "$ARTIFACT_DIR/chrome.log"
grep -Fq 'data-status="passed"' "$ARTIFACT_DIR/browser.html" || { cat "$ARTIFACT_DIR/browser.html" >&2; cat "$ARTIFACT_DIR/server-renderer.log" >&2; exit 1; }

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

# User-visible runtime contract: restart the real Server with its normal default-world bootstrap.
# This path goes through Application -> MachiVerseConnection -> Server/Simulation and does not inject View fixtures.
stop_server
env Server__Port="$SERVER_PORT" Simulation__TickRate=30 Simulation__Seed=29027 Simulation__SpatialCellSize=4096 Server__SnapshotRate=2 Server__MaximumSubscriptionCellCount=524288 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" dotnet run --project "$ROOT_DIR/src/server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$RUNTIME_ARTIFACT_DIR/server-runtime.log" 2>&1 & SERVER_PID=$!
wait_http "http://127.0.0.1:$SERVER_PORT/health"

RUNTIME_URL="http://127.0.0.1:$WEB_PORT/?visualTest=runtime"
node "$ROOT_DIR/scripts/run-headless-runtime-visual-e2e.mjs" "$CHROME" "$RUNTIME_URL" "$RUNTIME_ARTIFACT_DIR"
node "$ROOT_DIR/scripts/check-runtime-visual-golden.mjs" "$RUNTIME_ARTIFACT_DIR" "$RUNTIME_GOLDEN_FILE"

# VQ-0 user-facing Golden suite gets its own deterministic runtime. Bootstrap completes first,
# then the Server advances synchronously to one exact simulation tick and remains paused while
# every camera composition is captured. This keeps Vehicle/Train positions identical across scenes.
stop_server
env Server__Port="$SERVER_PORT" Simulation__TickRate=30 Simulation__Seed=29027 Simulation__SpatialCellSize=4096 Simulation__PauseAtTick="$USER_FACING_PAUSE_TICK" Server__SnapshotRate=2 Server__MaximumSubscriptionCellCount=524288 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" dotnet run --project "$ROOT_DIR/src/server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$USER_FACING_ARTIFACT_DIR/server-runtime.log" 2>&1 & SERVER_PID=$!
wait_http "http://127.0.0.1:$SERVER_PORT/health"

# VQ-0 user-facing Golden suite. This is deliberately separate from the renderer fixture
# and runtime structural Golden: it captures stable camera compositions for visual review.
USER_FACING_URL="http://127.0.0.1:$WEB_PORT/?visualTest=user-facing"
node "$ROOT_DIR/scripts/run-headless-user-facing-visual-e2e.mjs" "$CHROME" "$USER_FACING_URL" "$USER_FACING_ARTIFACT_DIR"
bash "$ROOT_DIR/scripts/check-user-facing-visual-goldens.sh" "$ROOT_DIR" "$USER_FACING_ARTIFACT_DIR"

cat "$ARTIFACT_DIR/browser.html"
cat "$RUNTIME_ARTIFACT_DIR/summary.json"
cat "$USER_FACING_ARTIFACT_DIR/summary.json"
echo "View Phase 3 Technical Golden + runtime structural Golden + VQ-0 user-facing Golden passed."
