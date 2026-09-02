#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/view-phase04-live-e2e"
WEB_PORT=5189
SERVER_PORT=5094
WEB_PID=""
SERVER_PID=""
mkdir -p "$ARTIFACT_DIR"; rm -f "$ARTIFACT_DIR"/*

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

CHROME="$(find_chrome)"
dotnet restore "$ROOT_DIR/MachiVerseWorks.slnx" 2>&1 | tee "$ARTIFACT_DIR/dotnet-restore.log"
dotnet build "$ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore 2>&1 | tee "$ARTIFACT_DIR/dotnet-build.log"
npm --prefix "$ROOT_DIR/src/web" ci
npm --prefix "$ROOT_DIR/src/web" run build
npm --prefix "$ROOT_DIR/src/web" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/view-phase04-live-e2e.html"

env Server__Port="$SERVER_PORT" Simulation__TickRate=30 Simulation__Seed=30034 Simulation__SpatialCellSize=4096 Server__SnapshotRate=10 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" Simulation__InitialAgentCount=0 Simulation__RegionalGenerationFixture=true dotnet run --project "$ROOT_DIR/src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$ARTIFACT_DIR/server.log" 2>&1 & SERVER_PID=$!
wait_http "http://127.0.0.1:$SERVER_PORT/health"

OUTPUT="$ARTIFACT_DIR/browser.html"
CHROME_LOG="$ARTIFACT_DIR/chrome.log"
URL="http://127.0.0.1:$WEB_PORT/tests/browser/view-phase04-live-e2e.html?server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$URL" "$OUTPUT" "$CHROME_LOG"
grep -Fq 'data-status="passed"' "$OUTPUT" || { cat "$OUTPUT" >&2; cat "$ARTIFACT_DIR/server.log" >&2; cat "$CHROME_LOG" >&2; exit 1; }
cat "$OUTPUT"
echo "View Phase 4 live Simulation -> Gateway -> Web rendering E2E passed."
