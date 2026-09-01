#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/.artifacts/phase27-e2e"
mkdir -p "$ARTIFACT_DIR"
rm -f "$ARTIFACT_DIR"/*

dotnet restore "$ROOT_DIR/MachiVerseWorks.slnx" 2>&1 | tee "$ARTIFACT_DIR/dotnet-restore.log"
dotnet build "$ROOT_DIR/MachiVerseWorks.slnx" --configuration Release --no-restore 2>&1 | tee "$ARTIFACT_DIR/dotnet-build.log"
dotnet test "$ROOT_DIR/tests/MachiVerseWorks.Server.Tests/MachiVerseWorks.Server.Tests.csproj" \
  --configuration Release \
  --no-build \
  --filter 'FullyQualifiedName~RemoteMcpTests' \
  2>&1 | tee "$ARTIFACT_DIR/remote-mcp-tests.log"

echo "Phase 27 Remote MCP -> Admin command -> SimulationRuntime E2E passed."
