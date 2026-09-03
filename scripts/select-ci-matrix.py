#!/usr/bin/env python3
"""Select E2E and benchmark CI cases from the changed file set.

E2E falls back to the full suite for cross-cutting or unknown changes.
PR benchmarks stay focused on affected code domains, while shared runtime or
build changes fall back to the full benchmark suite. Workflow-only changes are
validated by the benchmark smoke job instead of unrelated performance gates.
"""

from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path


E2E_CASES = [
    {"id": "core-poc", "name": "core-server-browser", "script": "scripts/run-phase6-e2e.sh", "artifacts": ".artifacts/phase6-e2e", "timeout": 25},
    {"id": "road-network", "name": "road-network-server-browser", "script": "scripts/run-phase11-e2e.sh", "artifacts": ".artifacts/phase11-e2e", "timeout": 15},
    {"id": "road-traffic", "name": "road-traffic-server-browser", "script": "scripts/run-phase13-e2e.sh", "artifacts": ".artifacts/phase13-e2e", "timeout": 15},
    {"id": "signal-traffic", "name": "signal-traffic-server-browser", "script": "scripts/run-phase14-e2e.sh", "artifacts": ".artifacts/phase14-e2e", "timeout": 15},
    {"id": "population", "name": "population-server-browser", "script": "scripts/run-phase15-e2e.sh", "artifacts": ".artifacts/phase15-e2e", "timeout": 15},
    {"id": "pedestrian", "name": "building-to-building-pedestrian", "script": "scripts/run-phase16-e2e.sh", "artifacts": ".artifacts/phase16-e2e", "timeout": 15},
    {"id": "railway", "name": "save-server-browser-railway", "script": "scripts/run-phase17-e2e.sh", "artifacts": ".artifacts/phase17-e2e", "timeout": 15},
    {"id": "railway-operations", "name": "railway-operations-server-browser", "script": "scripts/run-phase18-e2e.sh", "artifacts": ".artifacts/phase18-e2e", "timeout": 15},
    {"id": "multimodal-transit", "name": "multimodal-transit-server-browser", "script": "scripts/run-phase19-e2e.sh", "artifacts": ".artifacts/phase19-e2e", "timeout": 15},
    {"id": "administration-console", "name": "administration-console-server-browser", "script": "scripts/run-phase20-e2e.sh", "artifacts": ".artifacts/phase20-e2e", "timeout": 15},
    {"id": "economy", "name": "economy-employment-server-browser", "script": "scripts/run-phase21-e2e.sh", "artifacts": ".artifacts/phase21-e2e", "timeout": 15},
    {"id": "logistics", "name": "logistics-freight-server-browser", "script": "scripts/run-phase22-e2e.sh", "artifacts": ".artifacts/phase22-e2e", "timeout": 15},
    {"id": "power", "name": "power-outage-server-browser", "script": "scripts/run-phase23-e2e.sh", "artifacts": ".artifacts/phase23-e2e", "timeout": 15},
    {"id": "water-sewer", "name": "water-sewer-outage-server-browser", "script": "scripts/run-phase24-e2e.sh", "artifacts": ".artifacts/phase24-e2e", "timeout": 15},
    {"id": "gas", "name": "gas-pipeline-delivery-server-browser", "script": "scripts/run-phase25-e2e.sh", "artifacts": ".artifacts/phase25-e2e", "timeout": 15},
    {"id": "optical", "name": "optical-reroute-outage-server-browser", "script": "scripts/run-phase26-e2e.sh", "artifacts": ".artifacts/phase26-e2e", "timeout": 15},
    {"id": "remote-mcp", "name": "remote-mcp-administration", "script": "scripts/run-phase27-e2e.sh", "artifacts": ".artifacts/phase27-e2e", "timeout": 15},
    {"id": "radio-spectrum", "name": "radio-spectrum-server-browser", "script": "scripts/run-phase28-e2e.sh", "artifacts": ".artifacts/phase28-e2e", "timeout": 15},
    {"id": "world-environment", "name": "world-environment-restart-reproducibility", "script": "scripts/run-phase29-e2e.sh", "artifacts": ".artifacts/phase29-e2e", "timeout": 20},
    {"id": "view-physical-world", "name": "view-physical-world-rendering", "script": "scripts/run-view-phase03-e2e.sh", "artifacts": ".artifacts/view-phase03-e2e", "timeout": 20},
    {"id": "view-settlement-structure", "name": "view-settlement-structure-rendering", "script": "scripts/run-view-phase04-e2e.sh", "artifacts": ".artifacts/view-phase04-e2e", "timeout": 15},
    {"id": "view-settlement-structure-live", "name": "view-settlement-structure-live-delivery", "script": "scripts/run-view-phase04-live-e2e.sh", "artifacts": ".artifacts/view-phase04-live-e2e", "timeout": 20},
]

BENCHMARKDOTNET_CASES = [
    {"id": "road-network", "name": "road-network-10k-100k", "filter": "*RoadNetworkBenchmarks*", "timeout": 20},
    {"id": "routing", "name": "routing-small-medium-large", "filter": "*RoutingBenchmarks*", "timeout": 30},
    {"id": "intersection-control", "name": "queued-intersections", "filter": "*IntersectionControlBenchmarks*", "timeout": 30},
    {"id": "pedestrian", "name": "pedestrians-1k-10k", "filter": "*PedestrianBenchmarks*", "timeout": 20},
    {"id": "railway-infrastructure", "name": "railway-10k-100k", "filter": "*RailwayInfrastructureBenchmarks*", "timeout": 20},
    {"id": "railway-operations", "name": "railway-operations-100-1000", "filter": "*RailwayOperationsBenchmarks*", "timeout": 20},
    {"id": "multimodal-transit", "name": "journey-transfer-dispatch", "filter": "*MultimodalTransitBenchmarks*", "timeout": 20},
    {"id": "logistics", "name": "logistics-inventory-100-1000", "filter": "*LogisticsBenchmarks*", "timeout": 20},
    {"id": "power", "name": "power-loads-1k-5k", "filter": "*PowerBenchmarks*", "timeout": 20},
    {"id": "water-sewer", "name": "water-sewer-loads-1k-5k", "filter": "*WaterSewerBenchmarks*", "timeout": 20},
    {"id": "gas", "name": "gas-loads-1k-5k", "filter": "*GasBenchmarks*", "timeout": 20},
    {"id": "persistent-regional-evolution", "name": "persistent-regional-evolution-world-scale", "filter": "*PersistentRegionalEvolutionBenchmarks*", "timeout": 30},
]

SCENARIO_CASES = [
    {"id": "road-traffic", "name": "vehicles-1k-10k-100k", "args": "--road-traffic --warmup 5 --ticks 20"},
    {"id": "population", "name": "population-1k-10k-100k", "args": "--population --warmup 10 --ticks 50"},
]

E2E_CROSS_CUTTING_PREFIXES = (
    "src/MachiVerseWorks.Persistence/",
    "src/MachiVerseWorks.Protocol/",
    "src/MachiVerseWorks.Server/",
)

E2E_CROSS_CUTTING_FILES = {
    "Directory.Build.props",
    "Directory.Packages.props",
    "MachiVerseWorks.slnx",
    "global.json",
    "scripts/prepare-e2e.sh",
    "scripts/select-ci-matrix.py",
}

BENCHMARK_CROSS_CUTTING_PREFIXES = (
    "src/MachiVerseWorks.Persistence/",
    "src/MachiVerseWorks.Protocol/",
    "src/MachiVerseWorks.Server/",
)

BENCHMARK_CROSS_CUTTING_FILES = {
    "Directory.Build.props",
    "Directory.Packages.props",
    "MachiVerseWorks.slnx",
    "global.json",
    "scripts/compare-benchmark-results.py",
}


def changed_files(base: str | None, head: str | None, full: bool) -> list[str]:
    if full:
        return []
    if not base or not head:
        raise SystemExit("--base and --head are required unless --full is used")
    output = subprocess.check_output(
        ["git", "diff", "--name-only", base, head], text=True, encoding="utf-8"
    )
    return [line.strip() for line in output.splitlines() if line.strip()]


def is_e2e_cross_cutting(files: list[str]) -> bool:
    for path in files:
        if path in E2E_CROSS_CUTTING_FILES or path.startswith(E2E_CROSS_CUTTING_PREFIXES):
            return True
        if path in {".github/workflows/ci.yml", ".github/workflows/e2e.yml"}:
            return True
    return False


def is_benchmark_cross_cutting(files: list[str]) -> bool:
    return any(
        path in BENCHMARK_CROSS_CUTTING_FILES or path.startswith(BENCHMARK_CROSS_CUTTING_PREFIXES)
        for path in files
    )


def contains_any(path: str, terms: tuple[str, ...]) -> bool:
    value = Path(path).name.lower()
    return any(term in value for term in terms)


def full_benchmark_selection(reason: str = "full") -> dict[str, object]:
    return {
        "benchmarkdotnet": {"include": BENCHMARKDOTNET_CASES},
        "scenario": {"include": SCENARIO_CASES},
        "run_benchmarkdotnet": True,
        "run_scenario": True,
        "run_snapshot_readmodel": True,
        "run_regression": True,
        "reason": reason,
    }


def select_e2e(files: list[str], full: bool) -> dict[str, object]:
    if full or is_e2e_cross_cutting(files):
        return {"matrix": {"include": E2E_CASES}, "reason": "full"}

    selected: set[str] = set()
    uncertain = False

    script_map = {case["script"]: case["id"] for case in E2E_CASES}
    for path in files:
        if path in script_map:
            selected.add(script_map[path])
            continue

        if path.startswith("src/web/"):
            selected.update({"core-poc", "view-physical-world", "view-settlement-structure", "view-settlement-structure-live"})
            continue

        if path.startswith("src/MachiVerseWorks.Simulation/"):
            matched = False
            mappings = [
                (("roadnetwork", "road-network", "routing"), {"road-network", "road-traffic", "signal-traffic"}),
                (("roadtraffic", "traffic", "vehicle"), {"road-traffic", "signal-traffic"}),
                (("intersection", "signal"), {"signal-traffic", "road-traffic"}),
                (("population",), {"population", "pedestrian", "economy"}),
                (("pedestrian",), {"pedestrian"}),
                (("railway", "rail"), {"railway", "railway-operations", "multimodal-transit"}),
                (("multimodal", "transit"), {"multimodal-transit"}),
                (("economy", "employment"), {"economy"}),
                (("logistics", "freight", "inventory"), {"logistics"}),
                (("power",), {"power"}),
                (("water", "sewer"), {"water-sewer"}),
                (("gas",), {"gas"}),
                (("optical",), {"optical"}),
                (("radio", "spectrum"), {"radio-spectrum"}),
                (("worldenvironment", "environment", "weather", "climate"), {"world-environment"}),
                (("regional", "settlement", "building", "toponym"), {"view-physical-world", "view-settlement-structure", "view-settlement-structure-live"}),
            ]
            for terms, ids in mappings:
                if contains_any(path, terms):
                    selected.update(ids)
                    matched = True
            if not matched:
                uncertain = True
            continue

        if path.startswith("tests/fixtures/") or path.startswith("docs/development/baselines/"):
            lower = path.lower()
            if "view" in lower or "settlement" in lower:
                selected.update({"view-physical-world", "view-settlement-structure", "view-settlement-structure-live"})
            else:
                uncertain = True
            continue

        if path.startswith("scripts/run-") and path.endswith("-e2e.sh"):
            uncertain = True
            continue

        if path.startswith("src/") or path.startswith("scripts/"):
            uncertain = True

    if uncertain or not selected:
        return {"matrix": {"include": E2E_CASES}, "reason": "fallback-full"}

    selected.add("core-poc")
    cases = [case for case in E2E_CASES if case["id"] in selected]
    return {"matrix": {"include": cases}, "reason": "affected"}


def select_benchmarks(files: list[str], full: bool) -> dict[str, object]:
    if full:
        return full_benchmark_selection()
    if is_benchmark_cross_cutting(files):
        return full_benchmark_selection("shared-full")

    benchmark_ids: set[str] = set()
    scenario_ids: set[str] = set()
    snapshot = False
    regression = False
    uncertain = False

    for path in files:
        if path.startswith("src/web/"):
            continue

        if path.startswith("benchmarks/MachiVerseWorks.Benchmarks/"):
            name = Path(path).name.lower()
            direct = {
                "roadnetworkbenchmarks.cs": {"road-network"},
                "routingbenchmarks.cs": {"routing"},
                "intersectioncontrolbenchmarks.cs": {"intersection-control"},
                "pedestrianbenchmarks.cs": {"pedestrian"},
                "railwayinfrastructurebenchmarks.cs": {"railway-infrastructure"},
                "railwayoperationsbenchmarks.cs": {"railway-operations"},
                "multimodaltransitbenchmarks.cs": {"multimodal-transit"},
                "logisticsbenchmarks.cs": {"logistics"},
                "powerbenchmarks.cs": {"power"},
                "watersewerbenchmarks.cs": {"water-sewer"},
                "gasbenchmarks.cs": {"gas"},
                "persistentregionalevolutionbenchmarks.cs": {"persistent-regional-evolution"},
            }
            if name in direct:
                benchmark_ids.update(direct[name])
            else:
                uncertain = True
            continue

        if path.startswith("src/MachiVerseWorks.Simulation/"):
            matched = False
            mappings = [
                (("roadnetwork", "road-network"), {"road-network", "routing"}, set()),
                (("routing",), {"routing"}, set()),
                (("intersection", "signal"), {"intersection-control"}, set()),
                (("roadtraffic", "traffic", "vehicle"), set(), {"road-traffic"}),
                (("population",), set(), {"population"}),
                (("pedestrian",), {"pedestrian"}, set()),
                (("railway", "rail"), {"railway-infrastructure", "railway-operations", "multimodal-transit"}, set()),
                (("multimodal", "transit"), {"multimodal-transit"}, set()),
                (("logistics", "freight", "inventory"), {"logistics"}, set()),
                (("power",), {"power"}, set()),
                (("water", "sewer"), {"water-sewer"}, set()),
                (("gas",), {"gas"}, set()),
                (("regional", "settlement", "toponym"), {"persistent-regional-evolution"}, set()),
            ]
            for terms, bench, scenario in mappings:
                if contains_any(path, terms):
                    benchmark_ids.update(bench)
                    scenario_ids.update(scenario)
                    matched = True
            if contains_any(path, ("snapshot", "readmodel", "publishedreadmodel")):
                snapshot = True
                matched = True
            if contains_any(path, ("geometry", "position", "coordinate")):
                regression = True
                matched = True
            if not matched:
                uncertain = True
            continue

        if path == "scripts/run-phase9-regression-benchmark.sh":
            regression = True
            continue

        if path.startswith("src/") or path.startswith("benchmarks/"):
            uncertain = True

    if uncertain:
        return full_benchmark_selection("fallback-full")

    benchmark_cases = [case for case in BENCHMARKDOTNET_CASES if case["id"] in benchmark_ids]
    scenario_cases = [case for case in SCENARIO_CASES if case["id"] in scenario_ids]
    return {
        "benchmarkdotnet": {"include": benchmark_cases or [BENCHMARKDOTNET_CASES[0]]},
        "scenario": {"include": scenario_cases or [SCENARIO_CASES[0]]},
        "run_benchmarkdotnet": bool(benchmark_cases),
        "run_scenario": bool(scenario_cases),
        "run_snapshot_readmodel": snapshot,
        "run_regression": regression,
        "reason": "affected" if benchmark_cases or scenario_cases or snapshot or regression else "smoke-only",
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("e2e", "benchmarks"))
    parser.add_argument("--base")
    parser.add_argument("--head")
    parser.add_argument("--full", action="store_true")
    args = parser.parse_args()

    files = changed_files(args.base, args.head, args.full)
    result = select_e2e(files, args.full) if args.mode == "e2e" else select_benchmarks(files, args.full)
    print(json.dumps(result, separators=(",", ":")))


if __name__ == "__main__":
    main()
