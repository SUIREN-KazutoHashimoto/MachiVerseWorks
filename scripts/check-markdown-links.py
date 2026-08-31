#!/usr/bin/env python3
"""Validate repository-local Markdown links and heading anchors without external dependencies."""

from __future__ import annotations

import re
import sys
import unicodedata
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
EXCLUDED_DIRS = {".git", "node_modules", "bin", "obj", "artifacts", "dist"}
FENCED_CODE_RE = re.compile(r"```.*?```|~~~.*?~~~", re.DOTALL)
INLINE_LINK_RE = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")
REFERENCE_LINK_RE = re.compile(r"^\s*\[[^\]]+\]:\s*(\S+)", re.MULTILINE)
HEADING_RE = re.compile(r"^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$", re.MULTILINE)
INLINE_MARKDOWN_LINK_RE = re.compile(r"\[([^\]]+)\]\([^)]+\)")
HTML_TAG_RE = re.compile(r"<[^>]+>")
EXTERNAL_SCHEMES = {"http", "https", "mailto", "tel", "data", "javascript"}


def markdown_files() -> list[Path]:
    result: list[Path] = []
    for path in ROOT.rglob("*.md"):
        if any(part in EXCLUDED_DIRS for part in path.relative_to(ROOT).parts):
            continue
        result.append(path)
    return sorted(result)


def normalize_destination(raw: str) -> tuple[str, str | None] | None:
    value = raw.strip()
    if not value:
        return None

    if value.startswith("<") and ">" in value:
        value = value[1 : value.index(">")]
    else:
        # Markdown permits an optional title after the destination.
        value = value.split(maxsplit=1)[0]

    value = value.strip()
    if not value:
        return None

    parsed = urlsplit(value)
    if parsed.scheme.lower() in EXTERNAL_SCHEMES or parsed.netloc:
        return None

    # A leading slash is a site-root link, not a repository-local path.
    if parsed.path.startswith("/"):
        return None

    return unquote(parsed.path), unquote(parsed.fragment) if parsed.fragment else None


def destinations(markdown: str) -> list[tuple[str, str | None]]:
    without_fences = FENCED_CODE_RE.sub("", markdown)
    raw_values = [*INLINE_LINK_RE.findall(without_fences), *REFERENCE_LINK_RE.findall(without_fences)]

    normalized: list[tuple[str, str | None]] = []
    for raw in raw_values:
        destination = normalize_destination(raw)
        if destination is not None:
            normalized.append(destination)
    return normalized


def github_heading_slug(text: str) -> str:
    value = INLINE_MARKDOWN_LINK_RE.sub(r"\1", text)
    value = HTML_TAG_RE.sub("", value).replace("`", "").strip().lower()
    value = "".join(
        char for char in value
        if char in {" ", "-", "_"} or not unicodedata.category(char).startswith(("P", "S"))
    )
    return re.sub(r"\s+", "-", value)


def heading_anchors(markdown: str) -> set[str]:
    without_fences = FENCED_CODE_RE.sub("", markdown)
    anchors: set[str] = set()
    occurrences: dict[str, int] = {}
    for match in HEADING_RE.finditer(without_fences):
        base = github_heading_slug(match.group(1))
        if not base:
            continue
        occurrence = occurrences.get(base, 0)
        anchor = base if occurrence == 0 else f"{base}-{occurrence}"
        occurrences[base] = occurrence + 1
        anchors.add(anchor)
    return anchors


def main() -> int:
    errors: list[str] = []
    checked = 0
    anchor_cache: dict[Path, set[str]] = {}

    for markdown_path in markdown_files():
        text = markdown_path.read_text(encoding="utf-8")
        for destination, fragment in destinations(text):
            checked += 1
            target = markdown_path if not destination else (markdown_path.parent / destination).resolve()

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
                continue

            if fragment is None or target.suffix.lower() != ".md":
                continue

            anchors = anchor_cache.get(target)
            if anchors is None:
                anchors = heading_anchors(target.read_text(encoding="utf-8"))
                anchor_cache[target] = anchors
            if fragment not in anchors:
                display_target = destination or target.name
                errors.append(
                    f"{markdown_path.relative_to(ROOT)}: missing Markdown heading anchor: {display_target}#{fragment}"
                )

    if errors:
        print("Markdown link validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Markdown link validation passed ({checked} local links checked, including heading anchors).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
