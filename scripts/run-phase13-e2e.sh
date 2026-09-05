#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/phase13-e2e"
WEB_PORT=5176
SERVER_PORT=5083
WEB_PID=""
SERVER_PID=""
mkdir -p "$ARTIFACT_DIR"; rm -f "$ARTIFACT_DIR"/*

cleanup() {
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
  if [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null; then kill "$WEB_PID" 2>/dev/null || true; wait "$WEB_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM
find_chrome() { local candidate; for candidate in google-chrome-stable google-chrome chromium chromium-browser; do if command -v "$candidate" >/dev/null 2>&1; then command -v "$candidate"; return 0; fi; done; echo "E2E requires Chrome or Chromium in PATH." >&2; return 1; }
wait_http() { local url="$1"; for ((index = 0; index < 200; index += 1)); do if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then return 0; fi; sleep 0.1; done; echo "Timed out waiting for $url" >&2; return 1; }
CHROME="$(find_chrome)"

source "$ROOT_DIR/scripts/prepare-e2e.sh"
npm --prefix "$ROOT_DIR/src/view" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/phase13-e2e.html"

env Server__Port="$SERVER_PORT" Server__SnapshotRate=20 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" Simulation__TickRate=30 Simulation__SpatialCellSize=64 Simulation__InitialAgentCount=0 Simulation__RoadTrafficFixture=true dotnet run --project "$ROOT_DIR/src/server/MachiVerseWorks.Server.csproj" --configuration Release --no-build >"$ARTIFACT_DIR/server.log" 2>&1 & SERVER_PID=$!
wait_http "http://127.0.0.1:$SERVER_PORT/health"
BROWSER_URL="http://127.0.0.1:$WEB_PORT/tests/browser/phase13-e2e.html?server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$BROWSER_URL" "$ARTIFACT_DIR/browser.html" "$ARTIFACT_DIR/chrome.log"
grep -Fq 'data-status="passed"' "$ARTIFACT_DIR/browser.html" || { cat "$ARTIFACT_DIR/browser.html" >&2; exit 1; }
curl --fail --silent --show-error "http://127.0.0.1:$SERVER_PORT/health" >"$ARTIFACT_DIR/health.json"

echo "Phase 13 Road Traffic Server -> Browser E2E passed."
