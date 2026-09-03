from pathlib import Path

path = Path("scripts/worker3-batch2.py")
text = path.read_text(encoding="utf-8")
text = text.replace(
    "write(path, text[:begin] + replacement + text[finish:])",
    "write(path, text[:begin] + replacement + text[finish + len(end):])",
    1,
)
start_marker = '''replace_once(
    e2e,
    """          cache-dependency-path: src/web/package-lock.json'''
end_marker = "\n# Focused regression tests."
start = text.index(start_marker)
end = text.index(end_marker, start)
replacement = """replace_once(
    e2e,
    '''          cache-dependency-path: src/web/package-lock.json
      - name: Run end-to-end
        run: bash "${{ matrix.script }}"''',
    '''          cache-dependency-path: src/web/package-lock.json
      - name: Download shared E2E inputs
        uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8
        with:
          name: e2e-prepared-${{ github.sha }}
          path: .artifacts/prepared
      - name: Restore shared E2E inputs
        shell: bash
        run: |
          set -euo pipefail
          tar -xf .artifacts/prepared/e2e-prepared.tar
          test "$(cat .e2e-prepared-commit)" = "$GITHUB_SHA"
      - name: Run end-to-end
        env:
          MVW_E2E_PREPARED: '1'
        run: bash "${{ matrix.script }}"''',
)
"""
path.write_text(text[:start] + replacement + text[end:], encoding="utf-8")
