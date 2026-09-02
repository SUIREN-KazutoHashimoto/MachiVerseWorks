#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/phase29-e2e"
WEB_PORT=5186
SERVER_PORT=5093
WEB_PID=""
SERVER_PID=""
mkdir -p "$ARTIFACT_DIR"; rm -f "$ARTIFACT_DIR"/*

cleanup() {
  stop_server
  if [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null; then kill "$WEB_PID" 2>/dev/null || true; wait "$WEB_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM

stop_server() {
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
  SERVER_PID=""
}

find_chrome() {
  local candidate
  for candidate in google-chrome-stable google-chrome chromium chromium-browser; do
    if command -v "$candidate" >/dev/null 2>&1; then command -v "$candidate"; return 0; fi
  done
  echo "E2E requires Chrome or Chromium in PATH." >&2
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

start_server() {
  local suffix="$1"
  env Server__Port="$SERVER_PORT" Simulation__TickRate=30 Simulation__Seed=29027 Simulation__SpatialCellSize=4096 Server__SnapshotRate=2 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" Simulation__InitialAgentCount=0 dotnet run --project "$ROOT_DIR/src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$ARTIFACT_DIR/server-$suffix.log" 2>&1 & SERVER_PID=$!
  wait_http "http://127.0.0.1:$SERVER_PORT/health"
}

capture_snapshot() {
  local suffix="$1"
  local output="$ARTIFACT_DIR/browser-$suffix.html"
  local chrome_log="$ARTIFACT_DIR/chrome-$suffix.log"
  local url="http://127.0.0.1:$WEB_PORT/tests/browser/phase29-e2e.html?server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
  node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$url" "$output" "$chrome_log"
  grep -Fq 'data-status="passed"' "$output" || { cat "$output" >&2; cat "$ARTIFACT_DIR/server-$suffix.log" >&2; exit 1; }
  local hash
  hash="$(grep -o 'data-hash="[^"]*"' "$output" | head -n 1 | cut -d'"' -f2)"
  if [[ -z "$hash" ]]; then cat "$output" >&2; echo "Phase 29 E2E did not emit a deterministic hash." >&2; exit 1; fi
  printf '%s' "$hash"
}

CHROME="$(find_chrome)"
dotnet restore "$ROOT_DIR/MachiVerseWorks.slnx" 2>&1 | tee "$ARTIFACT_DIR/dotnet-restore.log"
dotnet build "$ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore 2>&1 | tee "$ARTIFACT_DIR/dotnet-build.log"
npm --prefix "$ROOT_DIR/src/web" ci
npm --prefix "$ROOT_DIR/src/web" run build
npm --prefix "$ROOT_DIR/src/web" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/phase29-e2e.html"

start_server before-restart
FIRST_HASH="$(capture_snapshot before-restart)"
stop_server
start_server after-restart
SECOND_HASH="$(capture_snapshot after-restart)"

if [[ "$FIRST_HASH" != "$SECOND_HASH" ]]; then
  echo "Phase 29 environment digest changed across server restart: $FIRST_HASH != $SECOND_HASH" >&2
  exit 1
fi

cat "$ARTIFACT_DIR/browser-before-restart.html"
cat "$ARTIFACT_DIR/browser-after-restart.html"
echo "Phase 29 Global Environment / Detailed Terrain restart reproducibility E2E passed: $FIRST_HASH"
