from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    (ROOT / path).write_text(text, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    if old not in text:
        raise RuntimeError(f"Expected text not found in {path}: {old[:120]!r}")
    write(path, text.replace(old, new, 1))


# Merge the parent branch's streaming/atomic persistence improvements with worker-3's
# cancellation, save-new, and post-load reconnect semantics.
admin_path = "src/MachiVerseWorks.Server/AdminCommandExecutorV2.cs"
admin = read(admin_path)
start = admin.index("    private async Task<AdminCommandResult> WorldAsync")
end = admin.index("    private AdminCommandResult RailwayMutate", start)
merged_world = r'''    private async Task<AdminCommandResult> WorldAsync(AdminCommand command, CancellationToken cancellationToken)
    {
        var action = Action(command, "world");
        var path = Path.GetFullPath(Arg(command, 1, "path"));
        if (Eq(action, "save") || Eq(action, "save-new"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detached = SimulationWorld.RestoreCheckpoint(simulation.CaptureCheckpoint());
            cancellationToken.ThrowIfCancellationRequested();
            var data = WorldSaveSerializer.Serialize(detached);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await WriteWorldSaveAtomicallyAsync(path, data, overwrite: Eq(action, "save"), cancellationToken);
            }
            catch (IOException) when (Eq(action, "save-new") && File.Exists(path))
            {
                return new AdminCommandResult(AdminCommandResultCode.Conflict, $"World save '{path}' already exists.");
            }
            return AdminCommandResult.Ok($"World saved to '{path}'.");
        }
        if (Eq(action, "load"))
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var world = await WorldSaveSerializer.LoadAsync(stream, WorldSaveLimits.Default, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            simulation.ReplaceWorld(world);
            foreach (var connection in connections.CreateSnapshot())
            {
                connection.Abort();
                connections.Remove(connection.Id);
            }
            return AdminCommandResult.Ok($"World loaded from '{path}'.");
        }
        return InvalidAction("world", action);
    }

    private static async Task WriteWorldSaveAtomicallyAsync(
        string path,
        ReadOnlyMemory<byte> data,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Save path does not have a parent directory.");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(data, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (IOException)
            {
                // Cleanup must not hide the original save result. A later save uses a unique temp name.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup must not hide the original save result.
            }
        }
    }

'''
write(admin_path, admin[:start] + merged_world + admin[end:])

# MCP logs: keep arbitrary ILogger traffic private, but emit explicitly sanitized administration events.
remote_path = "src/MachiVerseWorks.Server/RemoteMcp.cs"
replace_once(
    remote_path,
    "internal sealed class RemoteMcpAdminGateway(AdminCommandQueue queue, RemoteMcpOptions options)\n{\n",
    "internal sealed class RemoteMcpAdminGateway(AdminCommandQueue queue, RemoteMcpOptions options, RemoteMcpLogBuffer logs)\n{\n",
)
replace_once(
    remote_path,
    "        var result = await completion.Task.WaitAsync(cancellationToken);\n        return FromAdmin(result);\n",
    "        var result = await completion.Task.WaitAsync(cancellationToken);\n"
    "        var remoteResult = FromAdmin(result);\n"
    "        logs.AddSafe(new RemoteMcpLogEntry(\n"
    "            DateTimeOffset.UtcNow,\n"
    "            remoteResult.Success ? \"Information\" : \"Warning\",\n"
    "            \"MachiVerseWorks.Server.RemoteMcp.Admin\",\n"
    "            1,\n"
    "            $\"Administration request completed with code '{remoteResult.Code}'.\"));\n"
    "        return remoteResult;\n",
)

# Observation cache queues must not retain payloads after dictionary eviction.
cache_path = "src/MachiVerseWorks.Server/ObservationCache.cs"
replace_once(
    cache_path,
    "    private readonly ConcurrentQueue<(EncodedObservationCacheKey Key, Lazy<byte[]> Entry)> _encodedOrder = new();\n",
    "    private readonly ConcurrentQueue<(EncodedObservationCacheKey Key, WeakReference<Lazy<byte[]>> Entry)> _encodedOrder = new();\n",
)
replace_once(
    cache_path,
    "                _encodedOrder.Enqueue((key, actual));\n",
    "                _encodedOrder.Enqueue((key, new WeakReference<Lazy<byte[]>>(actual)));\n",
)
replace_once(
    cache_path,
    "            if (!RemoveEncodedExact(oldest.Key, oldest.Entry)) continue;\n",
    "            if (!oldest.Entry.TryGetTarget(out var entry) || !RemoveEncodedExact(oldest.Key, entry)) continue;\n",
)
replace_once(
    cache_path,
    "        private readonly ConcurrentQueue<(TKey Key, Lazy<object> Entry)> _order = new();\n",
    "        private readonly ConcurrentQueue<(TKey Key, WeakReference<Lazy<object>> Entry)> _order = new();\n",
)
replace_once(
    cache_path,
    "            if (added) _order.Enqueue((key, actual));\n",
    "            if (added) _order.Enqueue((key, new WeakReference<Lazy<object>>(actual)));\n",
)
replace_once(
    cache_path,
    "            while (_order.TryDequeue(out var oldest))\n                if (RemoveExact(_entries, oldest.Key, oldest.Entry)) return true;\n",
    "            while (_order.TryDequeue(out var oldest))\n                if (oldest.Entry.TryGetTarget(out var entry) && RemoveExact(_entries, oldest.Key, entry)) return true;\n",
)

# Persistent regional evolution gets its own fairness lane.
scheduler_path = "src/MachiVerseWorks.Server/SnapshotDeliveryScheduler.cs"
replace_once(
    scheduler_path,
    "    WorldEnvironment = 9,\n",
    "    WorldEnvironment = 9,\n    PersistentRegionalEvolution = 10,\n",
)
replace_once(
    "src/MachiVerseWorks.Server/PersistentRegionalEvolutionPublishService.cs",
    "                        ObservationDeliveryLane.Snapshot,\n",
    "                        ObservationDeliveryLane.PersistentRegionalEvolution,\n",
)

# Water/Sewer: select service-point node pairs first and scan already ordered topology without whole-set sorting.
water_path = "src/MachiVerseWorks.Server/WaterSewerMessageMapper.cs"
water = read(water_path)
block_start = water.index("        var servicePointCandidates = snapshot.ServicePoints")
block_end = water.index("\n        var facilities =", block_start)
new_block = r'''        var servicePointCandidates = SelectServicePointCandidates(snapshot.ServicePoints);
        var requiredWaterNodeIds = servicePointCandidates.Select(static item => item.WaterNodeId.Value).ToHashSet();
        var requiredSewerNodeIds = servicePointCandidates.Select(static item => item.SewerNodeId.Value).ToHashSet();

        var selectedWaterNodes = new List<WaterNodeSnapshot>(MaximumDebugEntries);
        foreach (var item in snapshot.WaterNodes)
            if (requiredWaterNodeIds.Contains(item.Id.Value)) selectedWaterNodes.Add(item);
        var selectedSewerNodes = new List<SewerNodeSnapshot>(MaximumDebugEntries);
        foreach (var item in snapshot.SewerNodes)
            if (requiredSewerNodeIds.Contains(item.Id.Value)) selectedSewerNodes.Add(item);

        var remainingNodeBudget = Math.Max(0, MaximumDebugEntries - selectedWaterNodes.Count - selectedSewerNodes.Count);
        var availableWaterNodes = Math.Max(0, snapshot.WaterNodes.Count - selectedWaterNodes.Count);
        var availableSewerNodes = Math.Max(0, snapshot.SewerNodes.Count - selectedSewerNodes.Count);
        var (extraWaterBudget, extraSewerBudget) = SplitBudget(availableWaterNodes, availableSewerNodes, remainingNodeBudget);
        foreach (var item in snapshot.WaterNodes)
        {
            if (extraWaterBudget == 0) break;
            if (requiredWaterNodeIds.Contains(item.Id.Value)) continue;
            selectedWaterNodes.Add(item);
            extraWaterBudget--;
        }
        foreach (var item in snapshot.SewerNodes)
        {
            if (extraSewerBudget == 0) break;
            if (requiredSewerNodeIds.Contains(item.Id.Value)) continue;
            selectedSewerNodes.Add(item);
            extraSewerBudget--;
        }

        var nodes = selectedWaterNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, MapWaterNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z))
            .Concat(selectedSewerNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, MapSewerNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z)))
            .ToArray();

        var waterNodeIds = selectedWaterNodes.Select(static item => item.Id.Value).ToHashSet();
        var sewerNodeIds = selectedSewerNodes.Select(static item => item.Id.Value).ToHashSet();
        bool WaterPipeSelected(WaterPipeSnapshot item) => waterNodeIds.Contains(item.FromNodeId.Value) && waterNodeIds.Contains(item.ToNodeId.Value);
        bool SewerPipeSelected(SewerPipeSnapshot item) => sewerNodeIds.Contains(item.FromNodeId.Value) && sewerNodeIds.Contains(item.ToNodeId.Value);
        var waterPipeCount = snapshot.WaterPipes.Count(WaterPipeSelected);
        var sewerPipeCount = snapshot.SewerPipes.Count(SewerPipeSelected);
        var (waterPipeBudget, sewerPipeBudget) = SplitBudget(waterPipeCount, sewerPipeCount);
        var pipes = snapshot.WaterPipes.Where(WaterPipeSelected).Take(waterPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService))
            .Concat(snapshot.SewerPipes.Where(SewerPipeSelected).Take(sewerPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService)))
            .ToArray();
'''
water = water[:block_start] + new_block + water[block_end:]
old_service = r'''        var servicePoints = servicePointCandidates
            .Where(item => waterNodeIds.Contains(item.WaterNodeId.Value) && sewerNodeIds.Contains(item.SewerNodeId.Value))
            .OrderByDescending(static item => item.WaterState)
            .ThenByDescending(static item => item.SewerState)
            .ThenBy(static item => item.Id.Value)
            .Take(MaximumDebugEntries)
            .Select(static item => new ProtocolWaterSewerServicePoint(
'''
new_service = r'''        var servicePoints = servicePointCandidates
            .Where(item => waterNodeIds.Contains(item.WaterNodeId.Value) && sewerNodeIds.Contains(item.SewerNodeId.Value))
            .Select(static item => new ProtocolWaterSewerServicePoint(
'''
if old_service not in water:
    raise RuntimeError("Water/Sewer service-point block not found")
water = water.replace(old_service, new_service, 1)
old_split = r'''    private static (int First, int Second) SplitBudget(int firstCount, int secondCount)
    {
        var first = Math.Min(firstCount, MaximumDebugEntries / 2);
        var second = Math.Min(secondCount, MaximumDebugEntries / 2);
        var remaining = MaximumDebugEntries - first - second;
'''
new_helpers = r'''    private static IReadOnlyList<WaterSewerServicePointSnapshot> SelectServicePointCandidates(IReadOnlyList<WaterSewerServicePointSnapshot> servicePoints)
    {
        var selected = new List<WaterSewerServicePointSnapshot>(MaximumDebugEntries);
        var waterNodeIds = new HashSet<ulong>();
        var sewerNodeIds = new HashSet<ulong>();
        for (var priority = 6; priority >= 0 && selected.Count < MaximumDebugEntries; priority--)
        {
            foreach (var item in servicePoints)
            {
                if (GetServicePointPriority(item) != priority) continue;
                var addedWater = waterNodeIds.Add(item.WaterNodeId.Value);
                var addedSewer = sewerNodeIds.Add(item.SewerNodeId.Value);
                if (waterNodeIds.Count + sewerNodeIds.Count > MaximumDebugEntries)
                {
                    if (addedWater) waterNodeIds.Remove(item.WaterNodeId.Value);
                    if (addedSewer) sewerNodeIds.Remove(item.SewerNodeId.Value);
                    continue;
                }
                selected.Add(item);
                if (selected.Count == MaximumDebugEntries) break;
            }
        }
        return selected;
    }

    private static int GetServicePointPriority(WaterSewerServicePointSnapshot item) =>
        item.SewerState == SewerServiceState.Overflow ? 6
        : item.WaterState == WaterServiceState.Unavailable ? 5
        : item.SewerState == SewerServiceState.Unavailable ? 4
        : item.WaterState == WaterServiceState.Constrained ? 3
        : item.SewerState == SewerServiceState.Constrained ? 2
        : 0;

    private static (int First, int Second) SplitBudget(int firstCount, int secondCount, int totalBudget = MaximumDebugEntries)
    {
        var first = Math.Min(firstCount, totalBudget / 2);
        var second = Math.Min(secondCount, totalBudget / 2);
        var remaining = totalBudget - first - second;
'''
if old_split not in water:
    raise RuntimeError("Water/Sewer SplitBudget block not found")
water = water.replace(old_split, new_helpers, 1)
write(water_path, water)

# Radio preflight: validate and measure without allocating full frame buffers.
codec_path = "src/MachiVerseWorks.Protocol/RadioProtocolCodec.cs"
codec = read(codec_path)
radio_prefix = r'''    public static byte[] Serialize(RadioSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRadio) throw new ArgumentOutOfRangeException(nameof(version), version, "Radio messages require Protocol 2.16 or newer.");
        ValidateRadio(message);
        var payloadLength = checked(
            RadioFixedLength
            + message.Sites.Count * SiteLength
            + message.Antennas.Count * AntennaLength
            + message.Transmitters.Count * TransmitterLength
            + message.Receivers.Count * ReceiverLength
            + message.Emissions.Count * EmissionLength
            + message.Links.Count * LinkLength
            + message.ServiceAreas.Count * ServiceAreaLength);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Radio snapshot exceeds protocol payload limit.");
'''
radio_repl = r'''    public static int GetSerializedLength(RadioSnapshotMessage message, ProtocolVersion version) =>
        checked(ProtocolFrameHeader.Size + GetRadioPayloadLength(message, version));

    public static int GetSerializedLength(SpectrumSnapshotMessage message, ProtocolVersion version) =>
        checked(ProtocolFrameHeader.Size + GetSpectrumPayloadLength(message, version));

    public static byte[] Serialize(RadioSnapshotMessage message, ProtocolVersion version)
    {
        var payloadLength = GetRadioPayloadLength(message, version);
'''
if radio_prefix not in codec:
    raise RuntimeError("Radio Serialize prefix not found after parent merge")
codec = codec.replace(radio_prefix, radio_repl, 1)
spectrum_prefix = r'''    public static byte[] Serialize(SpectrumSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRadio) throw new ArgumentOutOfRangeException(nameof(version), version, "Spectrum messages require Protocol 2.16 or newer.");
        ValidateSpectrum(message);
        var payloadLength = SpectrumFixedLength;
        foreach (var band in message.Bands) payloadLength = checked(payloadLength + BandFixedLength + Utf8.GetByteCount(band.Name));
        payloadLength = checked(payloadLength + message.FrequencyBlocks.Count * FrequencyBlockLength);
        foreach (var conflict in message.Conflicts) payloadLength = checked(payloadLength + ConflictFixedLength + Utf8.GetByteCount(conflict.Reason));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Spectrum snapshot exceeds protocol payload limit.");
'''
spectrum_repl = r'''    public static byte[] Serialize(SpectrumSnapshotMessage message, ProtocolVersion version)
    {
        var payloadLength = GetSpectrumPayloadLength(message, version);
'''
if spectrum_prefix not in codec:
    raise RuntimeError("Spectrum Serialize prefix not found after parent merge")
codec = codec.replace(spectrum_prefix, spectrum_repl, 1)
marker = "    private static void ValidateRadio(RadioSnapshotMessage message)\n"
helpers = r'''    private static int GetRadioPayloadLength(RadioSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRadio) throw new ArgumentOutOfRangeException(nameof(version), version, "Radio messages require Protocol 2.16 or newer.");
        ValidateRadio(message);
        var payloadLength = checked(
            RadioFixedLength
            + message.Sites.Count * SiteLength
            + message.Antennas.Count * AntennaLength
            + message.Transmitters.Count * TransmitterLength
            + message.Receivers.Count * ReceiverLength
            + message.Emissions.Count * EmissionLength
            + message.Links.Count * LinkLength
            + message.ServiceAreas.Count * ServiceAreaLength);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Radio snapshot exceeds protocol payload limit.");
        return payloadLength;
    }

    private static int GetSpectrumPayloadLength(SpectrumSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRadio) throw new ArgumentOutOfRangeException(nameof(version), version, "Spectrum messages require Protocol 2.16 or newer.");
        ValidateSpectrum(message);
        var payloadLength = SpectrumFixedLength;
        foreach (var band in message.Bands) payloadLength = checked(payloadLength + BandFixedLength + Utf8.GetByteCount(band.Name));
        payloadLength = checked(payloadLength + message.FrequencyBlocks.Count * FrequencyBlockLength);
        foreach (var conflict in message.Conflicts) payloadLength = checked(payloadLength + ConflictFixedLength + Utf8.GetByteCount(conflict.Reason));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Spectrum snapshot exceeds protocol payload limit.");
        return payloadLength;
    }

'''
if marker not in codec:
    raise RuntimeError("Radio validation marker not found")
codec = codec.replace(marker, helpers + marker, 1)
write(codec_path, codec)
replace_once(
    "src/MachiVerseWorks.Server/RadioPublishService.cs",
    "                    _ = RadioProtocolCodec.Serialize(messages.Radio, ProtocolVersion.Current);\n                    _ = RadioProtocolCodec.Serialize(messages.Spectrum, ProtocolVersion.Current);\n",
    "                    _ = RadioProtocolCodec.GetSerializedLength(messages.Radio, ProtocolVersion.Current);\n                    _ = RadioProtocolCodec.GetSerializedLength(messages.Spectrum, ProtocolVersion.Current);\n",
)

# Camera initialization samples terrain at the actual view focus, not subscription AABB center.
app_path = "src/web/src/application.ts"
replace_once(
    app_path,
    "import { ViewNavigationController, type ViewNavigationTarget } from './view-navigation.ts';\n",
    "import { ViewNavigationController, getCameraFocusAtSimulationAltitude, type ViewNavigationTarget } from './view-navigation.ts';\n",
)
replace_once(
    app_path,
    "    const centerX = (message.minX + message.maxX) * 0.5;\n    const centerY = (message.minY + message.maxY) * 0.5;\n    const elevation = this.observation.worldEnvironment.getNearestTerrainElevation(centerX, centerY);\n",
    "    const focus = getCameraFocusAtSimulationAltitude(this.view.camera, 0);\n    if (focus === undefined) return;\n    const elevation = this.observation.worldEnvironment.getNearestTerrainElevation(focus.x, focus.y);\n",
)

# Reconnect jitter never drops below the configured minimum.
replace_once(
    "src/web/src/connection.ts",
    "  const halfDelay = cappedDelay / 2;\n  const sample = Math.min(1, Math.max(0, random()));\n  return halfDelay + (sample * halfDelay);\n",
    "  const lowerDelay = Math.max(minimumDelayMs, cappedDelay / 2);\n  const sample = Math.min(1, Math.max(0, random()));\n  return lowerDelay + (sample * (cappedDelay - lowerDelay));\n",
)
replace_once(
    "src/web/tests/connection.test.mjs",
    "test('reconnect delay applies equal jitter to capped exponential backoff', () => {\n  assert.equal(computeReconnectDelay(0, 1_000, 5_000, () => 0), 500);\n",
    "test('reconnect delay applies jitter without going below the configured minimum', () => {\n  assert.equal(computeReconnectDelay(0, 1_000, 5_000, () => 0), 1_000);\n",
)

# Remote MCP test: an allowlisted administration event is visible, arbitrary ILogger payloads are not.
remote_test = "tests/MachiVerseWorks.Server.Tests/RemoteMcpTests.cs"
replace_once(
    remote_test,
    "        await using var readClient = await CreateMcpClientAsync(host, ReadToken);\n        var result = await readClient.CallToolAsync(\"logs_query\", new Dictionary<string, object?> { [\"limit\"] = 50 }, cancellationToken: CancellationToken.None);\n",
    "        await using var readClient = await CreateMcpClientAsync(host, ReadToken);\n"
    "        var status = await readClient.CallToolAsync(\"server_status\", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None);\n"
    "        Assert.IsFalse(status.IsError is true);\n"
    "        var result = await readClient.CallToolAsync(\"logs_query\", new Dictionary<string, object?> { [\"limit\"] = 50 }, cancellationToken: CancellationToken.None);\n",
)
replace_once(
    remote_test,
    "        using var _ = JsonDocument.Parse(message);\n        Assert.IsFalse(message.Contains(\"entry-\", StringComparison.Ordinal));\n",
    "        using var document = JsonDocument.Parse(message);\n"
    "        Assert.IsTrue(document.RootElement.GetArrayLength() > 0);\n"
    "        Assert.IsTrue(message.Contains(\"MachiVerseWorks.Server.RemoteMcp.Admin\", StringComparison.Ordinal));\n"
    "        Assert.IsFalse(message.Contains(\"entry-\", StringComparison.Ordinal));\n",
)

# Water/Sewer regression: reversed service-point pairings remain representable under the node budget.
water_test_path = "tests/MachiVerseWorks.Server.Tests/WaterSewerMessageMapperTests.cs"
water_test = read(water_test_path)
insert_at = water_test.rfind("}\n")
new_test = r'''
    [TestMethod]
    public void MapperBudgetsServicePointNodePairsTogether()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2401));
        var waterNodes = Enumerable.Range(0, 512)
            .Select(index => world.CreateWaterNode(new WorldPoint(index, 0, 0), WaterNodeKind.Service))
            .ToArray();
        var sewerNodes = Enumerable.Range(0, 512)
            .Select(index => world.CreateSewerNode(new WorldPoint(index, 0, -2), SewerNodeKind.Service))
            .ToArray();
        for (var index = 0; index < 512; index++)
            world.CreateWaterSewerServicePoint(waterNodes[index], sewerNodes[511 - index], 1d);
        world.Step();

        var message = WaterSewerMessageMapper.Create(world.CreateWaterSewerSnapshot());
        var waterIds = message.Nodes.Where(static node => node.NetworkKind == ProtocolUtilityNetworkKind.Water).Select(static node => node.NodeId).ToHashSet();
        var sewerIds = message.Nodes.Where(static node => node.NetworkKind == ProtocolUtilityNetworkKind.Sewer).Select(static node => node.NodeId).ToHashSet();

        Assert.HasCount(512, message.Nodes);
        Assert.HasCount(256, message.ServicePoints);
        Assert.IsTrue(message.ServicePoints.All(item => waterIds.Contains(item.WaterNodeId) && sewerIds.Contains(item.SewerNodeId)));
    }
'''
water_test = water_test[:insert_at] + new_test + water_test[insert_at:]
write(water_test_path, water_test)

# MCP save contract and safe-log architecture documentation.
spec_path = "docs/specifications/remote-mcp-administration.md"
spec = read(spec_path)
spec = spec.replace(
    "任意pathではなく安全なslot名のみを受け取る。実pathは`<SaveDirectory>/<slot>.mvw`としてServer側で生成し、既存`world save`へmappingする。",
    "任意pathではなく安全なslot名のみを受け取る。実pathは`<SaveDirectory>/<slot>.mvw`としてServer側で生成し、非上書きの`world save-new`へmappingする。既存slotが存在する場合は上書きせずstable `conflict`を返す。",
)
spec = spec.replace(
    "## Destructive Tool\n\n### `entity_remove`",
    "## Destructive Tool\n\n### `simulation_save_overwrite`\n\n`destructive` scopeと`confirm=true`を必須とし、`<SaveDirectory>/<slot>.mvw`へ`world save`で明示的に上書き保存する。通常の`simulation_save`から既存slotの破壊的更新を分離する。\n\n### `entity_remove`",
)
spec = spec.replace(
    "memory上のbounded log tailのみを検索する。",
    "Remote MCP境界が明示的に生成したsanitized eventだけを保持するmemory上のbounded log tailを検索する。一般`ILogger`出力はこのtailへ自動転送しない。",
)
write(spec_path, spec)

arch_path = "docs/architecture/remote-mcp-administration.md"
arch = read(arch_path)
arch = arch.replace(
    "| destructive | `read`, `write`, `destructive` | 上記に加えて許可済みEntity remove |",
    "| destructive | `read`, `write`, `destructive` | 上記に加えてsave overwriteと許可済みEntity remove |",
)
arch = arch.replace(
    "- `entity_remove`\n",
    "- `simulation_save_overwrite`\n- `entity_remove`\n",
    1,
)
arch = arch.replace(
    "実pathは常に`Server:Mcp:SaveDirectory`配下へ生成し、既存`world save` commandへ渡す。Directory作成・アクセスの失敗はMCP adapter内でstable `io_error`へ変換する。",
    "実pathは常に`Server:Mcp:SaveDirectory`配下へ生成する。通常の`simulation_save`は`world save-new`へmappingして既存slotを上書きせず、既存時はstable `conflict`を返す。既存slotの更新は`destructive` scopeと`confirm=true`を要求する`simulation_save_overwrite`だけが`world save`へmappingする。Directory作成・アクセスの失敗はMCP adapter内でstable `io_error`へ変換する。",
)
arch = arch.replace(
    "MCP有効時だけbounded memory `ILoggerProvider`を登録する。`logs_query`はこのtailのみ検索し、filesystem上のlog fileへアクセスしない。",
    "MCP用のbounded memory log bufferは一般`ILoggerProvider`として登録しない。Remote MCP Administration境界がstable codeなどallowlist済み情報だけから生成したsanitized eventを明示的に投入し、`logs_query`はこのtailのみ検索する。operator入力、credential、任意filesystem path、一般exception textを含み得る通常Server logはMCPへ転送せず、filesystem上のlog fileにもアクセスしない。",
)
write(arch_path, arch)

# Versioning contract: worker/ordinary PRs may keep VERSION stable; explicit version changes stay strict.
version_path = "docs/development/versioning.md"
version = read(version_path)
version = version.replace(
    "- `A`: `main` 向け PR を作成するときに `+1` し、`B = 0`, `C = 0` にする。\n- `B`: `develop` 向け PR を作成するときに `+1` し、`C = 0` にする。\n- `C`: 通常の開発コミットを作成するときに `+1` する。",
    "- `A`: releaseとして`main`へ統合するversion更新で`+1`し、`B = 0`, `C = 0`にする。\n- `B`: `develop`上の統合versionを進める明示的なversion更新で`+1`し、`C = 0`にする。\n- `C`: 必要に応じた通常のversion更新で`+1`する。\n\n並行作業ブランチや通常のPull Requestごとに`VERSION`更新を強制しない。複数PRが同じbase versionから同時に分岐する場合の機械的なA/B/C更新競合を避け、versionを進める操作はintegration/release境界で明示的に行う。",
)
version = version.replace(
    "- `develop`向けPull Requestでは、baseが`A.B.C`ならPR側を厳密に`A.(B+1).0`とすること\n- `main`向けPull Requestでは、baseが`A.B.C`ならPR側を厳密に`(A+1).0.0`とすること\n- その他のPR targetでは、PR側の`VERSION`がtarget/base branchより大きいこと",
    "- PR側の`VERSION`がtarget/base branchと同一なら、通常のコード統合として許可すること\n- `develop`向けPull Requestで`VERSION`を変更する場合、baseが`A.B.C`ならPR側を厳密に`A.(B+1).0`とすること\n- `main`向けPull Requestで`VERSION`を変更する場合、baseが`A.B.C`ならPR側を厳密に`(A+1).0.0`とすること\n- その他のPR targetで`VERSION`を変更する場合、PR側の`VERSION`がtarget/base branchより大きいこと",
)
version = version.replace(
    "baseとの比較は`A`, `B`, `C`を整数tupleとして行います。`develop` / `main`では単なる増加だけでなく、上記のbranch別transitionを要求します。versionの後退・再利用・誤ったincrement種別を検出した場合はrepository jobを失敗させるため、必須`ci-gate`も失敗します。",
    "baseとの比較は`A`, `B`, `C`を整数tupleとして行います。`VERSION`が変更された`develop` / `main` PRでは単なる増加だけでなく、上記のbranch別transitionを要求します。未変更の通常PRは許可し、versionの後退・再利用・変更時の誤ったincrement種別を検出した場合はrepository jobを失敗させるため、必須`ci-gate`も失敗します。",
)
version = version.replace(
    "通常コミットの`C + 1`は運用規則として維持しますが、merge commit、bot、release運用との衝突を避けるため現時点のpush CIでは1 commitごとのpatch incrementまでは強制しません。PR境界ではtarget branchに対応するA/B transitionを必須とします。",
    "通常コミットやworker PRごとのversion更新は強制しません。並行開発との衝突を避け、integration/releaseで`VERSION`を変更した場合だけtarget branchに対応するtransitionを厳密に検証します。",
)
write(version_path, version)

agents_path = "AGENTS.md"
agents = read(agents_path)
agents = agents.replace(
    "- `A`: `main` 向け PR を作成するときに `+1` し、`B = 0`, `C = 0` にリセットする。\n- `B`: `develop` 向け PR を作成するときに `+1` し、`C = 0` にリセットする。\n- `C`: 通常のコミットを作成するときに `+1` する。",
    "- `A`: releaseとして`main`へ統合する明示的なversion更新で`+1`し、`B = 0`, `C = 0`にリセットする。\n- `B`: `develop`上の統合versionを進める明示的なversion更新で`+1`し、`C = 0`にリセットする。\n- `C`: 必要に応じた通常のversion更新で`+1`する。\n- 並行worker branchや通常PRでは`VERSION`を据え置いてよく、version更新を各PRへ機械的に要求しない。`VERSION`を変更したPRだけbranch別transitionを厳密に検証する。",
)
write(agents_path, agents)

print("worker-3 review patch applied")
