from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}: {old[:100]!r}")
    return text.replace(old, new, 1)


ci_path = Path('.github/workflows/ci.yml')
ci = ci_path.read_text(encoding='utf-8')
ci = replace_once(ci, '        run: npm run lint --if-present', '        run: npm run lint', 'ci lint')
ci = replace_once(ci, '        run: npm run typecheck --if-present', '        run: npm run typecheck', 'ci typecheck')
ci = replace_once(ci, '        run: npm test --if-present', '        run: npm test', 'ci test')
ci = replace_once(
    ci,
    '''      - name: Validate Markdown links
        run: python scripts/check-markdown-links.py''',
    '''      - name: Validate required Web quality scripts
        shell: python
        run: |
          import json
          from pathlib import Path
          package = json.loads(Path('src/web/package.json').read_text(encoding='utf-8'))
          scripts = package.get('scripts')
          if not isinstance(scripts, dict):
              raise SystemExit('src/web/package.json must contain a scripts object')
          for name in ('lint', 'typecheck', 'test', 'build'):
              value = scripts.get(name)
              if not isinstance(value, str) or not value.strip():
                  raise SystemExit(f'src/web/package.json requires a non-empty scripts.{name}')

      - name: Validate Markdown links
        run: python scripts/check-markdown-links.py''',
    'ci quality scripts',
)
dependency_job = '''  dependency_review:
    name: dependency review
    needs: [repository]
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Review dependency changes
        uses: actions/dependency-review-action@a1d282b36b6f3519aa1f3fc636f609c47dddb294 # v5
        with:
          fail-on-severity: high

'''
ci = replace_once(ci, '  e2e:\n    name: required e2e', dependency_job + '  e2e:\n    name: required e2e', 'ci dependency job')
ci = replace_once(ci, '    needs: [repository, detect, dotnet, web, e2e]', '    needs: [repository, detect, dotnet, web, dependency_review, e2e]', 'ci gate needs')
ci = replace_once(ci, '          E2E_RESULT: ${{ needs.e2e.result }}', '          DEPENDENCY_REVIEW_RESULT: ${{ needs.dependency_review.result }}\n          E2E_RESULT: ${{ needs.e2e.result }}', 'ci gate env')
ci = replace_once(ci, '          for entry in "dotnet:$DOTNET_RESULT" "web:$WEB_RESULT" "e2e:$E2E_RESULT"; do', '          for entry in "dotnet:$DOTNET_RESULT" "web:$WEB_RESULT" "dependency-review:$DEPENDENCY_REVIEW_RESULT" "e2e:$E2E_RESULT"; do', 'ci gate loop')
Path('scripts/worker3-ci-proposed.yml').write_text(ci, encoding='utf-8')


e2e_path = Path('.github/workflows/e2e.yml')
e2e = e2e_path.read_text(encoding='utf-8')
prepare_job = '''  prepare:
    name: prepare shared build
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - name: Checkout
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7
      - name: Setup .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6
        with:
          global-json-file: global.json
      - name: Setup Node
        uses: actions/setup-node@820762786026740c76f36085b0efc47a31fe5020 # v7
        with:
          node-version-file: src/web/.node-version
          cache: npm
          cache-dependency-path: src/web/package-lock.json
      - name: Build shared E2E inputs
        run: source scripts/prepare-e2e.sh
      - name: Pack shared E2E inputs
        shell: bash
        run: |
          set -euo pipefail
          mkdir -p .artifacts
          printf '%s\\n' "$GITHUB_SHA" > .e2e-prepared-commit
          tar -cf .artifacts/e2e-prepared.tar .e2e-prepared-commit src/*/bin src/*/obj src/web/node_modules src/web/dist
      - name: Upload shared E2E inputs
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7
        with:
          name: e2e-prepared-${{ github.sha }}
          path: .artifacts/e2e-prepared.tar
          if-no-files-found: error
          retention-days: 1

'''
e2e = replace_once(e2e, 'jobs:\n  e2e:', 'jobs:\n' + prepare_job + '  e2e:', 'e2e prepare')
e2e = replace_once(e2e, '  e2e:\n    name: ${{ matrix.name }}', '  e2e:\n    name: ${{ matrix.name }}\n    needs: prepare', 'e2e needs')
e2e = replace_once(
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
    'e2e restore',
)
Path('scripts/worker3-e2e-proposed.yml').write_text(e2e, encoding='utf-8')
