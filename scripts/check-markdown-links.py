#!/usr/bin/env python3
"""Validate repository-local links in Markdown files without external dependencies."""

from __future__ import annotations

import re
import sys
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
EXCLUDED_DIRS = {".git", "node_modules", "bin", "obj", "artifacts", "dist"}
FENCED_CODE_RE = re.compile(r"```.*?```|~~~.*?~~~", re.DOTALL)
INLINE_LINK_RE = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")
REFERENCE_LINK_RE = re.compile(r"^\s*\[[^\]]+\]:\s*(\S+)", re.MULTILINE)
EXTERNAL_SCHEMES = {"http", "https", "mailto", "tel", "data", "javascript"}


def markdown_files() -> list[Path]:
    result: list[Path] = []
    for path in ROOT.rglob("*.md"):
        if any(part in EXCLUDED_DIRS for part in path.relative_to(ROOT).parts):
            continue
        result.append(path)
    return sorted(result)


def normalize_destination(raw: str) -> str | None:
    value = raw.strip()
    if not value:
        return None

    if value.startswith("<") and ">" in value:
        value = value[1 : value.index(">")]
    else:
        # Markdown permits an optional title after the destination.
        value = value.split(maxsplit=1)[0]

    value = value.strip()
    if not value or value.startswith("#"):
        return None

    parsed = urlsplit(value)
    if parsed.scheme.lower() in EXTERNAL_SCHEMES or parsed.netloc:
        return None

    # A leading slash is a site-root link, not a repository-local path.
    if parsed.path.startswith("/"):
        return None

    path = unquote(parsed.path)
    return path or None


def destinations(markdown: str) -> list[str]:
    without_fences = FENCED_CODE_RE.sub("", markdown)
    raw_values = [*INLINE_LINK_RE.findall(without_fences), *REFERENCE_LINK_RE.findall(without_fences)]

    normalized: list[str] = []
    for raw in raw_values:
        destination = normalize_destination(raw)
        if destination is not None:
            normalized.append(destination)
    return normalized


def main() -> int:
    errors: list[str] = []
    checked = 0

    for markdown_path in markdown_files():
        text = markdown_path.read_text(encoding="utf-8")
        for destination in destinations(text):
            checked += 1
            target = (markdown_path.parent / destination).resolve()

            try:
                target.relative_to(ROOT)
            except ValueError:
                errors.append(
                    f"{markdown_path.relative_to(ROOT)}: link escapes repository: {destination}"
                )
                continue

            if not target.exists():
                errors.append(
                    f"{markdown_path.relative_to(ROOT)}: missing local target: {destination}"
                )

    if errors:
        print("Markdown link validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Markdown link validation passed ({checked} local links checked).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
