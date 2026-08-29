#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline_commit="${PHASE9_BASELINE_COMMIT:-2ada7e8736c7d93038f3291fd7db154f58db09e0}"
artifacts_root="${MACHIVERSE_PHASE9_REGRESSION_ARTIFACTS:-$repository_root/.artifacts/phase9-regression}"
baseline_worktree="$(mktemp -d)"

cleanup() {
  git -C "$repository_root" worktree remove --force "$baseline_worktree" >/dev/null 2>&1 || true
  rm -rf "$baseline_worktree"
}
trap cleanup EXIT

rm -rf "$artifacts_root"
mkdir -p "$artifacts_root"

{
  echo "baseline_commit=$baseline_commit"
  echo "current_commit=$(git -C "$repository_root" rev-parse HEAD)"
  echo "utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo
  uname -a
  echo
  lscpu
  echo
  dotnet --info
} > "$artifacts_root/environment.txt"

git -C "$repository_root" worktree add --detach "$baseline_worktree" "$baseline_commit"

run_suite() {
  local label="$1"
  local source_root="$2"
  local output_root="$artifacts_root/$label"
  local benchmark_project="$source_root/benchmarks/MachiVerseWorks.Benchmarks/MachiVerseWorks.Benchmarks.csproj"

  mkdir -p "$output_root"
  dotnet restore "$source_root/MachiVerseWorks.slnx"
  dotnet build "$benchmark_project" --configuration Release --no-restore

  MACHIVERSE_BENCHMARK_ARTIFACTS="$output_root/benchmarkdotnet" \
    dotnet run --project "$benchmark_project" --configuration Release --no-build -- \
      --job Short \
      --filter \
      'MachiVerseWorks.Benchmarks.SpatialQueryBenchmarks.*' \
      'MachiVerseWorks.Benchmarks.SnapshotBenchmarks.*' \
      'MachiVerseWorks.Benchmarks.ProtocolCodecBenchmarks.*'

  dotnet run --project "$benchmark_project" --configuration Release --no-build -- \
    --warmup 60 --ticks 200 > "$output_root/tick.csv"
}

run_suite baseline "$baseline_worktree"
run_suite current "$repository_root"
