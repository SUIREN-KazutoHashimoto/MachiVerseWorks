from __future__ import annotations

from pathlib import Path
import json
import re


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}: {old[:120]!r}")
    write(path, text.replace(old, new, 1))


def replace_between(path: str, start: str, end: str, replacement: str) -> None:
    text = read(path)
    if text.count(start) != 1:
        raise SystemExit(f"{path}: start marker count != 1: {start!r}")
    begin = text.index(start)
    finish = text.index(end, begin)
    write(path, text[:begin] + replacement + text[finish:])


# #246: Application.start owns a single RAF loop.
app = "src/web/src/application.ts"
replace_once(
    app,
    "  private audioSyncPending = false;\n  private disposed = false;",
    "  private audioSyncPending = false;\n  private started = false;\n  private terrainCameraInitialized = false;\n  private disposed = false;",
)
replace_once(
    app,
    "  public start(): void { if (this.disposed) throw new Error('Application is disposed.'); this.connection.connect(); this.animationFrame = window.requestAnimationFrame(this.animate); }",
    """  public start(): void {
    if (this.disposed) throw new Error('Application is disposed.');
    if (this.started) return;
    this.started = true;
    try {
      this.connection.connect();
      this.animationFrame = window.requestAnimationFrame(this.animate);
    } catch (error) {
      this.started = false;
      throw error;
    }
  }""",
)
replace_once(
    app,
    "    this.disposed = true; window.cancelAnimationFrame(this.animationFrame);",
    "    this.started = false; this.disposed = true; window.cancelAnimationFrame(this.animationFrame);",
)
replace_once(
    app,
    "    if (this.disposed) return;\n    const performanceMetrics",
    "    if (this.disposed || !this.started) return;\n    const performanceMetrics",
)

# #301: initialize camera altitude from the first terrain snapshot and keep min height terrain-relative.
replace_once(
    app,
    """      case WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE:
      case REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE:
      case PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE:
        this.observation.apply(message); return;""",
    """      case WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE:
        this.observation.apply(message); this.initializeTerrainCamera(message); return;
      case REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE:
      case PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE:
        this.observation.apply(message); return;""",
)
replace_once(
    app,
    "  private applyPopulationStatistics(message: PopulationStatisticsMessage): void { this.ui.setPopulationStatistics(message); }",
    """  private initializeTerrainCamera(message: WorldEnvironmentSnapshotMessage): void {
    if (this.terrainCameraInitialized) return;
    const centerX = (message.minX + message.maxX) * 0.5;
    const centerY = (message.minY + message.maxY) * 0.5;
    const elevation = this.observation.worldEnvironment.getNearestTerrainElevation(centerX, centerY);
    if (elevation === undefined || !this.navigation.rebaseFocusAltitude(0, elevation)) return;
    this.terrainCameraInitialized = true;
    this.lastSubscription = null;
    this.lastSubscriptionAt = Number.NEGATIVE_INFINITY;
  }

  private applyPopulationStatistics(message: PopulationStatisticsMessage): void { this.ui.setPopulationStatistics(message); }""",
)

store = "src/web/src/world-environment-store.ts"
replace_once(
    store,
    "  getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined;\n}",
    "  getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined;\n  getNearestTerrainElevation(x: number, y: number): number | undefined;\n}",
)
replace_once(
    store,
    """  public getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined {
    return this.toponymsByFeatureId.get(featureId);
  }

  public clear(): void {""",
    """  public getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined {
    return this.toponymsByFeatureId.get(featureId);
  }

  public getNearestTerrainElevation(x: number, y: number): number | undefined {
    if (!Number.isFinite(x) || !Number.isFinite(y)) return undefined;
    const samples = this.currentSnapshot?.terrainSamples;
    if (samples === undefined || samples.length === 0) return undefined;
    let nearest = samples[0]!;
    let nearestDistanceSquared = Number.POSITIVE_INFINITY;
    for (const sample of samples) {
      const dx = sample.x - x;
      const dy = sample.y - y;
      const distanceSquared = dx * dx + dy * dy;
      if (distanceSquared < nearestDistanceSquared) {
        nearest = sample;
        nearestDistanceSquared = distanceSquared;
      }
    }
    return nearest.z;
  }

  public clear(): void {""",
)

nav = "src/web/src/view-navigation.ts"
replace_once(
    nav,
    "  private currentMoveSpeed: number;\n  private currentFollowDistance: number;",
    "  private currentMoveSpeed: number;\n  private currentFollowDistance: number;\n  private altitudeBaseline = 0;",
)
replace_once(
    nav,
    "  public jump(target: ViewNavigationTarget, now = performance.now()): boolean {",
    """  public rebaseFocusAltitude(fromAltitude: number, toAltitude: number): boolean {
    validateFinite(fromAltitude, 'source focus altitude');
    validateFinite(toAltitude, 'target focus altitude');
    if (getCameraFocusAtSimulationAltitude(this.camera, fromAltitude) === undefined) return false;
    const delta = toAltitude - fromAltitude;
    this.camera.position.y += delta;
    this.altitudeBaseline += delta;
    this.camera.updateMatrixWorld(true);
    return true;
  }

  public jump(target: ViewNavigationTarget, now = performance.now()): boolean {""",
)
replace_once(nav, "    this.camera.position.y = Math.max(this.minimumHeight, this.camera.position.y);\n    this.camera.updateMatrixWorld(true);\n  }\n\n  private placeBehindSampledPosition", "    this.camera.position.y = Math.max(this.altitudeBaseline + this.minimumHeight, this.camera.position.y);\n    this.camera.updateMatrixWorld(true);\n  }\n\n  private placeBehindSampledPosition")
replace_once(
    nav,
    """    const target = simulationPositionToThree(position);
    const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(this.camera.quaternion).normalize();
    this.camera.position.copy(target).addScaledVector(forward, -distance);
    this.camera.position.y = Math.max(this.minimumHeight, this.camera.position.y);""",
    """    const target = simulationPositionToThree(position);
    this.altitudeBaseline = target.y;
    const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(this.camera.quaternion).normalize();
    this.camera.position.copy(target).addScaledVector(forward, -distance);
    this.camera.position.y = Math.max(this.altitudeBaseline + this.minimumHeight, this.camera.position.y);""",
)
replace_once(
    nav,
    """    const target = simulationPositionToThree(position);
    const cp = Math.cos(this.pitch);""",
    """    const target = simulationPositionToThree(position);
    this.altitudeBaseline = target.y;
    const cp = Math.cos(this.pitch);""",
)
replace_once(nav, "    this.camera.position.y = Math.max(this.minimumHeight, this.camera.position.y);\n    this.camera.lookAt(target);", "    this.camera.position.y = Math.max(this.altitudeBaseline + this.minimumHeight, this.camera.position.y);\n    this.camera.lookAt(target);")

# #257: localize economy detail rows and use locale-aware number formatting.
loc = "src/web/src/localization.ts"
replace_once(
    loc,
    """  public constructor(
    public readonly locale: string,
    private readonly resource: LocaleResource,
  ) {}""",
    """  private readonly numberFormatter: Intl.NumberFormat;

  public constructor(
    public readonly locale: string,
    private readonly resource: LocaleResource,
  ) {
    this.numberFormatter = new Intl.NumberFormat(locale);
  }""",
)
replace_once(
    loc,
    """  public t(key: string, parameters: LocaleParameters = {}): string {
    const template = this.resource[key] ?? key;
    return template.replace(/\\{([A-Za-z0-9_.-]+)\\}/g, (_match, parameterName: string) => {
      const value = parameters[parameterName];
      return value === undefined ? `{${parameterName}}` : String(value);
    });
  }
}""",
    """  public t(key: string, parameters: LocaleParameters = {}): string {
    const template = this.resource[key] ?? key;
    return template.replace(/\\{([A-Za-z0-9_.-]+)\\}/g, (_match, parameterName: string) => {
      const value = parameters[parameterName];
      return value === undefined ? `{${parameterName}}` : String(value);
    });
  }

  public formatNumber(value: number | bigint): string {
    return this.numberFormatter.format(value);
  }
}""",
)
ui = "src/web/src/ui.ts"
replace_once(
    ui,
    """    const companies = message.companies.slice(0, 4).map((company) => `C${company.companyId.toString()}: ${company.employeeCount}人 / 売上 ${company.revenue.toString()}`);
    const households = message.households.slice(0, 4).map((household) => `H${household.householdId.toString()}: 残高 ${household.cashBalance.toString()} / 所得 ${household.income.toString()} / 支出 ${household.spending.toString()}`);""",
    """    const companies = message.companies.slice(0, 4).map((company) => this.localizer.t('economyDebug.companyDetail', {
      companyId: company.companyId,
      employeeCount: this.localizer.formatNumber(company.employeeCount),
      revenue: this.localizer.formatNumber(company.revenue),
    }));
    const households = message.households.slice(0, 4).map((household) => this.localizer.t('economyDebug.householdDetail', {
      householdId: household.householdId,
      cashBalance: this.localizer.formatNumber(household.cashBalance),
      income: this.localizer.formatNumber(household.income),
      spending: this.localizer.formatNumber(household.spending),
    }));""",
)
ja = "src/web/locales/ja-JP.json"
ja_data = json.loads(read(ja))
ja_data["economyDebug.companyDetail"] = "C{companyId}: {employeeCount}人 / 売上 {revenue}"
ja_data["economyDebug.householdDetail"] = "H{householdId}: 残高 {cashBalance} / 所得 {income} / 支出 {spending}"
write(ja, json.dumps(ja_data, ensure_ascii=False, indent=2) + "\n")

# #255: eliminate the DevTools port reservation/bind TOCTOU.
runner = "scripts/run-headless-browser-e2e.mjs"
replace_once(runner, "import { appendFile, mkdtemp, rm, writeFile } from 'node:fs/promises';", "import { appendFile, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';")
replace_once(runner, "import { createServer } from 'node:net';\n", "")
replace_once(runner, "const remoteDebuggingPort = await reservePort();\nconst profileDirectory", "const profileDirectory")
replace_once(runner, "`--remote-debugging-port=${String(remoteDebuggingPort)}`,", "'--remote-debugging-port=0',")
replace_once(runner, "  const page = await waitForPage(remoteDebuggingPort, targetUrl, browser, timeoutMs);", "  const remoteDebuggingPort = await waitForDevToolsPort(profileDirectory, browser, timeoutMs);\n  const page = await waitForPage(remoteDebuggingPort, targetUrl, browser, timeoutMs);")
replace_between(
    runner,
    "async function reservePort() {",
    "async function waitForPage(",
    """async function waitForDevToolsPort(profileDirectory, browserProcess, timeout) {
  const activePortPath = join(profileDirectory, 'DevToolsActivePort');
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    try {
      const contents = await readFile(activePortPath, 'utf8');
      const [portText] = contents.split(/\\r?\\n/, 1);
      const port = Number.parseInt(portText ?? '', 10);
      if (Number.isInteger(port) && port > 0 && port <= 65_535) return port;
      throw new Error(`Chrome wrote an invalid DevToolsActivePort value at ${activePortPath}.`);
    } catch (error) {
      if (!(error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT')) throw error;
    }
    await sleep(50);
  }
  throw new Error(`Timed out waiting for Chrome DevToolsActivePort in ${profileDirectory}.`);
}

async function waitForPage(""",
)

# #259: unsuccessful bool topology mutations do not invalidate caches or revisions.
runtime = "src/MachiVerseWorks.Server/SimulationRuntime.cs"
replace_once(
    runtime,
    """            var result = operation(_world);
            if (roadTopologyChanged) { _roadRevision = checked(_roadRevision + 1); _roadReadModel = null; }
            if (railwayTopologyChanged) { _railwayRevision = checked(_railwayRevision + 1); _railwayReadModel = null; }
            AdvanceObservationRevision();
            return result;""",
    """            var result = operation(_world);
            var changed = !((roadTopologyChanged || railwayTopologyChanged) && result is bool booleanResult && !booleanResult);
            if (!changed) return result;
            if (roadTopologyChanged) { _roadRevision = checked(_roadRevision + 1); _roadReadModel = null; }
            if (railwayTopologyChanged) { _railwayRevision = checked(_railwayRevision + 1); _railwayReadModel = null; }
            AdvanceObservationRevision();
            return result;""",
)
replace_once(runtime, "    public int PersonCount { get { lock (_gate) { EnsureFixtures(); return _world.PersonCount; } } }", "    public int PersonCount { get { lock (_gate) { EnsureFixtures(); return _world.PersonCount; } } }\n    internal ulong RoadRevision { get { lock (_gate) return _roadRevision; } }\n    internal ulong RailwayRevision { get { lock (_gate) return _railwayRevision; } }")

# #260: split the debug budget between water/sewer and maintain reference closure.
mapper = "src/MachiVerseWorks.Server/WaterSewerMessageMapper.cs"
text = read(mapper)
node_start = text.index("        var nodes = snapshot.WaterNodes")
pipe_start = text.index("        var pipes = snapshot.WaterPipes", node_start)
facilities_start = text.index("        var facilities = snapshot.WaterSources", pipe_start)
node_pipe_replacement = """        var servicePointCandidates = snapshot.ServicePoints
            .OrderByDescending(static item => item.WaterState)
            .ThenByDescending(static item => item.SewerState)
            .ThenBy(static item => item.Id.Value)
            .Take(MaximumDebugEntries)
            .ToArray();
        var requiredWaterNodeIds = servicePointCandidates.Select(static item => item.WaterNodeId.Value).ToHashSet();
        var requiredSewerNodeIds = servicePointCandidates.Select(static item => item.SewerNodeId.Value).ToHashSet();
        var (waterNodeBudget, sewerNodeBudget) = SplitBudget(snapshot.WaterNodes.Count, snapshot.SewerNodes.Count);
        var selectedWaterNodes = snapshot.WaterNodes
            .OrderBy(item => requiredWaterNodeIds.Contains(item.Id.Value) ? 0 : 1)
            .ThenBy(static item => item.Id.Value)
            .Take(waterNodeBudget)
            .ToArray();
        var selectedSewerNodes = snapshot.SewerNodes
            .OrderBy(item => requiredSewerNodeIds.Contains(item.Id.Value) ? 0 : 1)
            .ThenBy(static item => item.Id.Value)
            .Take(sewerNodeBudget)
            .ToArray();
        var nodes = selectedWaterNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, MapWaterNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z))
            .Concat(selectedSewerNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, MapSewerNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z)))
            .ToArray();

        var waterNodeIds = selectedWaterNodes.Select(static item => item.Id.Value).ToHashSet();
        var sewerNodeIds = selectedSewerNodes.Select(static item => item.Id.Value).ToHashSet();
        var waterPipeCandidates = snapshot.WaterPipes
            .Where(item => waterNodeIds.Contains(item.FromNodeId.Value) && waterNodeIds.Contains(item.ToNodeId.Value))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        var sewerPipeCandidates = snapshot.SewerPipes
            .Where(item => sewerNodeIds.Contains(item.FromNodeId.Value) && sewerNodeIds.Contains(item.ToNodeId.Value))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        var (waterPipeBudget, sewerPipeBudget) = SplitBudget(waterPipeCandidates.Length, sewerPipeCandidates.Length);
        var pipes = waterPipeCandidates.Take(waterPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService))
            .Concat(sewerPipeCandidates.Take(sewerPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService)))
            .ToArray();

"""
write(mapper, text[:node_start] + node_pipe_replacement + text[facilities_start:])
replace_once(
    mapper,
    "        var servicePoints = snapshot.ServicePoints\n",
    "        var servicePoints = servicePointCandidates\n            .Where(item => waterNodeIds.Contains(item.WaterNodeId.Value) && sewerNodeIds.Contains(item.SewerNodeId.Value))\n",
)
replace_once(
    mapper,
    "    private static ProtocolUtilityNodeKind MapWaterNodeKind(WaterNodeKind kind) => kind switch",
    """    private static (int First, int Second) SplitBudget(int firstCount, int secondCount)
    {
        var first = Math.Min(firstCount, MaximumDebugEntries / 2);
        var second = Math.Min(secondCount, MaximumDebugEntries / 2);
        var remaining = MaximumDebugEntries - first - second;
        var firstExtra = Math.Min(Math.Max(0, firstCount - first), remaining);
        first += firstExtra;
        remaining -= firstExtra;
        second += Math.Min(Math.Max(0, secondCount - second), remaining);
        return (first, second);
    }

    private static ProtocolUtilityNodeKind MapWaterNodeKind(WaterNodeKind kind) => kind switch""",
)

# #289: eviction queue entries retain the identity of the concrete Lazy they represent.
cache = "src/MachiVerseWorks.Server/ObservationCache.cs"
replace_once(cache, "    private readonly ConcurrentQueue<EncodedObservationCacheKey> _encodedOrder = new();", "    private readonly ConcurrentQueue<(EncodedObservationCacheKey Key, Lazy<byte[]> Entry)> _encodedOrder = new();")
replace_once(cache, "                _encodedOrder.Enqueue(key);", "                _encodedOrder.Enqueue((key, actual));")
replace_once(cache, "            if (!TryRemoveEncoded(oldest, out _)) continue;", "            if (!RemoveEncodedExact(oldest.Key, oldest.Entry)) continue;")
text = read(cache)
method_start = text.index("    private bool TryRemoveEncoded(EncodedObservationCacheKey key, out Lazy<byte[]>? removed)")
method_end = text.index("    private void ReleaseEncodedAccounting", method_start)
write(cache, text[:method_start] + text[method_end:])
replace_once(cache, "        private readonly ConcurrentQueue<TKey> _order = new();", "        private readonly ConcurrentQueue<(TKey Key, Lazy<object> Entry)> _order = new();")
replace_once(cache, "            if (added) _order.Enqueue(key);", "            if (added) _order.Enqueue((key, actual));")
replace_once(cache, "            while (_order.TryDequeue(out var key))\n                if (_entries.TryRemove(key, out _)) return true;", "            while (_order.TryDequeue(out var oldest))\n                if (RemoveExact(_entries, oldest.Key, oldest.Entry)) return true;")

# #254 + #304: mandatory Web quality scripts and Dependency Review are part of the required ci-gate.
ci = ".github/workflows/ci.yml"
replace_once(ci, "        run: npm run lint --if-present", "        run: npm run lint")
replace_once(ci, "        run: npm run typecheck --if-present", "        run: npm run typecheck")
replace_once(ci, "        run: npm test --if-present", "        run: npm test")
replace_once(
    ci,
    """      - name: Validate Markdown links
        run: python scripts/check-markdown-links.py""",
    """      - name: Validate required Web quality scripts
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
        run: python scripts/check-markdown-links.py""",
)
dependency_job = """  dependency_review:
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

"""
replace_once(ci, "  e2e:\n    name: required e2e", dependency_job + "  e2e:\n    name: required e2e")
replace_once(ci, "    needs: [repository, detect, dotnet, web, e2e]", "    needs: [repository, detect, dotnet, web, dependency_review, e2e]")
replace_once(ci, "          E2E_RESULT: ${{ needs.e2e.result }}", "          DEPENDENCY_REVIEW_RESULT: ${{ needs.dependency_review.result }}\n          E2E_RESULT: ${{ needs.e2e.result }}")
replace_once(ci, "          for entry in \"dotnet:$DOTNET_RESULT\" \"web:$WEB_RESULT\" \"e2e:$E2E_RESULT\"; do", "          for entry in \"dotnet:$DOTNET_RESULT\" \"web:$WEB_RESULT\" \"dependency-review:$DEPENDENCY_REVIEW_RESULT\" \"e2e:$E2E_RESULT\"; do")
Path(".github/workflows/dependency-review.yml").unlink()
docs = "docs/development/repository-settings.md"
replace_once(docs, "- required status checkとして `CI / ci-gate` を指定する。", "- required status checkとして `CI / ci-gate` を指定する。`ci-gate` は通常CI・対象PRのE2E・Dependency Review（high以上でfail）を集約する。")
replace_once(docs, "2. PRで `CI / ci-gate` がrequired checkとして認識される。", "2. PRで `CI / ci-gate` がrequired checkとして認識され、Dependency Review失敗も `ci-gate` の失敗へ反映される。")

# #253: build/install once, share the exact build outputs with every E2E matrix scenario.
prepare = Path("scripts/prepare-e2e.sh")
prepare.write_text("""#!/usr/bin/env bash
set -euo pipefail
E2E_ROOT_DIR=\"$(cd \"$(dirname \"${BASH_SOURCE[0]}\")/..\" && pwd)\"
if [[ \"${MVW_E2E_PREPARED:-0}\" != \"1\" ]]; then
  dotnet restore \"$E2E_ROOT_DIR/MachiVerseWorks.slnx\"
  dotnet build \"$E2E_ROOT_DIR/MachiVerseWorks.slnx\" --configuration Release --no-restore
  npm --prefix \"$E2E_ROOT_DIR/src/web\" ci
  npm --prefix \"$E2E_ROOT_DIR/src/web\" run build
fi
""", encoding="utf-8")

prep_pattern = re.compile(
    r'^dotnet restore "\$ROOT_DIR/MachiVerseWorks\.slnx"[^\n]*\n'
    r'^dotnet build "\$ROOT_DIR/MachiVerseWorks\.slnx"[^\n]*\n'
    r'^npm --prefix "\$ROOT_DIR/src/web" ci[^\n]*\n'
    r'^npm --prefix "\$ROOT_DIR/src/web" run build[^\n]*$',
    re.MULTILINE,
)
patched_scripts = 0
for script in sorted(Path("scripts").glob("run-*-e2e.sh")):
    text = script.read_text(encoding="utf-8")
    updated, count = prep_pattern.subn('source "$ROOT_DIR/scripts/prepare-e2e.sh"', text, count=1)
    if count == 1:
        script.write_text(updated, encoding="utf-8")
        patched_scripts += 1
if patched_scripts < 19:
    raise SystemExit(f"expected at least 19 E2E scripts to use the shared preparation, patched {patched_scripts}")

e2e = ".github/workflows/e2e.yml"
prepare_job = """  prepare:
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
          printf '%s\\n' \"$GITHUB_SHA\" > .e2e-prepared-commit
          tar -cf .artifacts/e2e-prepared.tar .e2e-prepared-commit src/*/bin src/*/obj src/web/node_modules src/web/dist
      - name: Upload shared E2E inputs
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7
        with:
          name: e2e-prepared-${{ github.sha }}
          path: .artifacts/e2e-prepared.tar
          if-no-files-found: error
          retention-days: 1

"""
replace_once(e2e, "jobs:\n  e2e:", "jobs:\n" + prepare_job + "  e2e:")
replace_once(e2e, "  e2e:\n    name: ${{ matrix.name }}", "  e2e:\n    name: ${{ matrix.name }}\n    needs: prepare")
replace_once(
    e2e,
    """          cache-dependency-path: src/web/package-lock.json
      - name: Run end-to-end
        run: bash "${{ matrix.script }}"""",
    """          cache-dependency-path: src/web/package-lock.json
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
        run: bash "${{ matrix.script }}"""",
)

# Focused regression tests.
Path("src/web/tests/application-lifecycle.test.mjs").write_text("""import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('Application guards a single animation loop and cannot reschedule after dispose', async () => {
  const source = await readFile(new URL('../src/application.ts', import.meta.url), 'utf8');
  assert.match(source, /if \\(this\\.started\\) return;/);
  assert.match(source, /this\\.started = false; this\\.disposed = true;/);
  assert.match(source, /if \\(this\\.disposed \\|\\| !this\\.started\\) return;/);
});
""", encoding="utf-8")
Path("src/web/tests/localizer-number.test.mjs").write_text("""import test from 'node:test';
import assert from 'node:assert/strict';
import { Localizer } from '../src/localization.ts';

test('Localizer formats numbers with its configured locale', () => {
  const localizer = new Localizer('en-US', { detail: 'Revenue {revenue}' });
  assert.equal(localizer.t('detail', { revenue: localizer.formatNumber(1234567) }), 'Revenue 1,234,567');
});
""", encoding="utf-8")

obs_test = "tests/MachiVerseWorks.Server.Tests/ObservationCacheTests.cs"
replace_once(
    obs_test,
    "    private sealed record CacheValue(int Value);",
    """    [TestMethod]
    public void FailedTopologyMutationDoesNotAdvanceTopologyOrObservationRevision()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [\"Simulation:InitialAgentCount\"] = \"0\", [\"Simulation:TickRate\"] = \"1\" })
            .Build();
        var runtime = new SimulationRuntime(ServerOptions.Load(configuration), configuration);
        var roadRevision = runtime.RoadRevision;
        var observationRevision = runtime.ObservationRevision;

        var removed = runtime.Mutate(static world => world.RemoveRoadNode(new RoadNodeId(999999)), roadTopologyChanged: true);

        Assert.IsFalse(removed);
        Assert.AreEqual(roadRevision, runtime.RoadRevision);
        Assert.AreEqual(observationRevision, runtime.ObservationRevision);
    }

    private sealed record CacheValue(int Value);""",
)

print(f"Patched {patched_scripts} E2E scenario scripts.")
