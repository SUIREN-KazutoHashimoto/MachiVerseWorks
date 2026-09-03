from pathlib import Path
import re


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8")


def replace(path: str, old: str, new: str, count: int = 1) -> None:
    text = read(path)
    actual = text.count(old)
    if actual != count:
        raise SystemExit(f"{path}: expected {count} occurrence(s), found {actual}: {old[:100]!r}")
    write(path, text.replace(old, new, count))


# #186: bounded async Save Data loading shared by Server admin load.
p = "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs"
marker = "        return Deserialize(buffer.ToArray(), limits);\n    }\n\n    private static BoundedSaveBuffer SerializeToBuffer"
addition = """        return Deserialize(buffer.ToArray(), limits);
    }

    public static Task<SimulationWorld> LoadAsync(Stream source, CancellationToken cancellationToken = default) =>
        LoadAsync(source, WorldSaveLimits.Default, cancellationToken);

    public static async Task<SimulationWorld> LoadAsync(Stream source, WorldSaveLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        if (!source.CanRead) throw new ArgumentException("Source stream must be readable.", nameof(source));
        if (source.CanSeek && source.Length - source.Position > limits.MaximumBytes)
            throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");

        using var buffer = new MemoryStream();
        var readBuffer = new byte[Math.Min(StreamReadBufferSize, limits.MaximumBytes)];
        while (true)
        {
            var read = await source.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length > limits.MaximumBytes - read)
                throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
            buffer.Write(readBuffer, 0, read);
        }
        return Deserialize(buffer.ToArray(), limits);
    }

    private static BoundedSaveBuffer SerializeToBuffer"""
replace(p, marker, addition)

# #186/#187: bounded FileStream load and same-directory atomic save replacement.
p = "src/MachiVerseWorks.Server/AdminCommandExecutorV2.cs"
text = read(p)
pattern = re.compile(r"    private async Task<AdminCommandResult> WorldAsync\(AdminCommand command, CancellationToken cancellationToken\)\n    \{.*?\n    \}\n(?=\n    private )", re.S)
match = pattern.search(text)
if not match:
    raise SystemExit("AdminCommandExecutorV2.WorldAsync block not found")
replacement = '''    private async Task<AdminCommandResult> WorldAsync(AdminCommand command, CancellationToken cancellationToken)
    {
        var action = Action(command, "world");
        var path = Path.GetFullPath(Arg(command, 1, "path"));
        if (Eq(action, "save"))
        {
            var detached = SimulationWorld.RestoreCheckpoint(simulation.CaptureCheckpoint());
            var data = WorldSaveSerializer.Serialize(detached);
            await WriteWorldSaveAtomicallyAsync(path, data, cancellationToken);
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
            simulation.ReplaceWorld(world);
            return AdminCommandResult.Ok($"World loaded from '{path}'.");
        }
        return InvalidAction("world", action);
    }

    private static async Task WriteWorldSaveAtomicallyAsync(string path, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
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
            File.Move(tempPath, path, overwrite: true);
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
write(p, text[:match.start()] + replacement + text[match.end():])

# #189: one canonical current Web protocol version (current branch is Protocol 2.19).
replace(
    "src/web/src/protocol.ts",
    "export const CURRENT_PROTOCOL_VERSION = Object.freeze({ major: 2, minor: 4 });",
    "export const CURRENT_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 19 });",
)
p = "src/web/src/person-inspection-protocol.ts"
replace(
    p,
    "export const WEB_CURRENT_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 19 });",
    "export { CURRENT_PROTOCOL_VERSION as WEB_CURRENT_PROTOCOL_VERSION } from './protocol.ts';",
)

# #199: restored Radio antennas must satisfy the same invariants as CreateRadioAntenna.
p = "src/MachiVerseWorks.Simulation/SimulationWorld.Radio.Persistence.cs"
old = """            var orientationLength = Math.Sqrt((antenna.Orientation.X * antenna.Orientation.X) + (antenna.Orientation.Y * antenna.Orientation.Y) + (antenna.Orientation.Z * antenna.Orientation.Z));
            if (orientationLength <= 1e-12 || !double.IsFinite(antenna.GainDb) || !double.IsFinite(antenna.BeamwidthDegrees) || antenna.BeamwidthDegrees <= 0d || antenna.BeamwidthDegrees > 360d || !double.IsFinite(antenna.FrontToBackRatioDb) || antenna.FrontToBackRatioDb < 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));"""
new = """            var orientationLength = Math.Sqrt((antenna.Orientation.X * antenna.Orientation.X) + (antenna.Orientation.Y * antenna.Orientation.Y) + (antenna.Orientation.Z * antenna.Orientation.Z));
            if (orientationLength <= 1e-12 || Math.Abs(orientationLength - 1d) > 1e-9
                || !double.IsFinite(antenna.GainDb) || !double.IsFinite(antenna.BeamwidthDegrees) || antenna.BeamwidthDegrees <= 0d || antenna.BeamwidthDegrees > 360d
                || !double.IsFinite(antenna.FrontToBackRatioDb) || antenna.FrontToBackRatioDb < 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            if (antenna.PatternKind == RadioAntennaPatternKind.Omnidirectional && antenna.BeamwidthDegrees != 360d)
                throw new ArgumentException("Omnidirectional Radio antennas must use a 360 degree beamwidth.", nameof(checkpoint));"""
replace(p, old, new)

# #214: enumerate households themselves when migrating checkpoints without Economy state.
replace(
    "src/MachiVerseWorks.Simulation/Internal/PopulationStore.cs",
    "    public PersonState GetPersonAt(int index) => persons[index];",
    "    public HouseholdState GetHouseholdAt(int index) => households[index];\n    public PersonState GetPersonAt(int index) => persons[index];",
)
replace(
    "src/MachiVerseWorks.Simulation/SimulationWorld.Economy.cs",
    "                EnsureHouseholdEconomyState(_population.GetPersonAt(index).HouseholdId);",
    "                EnsureHouseholdEconomyState(_population.GetHouseholdAt(index).Id);",
)

# #226: post-materialization Radio limits.
p = "src/MachiVerseWorks.Persistence/WorldSaveSerializer.Economy.cs"
replace(
    p,
    "        ValidateOpticalCheckpointWithinLimits(economy.Optical, limits);\n        ValidateWorldEnvironmentCheckpointWithinLimits",
    "        ValidateOpticalCheckpointWithinLimits(economy.Optical, limits);\n        ValidateRadioCheckpointWithinLimits(economy.Radio, limits);\n        ValidateWorldEnvironmentCheckpointWithinLimits",
)
text = read(p)
insert_before = "    private static void ValidateWorldEnvironmentCheckpointWithinLimits"
radio_validator = '''    private static void ValidateRadioCheckpointWithinLimits(RadioCheckpoint? radio, WorldSaveLimits limits)
    {
        if (radio is null) return;
        ValidateCount(radio.Sites.Count, limits.MaximumInfrastructureSiteCount, "RadioSites");
        ValidateCount(radio.Bands.Count, limits.MaximumInfrastructureNodeCount, "SpectrumBands");
        ValidateCount(radio.FrequencyBlocks.Count, limits.MaximumInfrastructureSegmentCount, "FrequencyBlocks");
        ValidateCount(radio.Links.Count, limits.MaximumInfrastructureConnectionCount, "RadioLinks");
        ValidateCount(radio.Peers.Count, limits.MaximumPersonCount, "RadioPeers");
        ValidateCount((radio.Antennas ?? []).Count, limits.MaximumInfrastructureSiteCount, "RadioAntennas");
        ValidateCount((radio.Transmitters ?? []).Count, limits.MaximumInfrastructureSiteCount, "RadioTransmitters");
        ValidateCount((radio.Receivers ?? []).Count, limits.MaximumInfrastructureSiteCount, "RadioReceivers");
        ValidateCount((radio.Emissions ?? []).Count, limits.MaximumInfrastructureSegmentCount, "RadioEmissions");
        ValidateCount((radio.SiteInfrastructure ?? []).Count, limits.MaximumInfrastructureSiteCount, "RadioSiteInfrastructure");
        ValidateCount((radio.LinkEntityBindings ?? []).Count, limits.MaximumInfrastructureConnectionCount, "RadioLinkEntityBindings");
    }

'''
if text.count(insert_before) != 1:
    raise SystemExit("Economy limits insertion marker mismatch")
write(p, text.replace(insert_before, radio_validator + insert_before, 1))

# #226: pre-materialization Optical and Radio collection limits.
p = "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs"
text = read(p)
old = '''            if (reader.ValueTextEquals("gas")) return NestedSaveProperty.Gas;
            if (reader.ValueTextEquals("worldEnvironment"))'''
new = '''            if (reader.ValueTextEquals("gas")) return NestedSaveProperty.Gas;
            if (reader.ValueTextEquals("optical")) return NestedSaveProperty.Optical;
            if (reader.ValueTextEquals("radio")) return NestedSaveProperty.Radio;
            if (reader.ValueTextEquals("worldEnvironment"))'''
if text.count(old) != 1:
    raise SystemExit("Economy nested-domain marker mismatch")
text = text.replace(old, new, 1)
world_environment_marker = '''        else if (context == NestedSaveContext.WorldEnvironment)
        {'''
optical_radio_props = '''        else if (context == NestedSaveContext.Optical)
        {
            if (reader.ValueTextEquals("nodes")) return NestedSaveProperty.OpticalNodes;
            if (reader.ValueTextEquals("fiberCables")) return NestedSaveProperty.FiberCables;
            if (reader.ValueTextEquals("equipment")) return NestedSaveProperty.OpticalEquipment;
            if (reader.ValueTextEquals("backhauls")) return NestedSaveProperty.OpticalBackhauls;
            if (reader.ValueTextEquals("demands")) return NestedSaveProperty.OpticalDemands;
        }
        else if (context == NestedSaveContext.Radio)
        {
            if (reader.ValueTextEquals("sites")) return NestedSaveProperty.RadioSites;
            if (reader.ValueTextEquals("bands")) return NestedSaveProperty.RadioBands;
            if (reader.ValueTextEquals("frequencyBlocks")) return NestedSaveProperty.RadioFrequencyBlocks;
            if (reader.ValueTextEquals("links")) return NestedSaveProperty.RadioLinks;
            if (reader.ValueTextEquals("peers")) return NestedSaveProperty.RadioPeers;
            if (reader.ValueTextEquals("antennas")) return NestedSaveProperty.RadioAntennas;
            if (reader.ValueTextEquals("transmitters")) return NestedSaveProperty.RadioTransmitters;
            if (reader.ValueTextEquals("receivers")) return NestedSaveProperty.RadioReceivers;
            if (reader.ValueTextEquals("emissions")) return NestedSaveProperty.RadioEmissions;
            if (reader.ValueTextEquals("siteInfrastructure")) return NestedSaveProperty.RadioSiteInfrastructure;
            if (reader.ValueTextEquals("linkEntityBindings")) return NestedSaveProperty.RadioLinkEntityBindings;
        }
        else if (context == NestedSaveContext.WorldEnvironment)
        {'''
if text.count(world_environment_marker) != 1:
    raise SystemExit("nested property insertion marker mismatch")
text = text.replace(world_environment_marker, optical_radio_props, 1)
old = '''            (NestedSaveContext.Economy, NestedSaveProperty.Gas) => NestedSaveContext.Gas,
            (NestedSaveContext.Economy, NestedSaveProperty.WorldEnvironment)'''
new = '''            (NestedSaveContext.Economy, NestedSaveProperty.Gas) => NestedSaveContext.Gas,
            (NestedSaveContext.Economy, NestedSaveProperty.Optical) => NestedSaveContext.Optical,
            (NestedSaveContext.Economy, NestedSaveProperty.Radio) => NestedSaveContext.Radio,
            (NestedSaveContext.Economy, NestedSaveProperty.WorldEnvironment)'''
if text.count(old) != 1:
    raise SystemExit("nested object context marker mismatch")
text = text.replace(old, new, 1)
rule_marker = "            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) =>"
rules = '''            (NestedSaveContext.Optical, NestedSaveProperty.OpticalNodes) => new(limits.MaximumInfrastructureNodeCount, "simulation.economy.optical.nodes", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.FiberCables) => new(limits.MaximumInfrastructureSegmentCount, "simulation.economy.optical.fiberCables", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalEquipment) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.equipment", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalBackhauls) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.backhauls", NestedArrayKind.None),
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalDemands) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.demands", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioSites) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.sites", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioBands) => new(limits.MaximumInfrastructureNodeCount, "simulation.economy.radio.bands", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioFrequencyBlocks) => new(limits.MaximumInfrastructureSegmentCount, "simulation.economy.radio.frequencyBlocks", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioLinks) => new(limits.MaximumInfrastructureConnectionCount, "simulation.economy.radio.links", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioPeers) => new(limits.MaximumPersonCount, "simulation.economy.radio.peers", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioAntennas) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.antennas", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioTransmitters) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.transmitters", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioReceivers) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.receivers", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioEmissions) => new(limits.MaximumInfrastructureSegmentCount, "simulation.economy.radio.emissions", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioSiteInfrastructure) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.siteInfrastructure", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioLinkEntityBindings) => new(limits.MaximumInfrastructureConnectionCount, "simulation.economy.radio.linkEntityBindings", NestedArrayKind.None),
'''
if text.count(rule_marker) != 1:
    raise SystemExit("nested rule marker mismatch")
text = text.replace(rule_marker, rules + rule_marker, 1)
old = "Economy, Logistics, Power, WaterSewer, Gas, WorldEnvironment, GeographicFeature,"
new = "Economy, Logistics, Power, WaterSewer, Gas, Optical, Radio, WorldEnvironment, GeographicFeature,"
if text.count(old) != 1:
    raise SystemExit("nested context enum marker mismatch")
text = text.replace(old, new, 1)
old = '''        Gas, GasNodes, GasPipelines, GasSources, GasImportTerminals, GasStorages, GasServicePoints,
        WorldEnvironment,'''
new = '''        Gas, GasNodes, GasPipelines, GasSources, GasImportTerminals, GasStorages, GasServicePoints,
        Optical, OpticalNodes, FiberCables, OpticalEquipment, OpticalBackhauls, OpticalDemands,
        Radio, RadioSites, RadioBands, RadioFrequencyBlocks, RadioLinks, RadioPeers, RadioAntennas, RadioTransmitters, RadioReceivers, RadioEmissions, RadioSiteInfrastructure, RadioLinkEntityBindings,
        WorldEnvironment,'''
if text.count(old) != 1:
    raise SystemExit("nested property enum marker mismatch")
text = text.replace(old, new, 1)
write(p, text)

# #227: current expanded Economy schema gets a new format number; format 11 remains readable.
replace(
    "src/MachiVerseWorks.Persistence/SaveFormatVersion.cs",
    "    public const int Economy = 11;\n    public const int Current = Economy;",
    "    public const int Economy = 11;\n    public const int EconomyExtensions = 12;\n    public const int Current = EconomyExtensions;",
)
replace(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.cs",
    "SaveFormatVersion.MultimodalTransit or SaveFormatVersion.Economy))",
    "SaveFormatVersion.MultimodalTransit or SaveFormatVersion.Economy or SaveFormatVersion.EconomyExtensions))",
)
p = "src/MachiVerseWorks.Persistence/README.md"
text = read(p)
note = "Save format versioning: adding or changing authoritative persisted schema requires a new `SaveFormatVersion`; format 11 is retained as a legacy Economy-family input, while current saves are written with the expanded-schema version."
if note not in text:
    write(p, text.rstrip() + "\n\n" + note + "\n")

# #228: Agent stable ID 0 is invalid on both encode and decode.
p = "src/MachiVerseWorks.Protocol/ProtocolCodec.cs"
replace(
    p,
    "            case AgentRemoveMessage agentRemove:\n                WriteUInt64(payload, agentRemove.AgentId);",
    "            case AgentRemoveMessage agentRemove:\n                ValidateStableId(agentRemove.AgentId, nameof(message));\n                WriteUInt64(payload, agentRemove.AgentId);",
)
replace(
    p,
    "    private static void WriteAgent(Span<byte> payload, ulong id, double x, double y, double z, double velocityX, double velocityY, double velocityZ, ulong tick)\n    {\n        ValidateFinite(x, nameof(x));",
    "    private static void WriteAgent(Span<byte> payload, ulong id, double x, double y, double z, double velocityX, double velocityY, double velocityZ, ulong tick)\n    {\n        ValidateStableId(id, nameof(id));\n        ValidateFinite(x, nameof(x));",
)
replace(
    p,
    "            case MessageType.AgentRemove:\n                if (payload.Length != AgentRemovePayloadLength) return InvalidPayload(out message, out error);\n                message = new AgentRemoveMessage(ReadUInt64(payload), ReadUInt64(payload[8..]));",
    "            case MessageType.AgentRemove:\n                if (payload.Length != AgentRemovePayloadLength || ReadUInt64(payload) == 0) return InvalidPayload(out message, out error);\n                message = new AgentRemoveMessage(ReadUInt64(payload), ReadUInt64(payload[8..]));",
)
replace(
    p,
    "        var id = ReadUInt64(payload);\n        var x = ReadDouble(payload[8..]);",
    "        var id = ReadUInt64(payload);\n        if (id == 0) return InvalidPayload(out message, out error);\n        var x = ReadDouble(payload[8..]);",
)

# #229: validate the payload budget before every Protocol frame allocation.
p = "src/MachiVerseWorks.Protocol/ProtocolFrame.cs"
text = read(p)
marker = "    internal static void Write(Span<byte> destination, ProtocolFrameHeader header)"
helper = '''    internal static int GetFrameLength(int payloadLength)
    {
        if (payloadLength < 0 || (uint)payloadLength > MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payloadLength), payloadLength, $"Protocol payload must be between 0 and {MaxPayloadLength} bytes.");
        return checked(Size + payloadLength);
    }

'''
if text.count(marker) != 1:
    raise SystemExit("ProtocolFrame helper marker mismatch")
write(p, text.replace(marker, helper + marker, 1))
protocol_dir = Path("src/MachiVerseWorks.Protocol")
changed_allocations = 0
for path in protocol_dir.glob("*ProtocolCodec.cs"):
    text = path.read_text(encoding="utf-8")
    old = "new byte[ProtocolFrameHeader.Size + payloadLength]"
    count = text.count(old)
    if count:
        path.write_text(text.replace(old, "new byte[ProtocolFrameHeader.GetFrameLength(payloadLength)]"), encoding="utf-8")
        changed_allocations += count
if changed_allocations < 5:
    raise SystemExit(f"Expected to harden at least 5 Protocol codec allocations, changed {changed_allocations}")

# #230: reject non-canonical boolean bytes before they are normalized to bool.
p = "src/MachiVerseWorks.Protocol/PowerProtocolCodec.cs"
old = """        for (var index = 0; index < lines.Length; index++)
        {
            var value = ReadLine(payload.Slice(offset, LinePayloadLength));
            if (!IsValidLine(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }"""
new = """        for (var index = 0; index < lines.Length; index++)
        {
            var raw = payload.Slice(offset, LinePayloadLength);
            if (raw[32] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; }
            var value = ReadLine(raw);
            if (!IsValidLine(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }"""
replace(p, old, new)

p = "src/MachiVerseWorks.Protocol/OpticalProtocolCodec.cs"
old = """        var nodes = new ProtocolOpticalNode[nc]; for (var i = 0; i < nodes.Length; i++) { nodes[i] = ReadNode(p.Slice(offset, NodeLength)); offset += NodeLength; }
        var cables = new ProtocolFiberCable[cc]; for (var i = 0; i < cables.Length; i++) { cables[i] = ReadCable(p.Slice(offset, CableLength)); offset += CableLength; }
        var equipment = new ProtocolOpticalEquipment[ec]; for (var i = 0; i < equipment.Length; i++) { equipment[i] = ReadEquipment(p.Slice(offset, EquipmentLength)); offset += EquipmentLength; }
        var backhauls = new ProtocolOpticalBackhaul[bc]; for (var i = 0; i < backhauls.Length; i++) { backhauls[i] = ReadBackhaul(p.Slice(offset, BackhaulLength)); offset += BackhaulLength; }
        var demands = new ProtocolOpticalDemand[dc]; for (var i = 0; i < demands.Length; i++) { demands[i] = ReadDemand(p.Slice(offset, DemandLength)); offset += DemandLength; }"""
new = """        var nodes = new ProtocolOpticalNode[nc]; for (var i = 0; i < nodes.Length; i++) { nodes[i] = ReadNode(p.Slice(offset, NodeLength)); offset += NodeLength; }
        var cables = new ProtocolFiberCable[cc]; for (var i = 0; i < cables.Length; i++) { var raw = p.Slice(offset, CableLength); if (raw[48] > 1 || raw[49] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; } cables[i] = ReadCable(raw); offset += CableLength; }
        var equipment = new ProtocolOpticalEquipment[ec]; for (var i = 0; i < equipment.Length; i++) { var raw = p.Slice(offset, EquipmentLength); if (raw[41] > 1 || raw[42] > 1 || raw[43] > 1 || raw[44] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; } equipment[i] = ReadEquipment(raw); offset += EquipmentLength; }
        var backhauls = new ProtocolOpticalBackhaul[bc]; for (var i = 0; i < backhauls.Length; i++) { var raw = p.Slice(offset, BackhaulLength); if (raw[40] > 1 || raw[41] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; } backhauls[i] = ReadBackhaul(raw); offset += BackhaulLength; }
        var demands = new ProtocolOpticalDemand[dc]; for (var i = 0; i < demands.Length; i++) { demands[i] = ReadDemand(p.Slice(offset, DemandLength)); offset += DemandLength; }"""
replace(p, old, new)

p = "src/MachiVerseWorks.Protocol/RadioProtocolCodec.cs"
old = """        var sites = new ProtocolRadioSite[siteCount];
        for (var i = 0; i < sites.Length; i++) { sites[i] = ReadSite(payload.Slice(offset, SiteLength)); offset += SiteLength; }
        var antennas = new ProtocolRadioAntenna[antennaCount];
        for (var i = 0; i < antennas.Length; i++) { antennas[i] = ReadAntenna(payload.Slice(offset, AntennaLength)); offset += AntennaLength; }
        var transmitters = new ProtocolRadioTransmitter[transmitterCount];
        for (var i = 0; i < transmitters.Length; i++) { transmitters[i] = ReadTransmitter(payload.Slice(offset, TransmitterLength)); offset += TransmitterLength; }
        var receivers = new ProtocolRadioReceiver[receiverCount];
        for (var i = 0; i < receivers.Length; i++) { receivers[i] = ReadReceiver(payload.Slice(offset, ReceiverLength)); offset += ReceiverLength; }
        var emissions = new ProtocolRadioEmission[emissionCount];
        for (var i = 0; i < emissions.Length; i++) { emissions[i] = ReadEmission(payload.Slice(offset, EmissionLength)); offset += EmissionLength; }
        var links = new ProtocolRadioLink[linkCount];
        for (var i = 0; i < links.Length; i++) { links[i] = ReadLink(payload.Slice(offset, LinkLength)); offset += LinkLength; }"""
new = """        var sites = new ProtocolRadioSite[siteCount];
        for (var i = 0; i < sites.Length; i++) { var raw = payload.Slice(offset, SiteLength); if (raw[49] > 1) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload); sites[i] = ReadSite(raw); offset += SiteLength; }
        var antennas = new ProtocolRadioAntenna[antennaCount];
        for (var i = 0; i < antennas.Length; i++) { var raw = payload.Slice(offset, AntennaLength); if (raw[89] > 1) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload); antennas[i] = ReadAntenna(raw); offset += AntennaLength; }
        var transmitters = new ProtocolRadioTransmitter[transmitterCount];
        for (var i = 0; i < transmitters.Length; i++) { var raw = payload.Slice(offset, TransmitterLength); if (raw[32] > 1 || raw[33] > 1) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload); transmitters[i] = ReadTransmitter(raw); offset += TransmitterLength; }
        var receivers = new ProtocolRadioReceiver[receiverCount];
        for (var i = 0; i < receivers.Length; i++) { var raw = payload.Slice(offset, ReceiverLength); if (raw[48] > 1 || raw[49] > 1) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload); receivers[i] = ReadReceiver(raw); offset += ReceiverLength; }
        var emissions = new ProtocolRadioEmission[emissionCount];
        for (var i = 0; i < emissions.Length; i++) { var raw = payload.Slice(offset, EmissionLength); if (raw[56] > 1 || raw[57] > 1) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload); emissions[i] = ReadEmission(raw); offset += EmissionLength; }
        var links = new ProtocolRadioLink[linkCount];
        for (var i = 0; i < links.Length; i++) { var raw = payload.Slice(offset, LinkLength); if (raw[81] > 1) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload); links[i] = ReadLink(raw); offset += LinkLength; }"""
replace(p, old, new)

print("Batch 1 patches applied")
