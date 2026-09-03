#!/usr/bin/env bash
set -euo pipefail
E2E_ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ "${MVW_E2E_PREPARED:-0}" != "1" ]]; then
  dotnet restore "$E2E_ROOT_DIR/MachiVerseWorks.slnx"
  dotnet build "$E2E_ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore
  npm --prefix "$E2E_ROOT_DIR/src/web" ci
  npm --prefix "$E2E_ROOT_DIR/src/web" run build
fi
