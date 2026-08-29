#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/phase6-e2e"
WEB_PORT=5173
SERVER_PORT=5080
WEB_PID=""
SERVER_PID=""

mkdir -p "$ARTIFACT_DIR"
rm -f "$ARTIFACT_DIR"/*

cleanup_server() {
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
    kill "$SERVER_PID" 2>/dev/null || true
    wait "$SERVER_PID" 2>/dev/null || true
  fi
  SERVER_PID=""
}

cleanup() {
  cleanup_server
  if [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null; then
    kill "$WEB_PID" 2>/dev/null || true
    wait "$WEB_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM

find_chrome() {
  local candidate
  for candidate in google-chrome-stable google-chrome chromium chromium-browser; do
    if command -v "$candidate" >/dev/null 2>&1; then
      command -v "$candidate"
      return 0
    fi
  done
  echo "E2E requires Chrome or Chromium in PATH." >&2
  return 1
}

wait_http() {
  local url="$1"
  local attempts="${2:-100}"
  local index
  for ((index = 0; index < attempts; index += 1)); do
    if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.1
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

CHROME="$(find_chrome)"

echo "Preparing .NET and Web Client dependencies..."
dotnet restore "$ROOT_DIR/MachiVerseWorks.slnx"
dotnet build "$ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore
npm --prefix "$ROOT_DIR/src/web" ci
npm --prefix "$ROOT_DIR/src/web" run build

npm --prefix "$ROOT_DIR/src/web" run dev -- --host 127.0.0.1 --port "$WEB_PORT" --strictPort \
  >"$ARTIFACT_DIR/vite.log" 2>&1 &
WEB_PID=$!
wait_http "http://127.0.0.1:$WEB_PORT/tests/browser/e2e.html"

run_scenario() {
  local agents="$1"
  local mode="$2"
  local name="${agents}-${mode}"
  local server_log="$ARTIFACT_DIR/server-$name.log"
  local browser_dom="$ARTIFACT_DIR/browser-$name.html"
  local metrics_json="$ARTIFACT_DIR/server-metrics-$name.json"
  local browser_url="http://127.0.0.1:$WEB_PORT/tests/browser/e2e.html?agents=$agents&mode=$mode&server=ws%3A%2F%2F127.0.0.1%3A$SERVER_PORT%2Fws"
  local spawn_min_x=-500
  local spawn_min_y=-500
  local spawn_min_z=0
  local spawn_max_x=500
  local spawn_max_y=500
  local spawn_max_z=0

  if [[ "$mode" == "altitude" ]]; then
    spawn_min_x=0
    spawn_min_y=0
    spawn_min_z=10
    spawn_max_x=0
    spawn_max_y=0
    spawn_max_z=100
  fi

  echo "Running E2E scenario: agents=$agents mode=$mode"
  cleanup_server

  env \
    Server__Port="$SERVER_PORT" \
    Server__SnapshotRate=5 \
    Simulation__TickRate=10 \
    Simulation__InitialAgentCount="$agents" \
    Simulation__SpawnArea__MinX="$spawn_min_x" \
    Simulation__SpawnArea__MinY="$spawn_min_y" \
    Simulation__SpawnArea__MinZ="$spawn_min_z" \
    Simulation__SpawnArea__MaxX="$spawn_max_x" \
    Simulation__SpawnArea__MaxY="$spawn_max_y" \
    Simulation__SpawnArea__MaxZ="$spawn_max_z" \
    dotnet run \
      --project "$ROOT_DIR/src/MachiVerseWorks.Server/MachiVerseWorks.Server.csproj" \
      --configuration Release --no-build \
      >"$server_log" 2>&1 &
  SERVER_PID=$!

  wait_http "http://127.0.0.1:$SERVER_PORT/health" 200

  node "$ROOT_DIR/scripts/run-headless-browser-e2e.mjs" \
    "$CHROME" \
    "$browser_url" \
    "$browser_dom" \
    "$ARTIFACT_DIR/chrome.log"

  if ! grep -Fq 'data-status="passed"' "$browser_dom"; then
    echo "Browser E2E scenario failed: $name" >&2
    cat "$browser_dom" >&2
    return 1
  fi

  curl --fail --silent --show-error \
    "http://127.0.0.1:$SERVER_PORT/metrics/e2e" \
    >"$metrics_json"

  python - "$metrics_json" "$agents" "$mode" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
agent_total = int(sys.argv[2])
mode = sys.argv[3]
data = json.loads(path.read_text(encoding="utf-8"))

if data["totalSnapshotDeliveries"] <= 0:
    raise SystemExit("No snapshot delivery was recorded")
if data["totalMessages"] <= 0 or data["totalBytes"] <= 0:
    raise SystemExit("No protocol traffic was recorded")
if data["totalEncodeTimeMs"] < 0 or data["totalSendTimeMs"] < 0:
    raise SystemExit("Invalid server timing metrics")

if mode == "altitude":
    if data["lastAgentCount"] != agent_total:
        raise SystemExit(
            f"Expected all altitude agents in the final subscription, got {data['lastAgentCount']} of {agent_total}"
        )
elif data["lastAgentCount"] <= 0 or data["lastAgentCount"] >= agent_total:
    raise SystemExit(
        f"Expected the final subscription to contain a nearby subset, got {data['lastAgentCount']} of {agent_total}"
    )

print(json.dumps(data, ensure_ascii=False, indent=2))
PY

  cleanup_server
}

run_scenario 1000 full
run_scenario 10000 near
run_scenario 100000 near
run_scenario 2 altitude

echo "E2E scenarios passed. Artifacts: $ARTIFACT_DIR"
