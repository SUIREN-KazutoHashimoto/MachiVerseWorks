#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/phase20-e2e"
WEB_PORT=5177
SERVER_PORT=5084
WEB_PID=""
SERVER_PID=""
BROWSER_PID=""
FIFO_PATH="$ARTIFACT_DIR/admin.stdin"
mkdir -p "$ARTIFACT_DIR"; rm -f "$ARTIFACT_DIR"/*

cleanup() {
  exec 3>&- 2>/dev/null || true
  if [[ -n "$BROWSER_PID" ]] && kill -0 "$BROWSER_PID" 2>/dev/null; then kill "$BROWSER_PID" 2>/dev/null || true; wait "$BROWSER_PID" 2>/dev/null || true; fi
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
  if [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null; then kill "$WEB_PID" 2>/dev/null || true; wait "$WEB_PID" 2>/dev/null || true; fi
  rm -f "$FIFO_PATH"
}
trap cleanup EXIT INT TERM

find_chrome() { local candidate; for candidate in google-chrome-stable google-chrome chromium chromium-browser; do if command -v "$candidate" >/dev/null 2>&1; then command -v "$candidate"; return 0; fi; done; echo "E2E requires Chrome or Chromium in PATH." >&2; return 1; }
wait_http() { local url="$1"; for ((index = 0; index < 300; index += 1)); do if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then return 0; fi; sleep 0.1; done; echo "Timed out waiting for $url" >&2; return 1; }
wait_connection() { for ((index = 0; index < 300; index += 1)); do local count; count="$(curl --fail --silent --show-error "http://127.0.0.1:$SERVER_PORT/health" | python -c 'import json,sys; print(json.load(sys.stdin).get("connections",0))')"; if [[ "$count" -ge 1 ]]; then return 0; fi; sleep 0.1; done; echo "Timed out waiting for browser connection." >&2; return 1; }
CHROME="$(find_chrome)"

source "$ROOT_DIR/scripts/prepare-e2e.sh"
npm --prefix "$ROOT_DIR/src/web" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort >"$ARTIFACT_DIR/vite.log" 2>&1 & WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/phase20-e2e.html"

mkfifo "$FIFO_PATH"
env Server__Port="$SERVER_PORT" Server__SnapshotRate=10 Server__AllowedWebSocketOrigins="http://127.0.0.1:$WEB_PORT" Server__Console__Enabled=true Simulation__TickRate=30 Simulation__InitialAgentCount=0 dotnet run --project "$ROOT_DIR/src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj" --configuration Release --no-build <"$FIFO_PATH" >"$ARTIFACT_DIR/server.log" 2>&1 & SERVER_PID=$!
exec 3>"$FIFO_PATH"
wait_http "http://127.0.0.1:$SERVER_PORT/health"

BROWSER_URL="http://127.0.0.1:$WEB_PORT/tests/browser/phase20-e2e.html?server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" "$CHROME" "$BROWSER_URL" "$ARTIFACT_DIR/browser.html" "$ARTIFACT_DIR/chrome.log" >"$ARTIFACT_DIR/browser-runner.log" 2>&1 & BROWSER_PID=$!
wait_connection

printf 'simulation pause\n' >&3
sleep 0.3
curl --fail --silent --show-error "http://127.0.0.1:$SERVER_PORT/health" >"$ARTIFACT_DIR/paused-before.json"
sleep 0.3
curl --fail --silent --show-error "http://127.0.0.1:$SERVER_PORT/health" >"$ARTIFACT_DIR/paused-after.json"
python - "$ARTIFACT_DIR/paused-before.json" "$ARTIFACT_DIR/paused-after.json" <<'PY'
import json, sys
from pathlib import Path
before = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
after = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
if not before.get("paused") or not after.get("paused"):
    raise SystemExit("Simulation did not remain paused.")
if before.get("tick") != after.get("tick"):
    raise SystemExit(f"Automatic tick advanced while paused: {before.get('tick')} -> {after.get('tick')}")
PY

printf 'road node add -50 0 0\n' >&3
printf 'road node add 50 0 0\n' >&3
printf 'road segment add 1 2\n' >&3
printf 'road lane add 1 --direction=Forward --order=0 --width=3.5 --speed=10\n' >&3
printf 'simulation step 2\n' >&3
printf 'simulation resume\n' >&3

wait "$BROWSER_PID"; BROWSER_PID=""
grep -Fq 'data-status="passed"' "$ARTIFACT_DIR/browser.html" || { cat "$ARTIFACT_DIR/browser.html" >&2; cat "$ARTIFACT_DIR/browser-runner.log" >&2; cat "$ARTIFACT_DIR/server.log" >&2; exit 1; }
curl --fail --silent --show-error "http://127.0.0.1:$SERVER_PORT/health" >"$ARTIFACT_DIR/health.json"
python - "$ARTIFACT_DIR/health.json" <<'PY'
import json, sys
from pathlib import Path
health = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
if health.get("paused"):
    raise SystemExit("Simulation remained paused after resume.")
if health.get("roadSegments") != 1:
    raise SystemExit(f"Expected one RoadSegment, got {health.get('roadSegments')}")
print(json.dumps(health, ensure_ascii=False, indent=2))
PY

grep -Fq 'ok: Simulation paused.' "$ARTIFACT_DIR/server.log"
grep -Fq 'ok: Road node 1 created.' "$ARTIFACT_DIR/server.log"
grep -Fq 'ok: Road segment 1 created.' "$ARTIFACT_DIR/server.log"
grep -Fq 'ok: Lane 1 created.' "$ARTIFACT_DIR/server.log"
grep -Fq 'ok: Simulation resumed.' "$ARTIFACT_DIR/server.log"
cat "$ARTIFACT_DIR/browser.html"
echo "Phase 20 stdin -> AdminCommandQueue -> SimulationRuntime -> Browser E2E passed."
