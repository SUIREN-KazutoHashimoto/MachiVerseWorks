#!/usr/bin/env python3
"""Compare BenchmarkDotNet JSON results from a PR head against its base run."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", required=True, type=Path)
    parser.add_argument("--head", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--latency-ratio", type=float, default=1.25)
    parser.add_argument("--allocation-ratio", type=float, default=1.15)
    return parser.parse_args()


def load_benchmarks(root: Path) -> dict[str, dict[str, float | None]]:
    results: dict[str, dict[str, float | None]] = {}
    for path in sorted(root.rglob("*.json")):
        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        benchmarks = document.get("Benchmarks") if isinstance(document, dict) else None
        if not isinstance(benchmarks, list):
            continue
        for item in benchmarks:
            if not isinstance(item, dict):
                continue
            name = item.get("FullName") or item.get("DisplayInfo") or item.get("Method")
            if not isinstance(name, str) or not name:
                continue
            statistics = item.get("Statistics") if isinstance(item.get("Statistics"), dict) else {}
            memory = item.get("Memory") if isinstance(item.get("Memory"), dict) else {}
            mean = finite_number(statistics.get("Mean"))
            allocated = finite_number(memory.get("BytesAllocatedPerOperation"))
            results[name] = {"mean_ns": mean, "allocated_bytes": allocated}
    return results


def finite_number(value: Any) -> float | None:
    if not isinstance(value, (int, float)):
        return None
    number = float(value)
    return number if math.isfinite(number) and number >= 0.0 else None


def compare_metric(base: float | None, head: float | None, ratio: float) -> tuple[bool, float | None]:
    if base is None or head is None:
        return False, None
    if base == 0.0:
        return head == 0.0, None
    observed = head / base
    return observed <= ratio, observed


def main() -> int:
    args = parse_args()
    if args.latency_ratio < 1.0 or args.allocation_ratio < 1.0:
        raise SystemExit("Regression ratios must be at least 1.0")

    base = load_benchmarks(args.base)
    head = load_benchmarks(args.head)
    common = sorted(base.keys() & head.keys())
    if not common:
        raise SystemExit("No matching BenchmarkDotNet JSON benchmark results were found in base/head directories")

    rows: list[dict[str, Any]] = []
    failed = False
    for name in common:
        latency_ok, latency_ratio = compare_metric(base[name]["mean_ns"], head[name]["mean_ns"], args.latency_ratio)
        allocation_ok, allocation_ratio = compare_metric(base[name]["allocated_bytes"], head[name]["allocated_bytes"], args.allocation_ratio)
        ok = latency_ok and allocation_ok
        failed |= not ok
        rows.append({
            "benchmark": name,
            "base_mean_ns": base[name]["mean_ns"],
            "head_mean_ns": head[name]["mean_ns"],
            "latency_ratio": latency_ratio,
            "latency_limit": args.latency_ratio,
            "base_allocated_bytes": base[name]["allocated_bytes"],
            "head_allocated_bytes": head[name]["allocated_bytes"],
            "allocation_ratio": allocation_ratio,
            "allocation_limit": args.allocation_ratio,
            "passed": ok,
        })

    report = {
        "base_directory": str(args.base),
        "head_directory": str(args.head),
        "benchmarks_compared": len(rows),
        "passed": not failed,
        "results": rows,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    for row in rows:
        status = "PASS" if row["passed"] else "FAIL"
        print(f"{status}: {row['benchmark']} latency={row['latency_ratio']} allocation={row['allocation_ratio']}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
