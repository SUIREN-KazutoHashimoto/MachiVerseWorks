namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private const double RadioEmissionSpatialCellSizeMeters = 500d;
    private const double RadioCandidateRadiusMeters = 20_000d;
    private const double RadioBuildingPenetrationLossDb = 12d;
    private const double RadioNlosPenaltyDb = 6d;

    private readonly List<RadioAntennaState> _radioAntennas = [];
    private readonly Dictionary<RadioAntennaId, RadioAntennaState> _radioAntennaIndex = [];
    private readonly List<RadioTransmitterState> _radioTransmitters = [];
    private readonly Dictionary<RadioTransmitterId, RadioTransmitterState> _radioTransmitterIndex = [];
    private readonly List<RadioReceiverState> _radioReceivers = [];
    private readonly Dictionary<RadioReceiverId, RadioReceiverState> _radioReceiverIndex = [];
    private readonly List<RadioEmissionState> _radioEmissions = [];
    private readonly Dictionary<RadioEmissionId, RadioEmissionState> _radioEmissionIndex = [];
    private readonly Dictionary<RadioSiteId, RadioSiteInfrastructureBinding> _radioSiteInfrastructure = [];
    private readonly Dictionary<RadioLinkId, RadioLinkEntityBinding> _radioLinkEntityBindings = [];
    private readonly RadioEmissionSpatialIndex _radioEmissionSpatialIndex = new(RadioEmissionSpatialCellSizeMeters);
    private ulong _nextRadioAntennaId = 1;
    private ulong _nextRadioTransmitterId = 1;
    private ulong _nextRadioReceiverId = 1;
    private ulong _nextRadioEmissionId = 1;

    public int RadioAntennaCount => _radioAntennas.Count;
    public int RadioTransmitterCount => _radioTransmitters.Count;
    public int RadioReceiverCount => _radioReceivers.Count;
    public int RadioEmissionCount => _radioEmissions.Count;

    public RadioChannelId CreateRadioChannel(
        SpectrumBandId bandId,
        double centerFrequencyMegahertz,
        double bandwidthMegahertz)
    {
        var blockId = CreateFrequencyBlock(bandId, centerFrequencyMegahertz, bandwidthMegahertz);
        return new RadioChannelId(blockId.Value);
    }

    public bool TryGetRadioChannel(RadioChannelId id, out RadioChannel channel)
    {
        if (_frequencyBlockIndex.TryGetValue(new FrequencyBlockId(id.Value), out var block))
        {
            channel = new RadioChannel(id, block.BandId, block.CenterFrequencyMegahertz, block.BandwidthMegahertz);
            return true;
        }
        channel = default;
        return false;
    }

    public RadioAntennaId CreateRadioAntenna(
        RadioSiteId siteId,
        WorldVector positionOffset,
        WorldVector orientation,
        double gainDb,
        RadioAntennaPatternKind patternKind = RadioAntennaPatternKind.Omnidirectional,
        double beamwidthDegrees = 360d,
        double frontToBackRatioDb = 0d,
        bool isInService = true)
    {
        if (!_radioSiteIndex.ContainsKey(siteId)) throw new ArgumentException($"Radio site {siteId.Value} does not exist.", nameof(siteId));
        ValidateRadioVector(positionOffset, nameof(positionOffset));
        var normalizedOrientation = NormalizeRadioOrientation(orientation, nameof(orientation));
        if (!double.IsFinite(gainDb)) throw new ArgumentOutOfRangeException(nameof(gainDb));
        if (!Enum.IsDefined(patternKind)) throw new ArgumentOutOfRangeException(nameof(patternKind));
        if (!double.IsFinite(beamwidthDegrees) || beamwidthDegrees <= 0d || beamwidthDegrees > 360d) throw new ArgumentOutOfRangeException(nameof(beamwidthDegrees));
        if (!double.IsFinite(frontToBackRatioDb) || frontToBackRatioDb < 0d) throw new ArgumentOutOfRangeException(nameof(frontToBackRatioDb));
        if (patternKind == RadioAntennaPatternKind.Omnidirectional && beamwidthDegrees != 360d)
            throw new ArgumentException("Omnidirectional antennas must use a 360 degree beamwidth.", nameof(beamwidthDegrees));

        EnsureRadioIdCapacity(_nextRadioAntennaId, "Radio antenna");
        var id = new RadioAntennaId(_nextRadioAntennaId++);
        var state = new RadioAntennaState(id, siteId, positionOffset, normalizedOrientation, gainDb, patternKind, beamwidthDegrees, frontToBackRatioDb, isInService);
        _radioAntennas.Add(state);
        _radioAntennaIndex.Add(id, state);
        return id;
    }

    public RadioTransmitterId CreateRadioTransmitter(
        RadioSiteId siteId,
        RadioAntennaId antennaId,
        double maximumTransmitPowerDbm,
        bool isInService = true)
    {
        if (!_radioSiteIndex.ContainsKey(siteId)) throw new ArgumentException($"Radio site {siteId.Value} does not exist.", nameof(siteId));
        if (!_radioAntennaIndex.TryGetValue(antennaId, out var antenna) || antenna.SiteId != siteId)
            throw new ArgumentException("Radio transmitter antenna must exist on the same Radio site.", nameof(antennaId));
        ValidateRadioPowerDbm(maximumTransmitPowerDbm, nameof(maximumTransmitPowerDbm));
        EnsureRadioIdCapacity(_nextRadioTransmitterId, "Radio transmitter");
        var id = new RadioTransmitterId(_nextRadioTransmitterId++);
        var state = new RadioTransmitterState(id, siteId, antennaId, maximumTransmitPowerDbm, isInService);
        _radioTransmitters.Add(state);
        _radioTransmitterIndex.Add(id, state);
        return id;
    }

    public RadioReceiverId CreateRadioReceiver(
        RadioSiteId siteId,
        RadioAntennaId antennaId,
        double minimumFrequencyMegahertz,
        double maximumFrequencyMegahertz,
        double sensitivityDbm,
        bool isInService = true)
    {
        if (!_radioSiteIndex.ContainsKey(siteId)) throw new ArgumentException($"Radio site {siteId.Value} does not exist.", nameof(siteId));
        if (!_radioAntennaIndex.TryGetValue(antennaId, out var antenna) || antenna.SiteId != siteId)
            throw new ArgumentException("Radio receiver antenna must exist on the same Radio site.", nameof(antennaId));
        ValidateRadioFrequencyRange(minimumFrequencyMegahertz, maximumFrequencyMegahertz);
        if (!double.IsFinite(sensitivityDbm) || sensitivityDbm >= 0d) throw new ArgumentOutOfRangeException(nameof(sensitivityDbm));
        EnsureRadioIdCapacity(_nextRadioReceiverId, "Radio receiver");
        var id = new RadioReceiverId(_nextRadioReceiverId++);
        var state = new RadioReceiverState(id, siteId, antennaId, minimumFrequencyMegahertz, maximumFrequencyMegahertz, sensitivityDbm, isInService);
        _radioReceivers.Add(state);
        _radioReceiverIndex.Add(id, state);
        return id;
    }

    public RadioEmissionId CreateRadioEmission(
        RadioTransmitterId transmitterId,
        RadioChannelId channelId,
        double transmitPowerDbm,
        double utilization = 0d,
        bool isInService = true)
    {
        if (!_radioTransmitterIndex.TryGetValue(transmitterId, out var transmitter))
            throw new ArgumentException($"Radio transmitter {transmitterId.Value} does not exist.", nameof(transmitterId));
        if (!TryGetRadioChannel(channelId, out _)) throw new ArgumentException($"Radio channel {channelId.Value} does not exist.", nameof(channelId));
        ValidateRadioPowerDbm(transmitPowerDbm, nameof(transmitPowerDbm));
        if (transmitPowerDbm > transmitter.MaximumTransmitPowerDbm)
            throw new ArgumentOutOfRangeException(nameof(transmitPowerDbm), "Emission power cannot exceed transmitter maximum power.");
        ValidateRadioUtilization(utilization, nameof(utilization));
        EnsureRadioIdCapacity(_nextRadioEmissionId, "Radio emission");
        var id = new RadioEmissionId(_nextRadioEmissionId++);
        var state = new RadioEmissionState(id, transmitterId, channelId, transmitPowerDbm, utilization, isInService);
        _radioEmissions.Add(state);
        _radioEmissionIndex.Add(id, state);
        _radioEmissionSpatialIndex.Add(id, GetRadioTransmitterPosition(transmitter));
        RecalculateRadioPlan();
        return id;
    }

    public RadioLinkId CreateRadioLink(RadioEmissionId emissionId, RadioReceiverId receiverId, double fadeMarginDb = 6d)
    {
        if (!_radioEmissionIndex.TryGetValue(emissionId, out var emission)) throw new ArgumentException($"Radio emission {emissionId.Value} does not exist.", nameof(emissionId));
        if (!_radioReceiverIndex.TryGetValue(receiverId, out var receiver)) throw new ArgumentException($"Radio receiver {receiverId.Value} does not exist.", nameof(receiverId));
        if (!TryGetRadioChannel(emission.ChannelId, out var channel)) throw new InvalidOperationException("Radio emission references a missing channel.");
        if (!ReceiverSupportsChannel(receiver, channel)) throw new ArgumentException("Radio receiver does not support the emission channel.", nameof(receiverId));
        if (!double.IsFinite(fadeMarginDb) || fadeMarginDb < 0d) throw new ArgumentOutOfRangeException(nameof(fadeMarginDb));

        var transmitter = _radioTransmitterIndex[emission.TransmitterId];
        var txAntenna = _radioAntennaIndex[transmitter.AntennaId];
        var rxAntenna = _radioAntennaIndex[receiver.AntennaId];
        var txPosition = GetRadioAntennaPosition(txAntenna);
        var rxPosition = GetRadioAntennaPosition(rxAntenna);
        var transmitGainDb = CalculateRadioAntennaGainDb(txAntenna, rxPosition);
        var receiveGainDb = CalculateRadioAntennaGainDb(rxAntenna, txPosition);
        var budget = new RadioLinkBudget(
            new TransmitterPathBudget(new EffectiveRadiatedPower(emission.TransmitPowerDbm + transmitGainDb), 0d, 0d),
            receiveGainDb,
            receiver.SensitivityDbm,
            fadeMarginDb);
        var linkId = CreateRadioLink(transmitter.SiteId, receiver.SiteId, new FrequencyBlockId(channel.Id.Value), budget, emission.Utilization, isInService: true);
        _radioLinkEntityBindings.Add(linkId, new RadioLinkEntityBinding(linkId, emissionId, receiverId));
        RecalculateRadioPlan();
        return linkId;
    }

    public void BindRadioSiteInfrastructure(
        RadioSiteId siteId,
        BuildingId? buildingId = null,
        OpticalBackhaulId? opticalBackhaulId = null,
        bool requiresPower = true)
    {
        if (!_radioSiteIndex.ContainsKey(siteId)) throw new ArgumentException($"Radio site {siteId.Value} does not exist.", nameof(siteId));
        if (buildingId is { } building && !TryGetBuildingSnapshot(building, out _)) throw new ArgumentException($"Building {building.Value} does not exist.", nameof(buildingId));
        if (opticalBackhaulId is { } backhaul && !_opticalBackhaulIndex.ContainsKey(backhaul)) throw new ArgumentException($"Optical backhaul {backhaul.Value} does not exist.", nameof(opticalBackhaulId));
        if (requiresPower && buildingId is null) throw new ArgumentException("A power-dependent Radio site must reference a Building.", nameof(buildingId));
        _radioSiteInfrastructure[siteId] = new RadioSiteInfrastructureBinding(siteId, buildingId, opticalBackhaulId, requiresPower);
        RecalculateRadioPlan();
    }

    public void SetRadioAntennaInService(RadioAntennaId id, bool isInService)
    {
        if (!_radioAntennaIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Radio antenna {id.Value} does not exist.", nameof(id));
        item.IsInService = isInService;
        RecalculateRadioPlan();
    }

    public void SetRadioTransmitterInService(RadioTransmitterId id, bool isInService)
    {
        if (!_radioTransmitterIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Radio transmitter {id.Value} does not exist.", nameof(id));
        item.IsInService = isInService;
        RecalculateRadioPlan();
    }

    public void SetRadioReceiverInService(RadioReceiverId id, bool isInService)
    {
        if (!_radioReceiverIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Radio receiver {id.Value} does not exist.", nameof(id));
        item.IsInService = isInService;
        RecalculateRadioPlan();
    }

    public void SetRadioEmissionInService(RadioEmissionId id, bool isInService)
    {
        if (!_radioEmissionIndex.TryGetValue(id, out var item)) throw new ArgumentException($"Radio emission {id.Value} does not exist.", nameof(id));
        item.IsInService = isInService;
        RecalculateRadioPlan();
    }

    public RadioEmissionSnapshot[] QueryRadioEmissionCandidates(RadioReceiverId receiverId, double maximumDistanceMeters = RadioCandidateRadiusMeters)
    {
        if (!_radioReceiverIndex.TryGetValue(receiverId, out var receiver)) throw new ArgumentException($"Radio receiver {receiverId.Value} does not exist.", nameof(receiverId));
        if (!double.IsFinite(maximumDistanceMeters) || maximumDistanceMeters <= 0d) throw new ArgumentOutOfRangeException(nameof(maximumDistanceMeters));
        var position = GetRadioAntennaPosition(_radioAntennaIndex[receiver.AntennaId]);
        var volume = new WorldVolume(
            position.X - maximumDistanceMeters,
            position.Y - maximumDistanceMeters,
            position.Z - maximumDistanceMeters,
            position.X + maximumDistanceMeters,
            position.Y + maximumDistanceMeters,
            position.Z + maximumDistanceMeters);
        return _radioEmissionSpatialIndex.Query(volume)
            .Where(id => _radioEmissionIndex.TryGetValue(id, out var emission)
                && IsRadioEmissionOperational(emission)
                && TryGetRadioChannel(emission.ChannelId, out var channel)
                && ReceiverSupportsChannel(receiver, channel)
                && DistanceMeters(GetRadioTransmitterPosition(_radioTransmitterIndex[emission.TransmitterId]), position) <= maximumDistanceMeters)
            .Select(id => CreateRadioEmissionSnapshot(_radioEmissionIndex[id]))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
    }

    internal RadioAntennaSnapshot[] CreateRadioAntennaSnapshots() =>
        _radioAntennas.OrderBy(static item => item.Id.Value).Select(CreateRadioAntennaSnapshot).ToArray();

    internal RadioTransmitterSnapshot[] CreateRadioTransmitterSnapshots() =>
        _radioTransmitters.OrderBy(static item => item.Id.Value).Select(CreateRadioTransmitterSnapshot).ToArray();

    internal RadioReceiverSnapshot[] CreateRadioReceiverSnapshots() =>
        _radioReceivers.OrderBy(static item => item.Id.Value).Select(CreateRadioReceiverSnapshot).ToArray();

    internal RadioEmissionSnapshot[] CreateRadioEmissionSnapshots() =>
        _radioEmissions.OrderBy(static item => item.Id.Value).Select(CreateRadioEmissionSnapshot).ToArray();

    private bool IsRadioSiteInfrastructureAvailable(RadioSiteId siteId)
    {
        if (!_radioSiteInfrastructure.TryGetValue(siteId, out var binding)) return true;
        if (binding.RequiresPower && binding.BuildingId is { } buildingId && !IsBuildingPowered(buildingId)) return false;
        if (binding.OpticalBackhaulId is { } backhaulId)
        {
            if (!_opticalBackhaulIndex.TryGetValue(backhaulId, out var backhaul)) return false;
            if (!CreateOpticalBackhaulSnapshot(backhaul).IsOperational) return false;
        }
        return true;
    }

    private bool IsRadioTransmitterOperational(RadioTransmitterState transmitter) =>
        transmitter.IsInService
        && _radioSiteIndex.TryGetValue(transmitter.SiteId, out var site) && site.IsInService
        && IsRadioSiteInfrastructureAvailable(transmitter.SiteId)
        && _radioAntennaIndex.TryGetValue(transmitter.AntennaId, out var antenna) && antenna.IsInService;

    private bool IsRadioReceiverOperational(RadioReceiverState receiver) =>
        receiver.IsInService
        && _radioSiteIndex.TryGetValue(receiver.SiteId, out var site) && site.IsInService
        && IsRadioSiteInfrastructureAvailable(receiver.SiteId)
        && _radioAntennaIndex.TryGetValue(receiver.AntennaId, out var antenna) && antenna.IsInService;

    private bool IsRadioEmissionOperational(RadioEmissionState emission) =>
        emission.IsInService
        && _radioTransmitterIndex.TryGetValue(emission.TransmitterId, out var transmitter)
        && IsRadioTransmitterOperational(transmitter);

    private bool TryCreateExplicitRadioPropagationRequest(
        RadioLinkStateData link,
        double interferenceDbm,
        out RadioPropagationRequest request)
    {
        request = default;
        if (!_radioLinkEntityBindings.TryGetValue(link.Id, out var binding)
            || !_radioEmissionIndex.TryGetValue(binding.EmissionId, out var emission)
            || !_radioReceiverIndex.TryGetValue(binding.ReceiverId, out var receiver)
            || !_radioTransmitterIndex.TryGetValue(emission.TransmitterId, out var transmitter)
            || !_radioAntennaIndex.TryGetValue(transmitter.AntennaId, out var txAntenna)
            || !_radioAntennaIndex.TryGetValue(receiver.AntennaId, out var rxAntenna)
            || !TryGetRadioChannel(emission.ChannelId, out var channel)) return false;

        var txPosition = GetRadioAntennaPosition(txAntenna);
        var rxPosition = GetRadioAntennaPosition(rxAntenna);
        var txSite = CreateRadioSiteSnapshot(_radioSiteIndex[transmitter.SiteId]) with { Position = txPosition, AntennaGainDb = 0d };
        var rxSite = CreateRadioSiteSnapshot(_radioSiteIndex[receiver.SiteId]) with { Position = rxPosition, AntennaGainDb = 0d };
        var txGain = CalculateRadioAntennaGainDb(txAntenna, rxPosition);
        var rxGain = CalculateRadioAntennaGainDb(rxAntenna, txPosition);
        var budget = new RadioLinkBudget(
            new TransmitterPathBudget(new EffectiveRadiatedPower(emission.TransmitPowerDbm + txGain), 0d, 0d),
            rxGain,
            receiver.SensitivityDbm,
            link.LinkBudget.FadeMarginDb);
        var obstruction = CalculateRadioBuildingObstruction(txPosition, rxPosition);
        request = new RadioPropagationRequest(txSite, rxSite, channel.ToFrequencyBlock(), budget, interferenceDbm, RadioDefaults.ThermalNoiseFloorDbm, obstruction.LossDb, obstruction.IsLineOfSight);
        return true;
    }

    private double CalculateExplicitInterferenceDbm(RadioLinkStateData target, RadioLinkEntityBinding binding)
    {
        if (!_radioReceiverIndex.TryGetValue(binding.ReceiverId, out var receiver)) return -300d;
        var receiverAntenna = _radioAntennaIndex[receiver.AntennaId];
        var receiverPosition = GetRadioAntennaPosition(receiverAntenna);
        var targetEmission = _radioEmissionIndex[binding.EmissionId];
        var targetChannel = GetRequiredRadioChannel(targetEmission.ChannelId);
        var totalMilliwatts = 0d;
        foreach (var candidateSnapshot in QueryRadioEmissionCandidates(binding.ReceiverId))
        {
            if (candidateSnapshot.Id == binding.EmissionId) continue;
            var candidate = _radioEmissionIndex[candidateSnapshot.Id];
            var candidateChannel = GetRequiredRadioChannel(candidate.ChannelId);
            if (!RadioChannelsOverlap(targetChannel, candidateChannel)) continue;
            var transmitter = _radioTransmitterIndex[candidate.TransmitterId];
            var antenna = _radioAntennaIndex[transmitter.AntennaId];
            var txPosition = GetRadioAntennaPosition(antenna);
            var txSite = CreateRadioSiteSnapshot(_radioSiteIndex[transmitter.SiteId]) with { Position = txPosition, AntennaGainDb = 0d };
            var rxSite = CreateRadioSiteSnapshot(_radioSiteIndex[receiver.SiteId]) with { Position = receiverPosition, AntennaGainDb = 0d };
            var txGain = CalculateRadioAntennaGainDb(antenna, receiverPosition);
            var rxGain = CalculateRadioAntennaGainDb(receiverAntenna, txPosition);
            var budget = new RadioLinkBudget(new TransmitterPathBudget(new EffectiveRadiatedPower(candidate.TransmitPowerDbm + txGain), 0d, 0d), rxGain, receiver.SensitivityDbm, 0d);
            var obstruction = CalculateRadioBuildingObstruction(txPosition, receiverPosition);
            var request = new RadioPropagationRequest(txSite, rxSite, candidateChannel.ToFrequencyBlock(), budget, -300d, RadioDefaults.ThermalNoiseFloorDbm, obstruction.LossDb, obstruction.IsLineOfSight);
            var result = _radioPropagationSolver.Solve(request);
            totalMilliwatts += Math.Pow(10d, result.ReceivedPowerDbm / 10d);
        }
        return totalMilliwatts <= 0d ? -300d : 10d * Math.Log10(totalMilliwatts);
    }

    private (bool IsLineOfSight, double LossDb) CalculateRadioBuildingObstruction(WorldPoint from, WorldPoint to)
    {
        var intersections = 0;
        foreach (var building in CreateBuildingSnapshot().OrderBy(static item => item.Id.Value))
        {
            if (building.Bounds.Contains(from) || building.Bounds.Contains(to)) continue;
            if (RadioSegmentIntersectsVolume(from, to, building.Bounds)) intersections++;
        }
        return intersections == 0
            ? (true, 0d)
            : (false, RadioNlosPenaltyDb + (RadioBuildingPenetrationLossDb * intersections));
    }

    private static bool RadioSegmentIntersectsVolume(WorldPoint from, WorldPoint to, WorldVolume bounds)
    {
        var tMin = 0d;
        var tMax = 1d;
        return RadioIntersectAxis(from.X, to.X - from.X, bounds.MinX, bounds.MaxX, ref tMin, ref tMax)
            && RadioIntersectAxis(from.Y, to.Y - from.Y, bounds.MinY, bounds.MaxY, ref tMin, ref tMax)
            && RadioIntersectAxis(from.Z, to.Z - from.Z, bounds.MinZ, bounds.MaxZ, ref tMin, ref tMax);
    }

    private static bool RadioIntersectAxis(double origin, double direction, double minimum, double maximum, ref double tMin, ref double tMax)
    {
        if (Math.Abs(direction) <= 1e-12) return origin >= minimum && origin <= maximum;
        var inverse = 1d / direction;
        var first = (minimum - origin) * inverse;
        var second = (maximum - origin) * inverse;
        if (first > second) (first, second) = (second, first);
        tMin = Math.Max(tMin, first);
        tMax = Math.Min(tMax, second);
        return tMin <= tMax;
    }

    private RadioChannel GetRequiredRadioChannel(RadioChannelId id) =>
        TryGetRadioChannel(id, out var channel) ? channel : throw new InvalidOperationException($"Radio channel {id.Value} does not exist.");

    private static bool ReceiverSupportsChannel(RadioReceiverState receiver, RadioChannel channel)
    {
        var half = channel.BandwidthMegahertz / 2d;
        return channel.CenterFrequencyMegahertz - half >= receiver.MinimumFrequencyMegahertz
            && channel.CenterFrequencyMegahertz + half <= receiver.MaximumFrequencyMegahertz;
    }

    private static bool RadioChannelsOverlap(RadioChannel first, RadioChannel second) =>
        RadioValidation.FrequencyBlocksOverlap(first.ToFrequencyBlock(), second.ToFrequencyBlock());

    private WorldPoint GetRadioTransmitterPosition(RadioTransmitterState transmitter) =>
        GetRadioAntennaPosition(_radioAntennaIndex[transmitter.AntennaId]);

    private WorldPoint GetRadioAntennaPosition(RadioAntennaState antenna)
    {
        var site = _radioSiteIndex[antenna.SiteId];
        return new WorldPoint(
            site.Position.X + antenna.PositionOffset.X,
            site.Position.Y + antenna.PositionOffset.Y,
            site.Position.Z + antenna.PositionOffset.Z);
    }

    private double CalculateRadioAntennaGainDb(RadioAntennaState antenna, WorldPoint target)
    {
        if (antenna.PatternKind == RadioAntennaPatternKind.Omnidirectional) return antenna.GainDb;
        var position = GetRadioAntennaPosition(antenna);
        var dx = target.X - position.X;
        var dy = target.Y - position.Y;
        var dz = target.Z - position.Z;
        var length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        if (length <= 1e-9) return antenna.GainDb;
        var dot = ((dx / length) * antenna.Orientation.X) + ((dy / length) * antenna.Orientation.Y) + ((dz / length) * antenna.Orientation.Z);
        var angle = Math.Acos(Math.Clamp(dot, -1d, 1d)) * (180d / Math.PI);
        return angle <= antenna.BeamwidthDegrees / 2d ? antenna.GainDb : antenna.GainDb - antenna.FrontToBackRatioDb;
    }

    private RadioAntennaSnapshot CreateRadioAntennaSnapshot(RadioAntennaState item) =>
        new(item.Id, item.SiteId, item.PositionOffset, item.Orientation, item.GainDb, item.PatternKind, item.BeamwidthDegrees, item.FrontToBackRatioDb, item.IsInService);

    private RadioTransmitterSnapshot CreateRadioTransmitterSnapshot(RadioTransmitterState item) =>
        new(item.Id, item.SiteId, item.AntennaId, item.MaximumTransmitPowerDbm, item.IsInService, IsRadioTransmitterOperational(item));

    private RadioReceiverSnapshot CreateRadioReceiverSnapshot(RadioReceiverState item) =>
        new(item.Id, item.SiteId, item.AntennaId, item.MinimumFrequencyMegahertz, item.MaximumFrequencyMegahertz, item.SensitivityDbm, item.IsInService, IsRadioReceiverOperational(item));

    private RadioEmissionSnapshot CreateRadioEmissionSnapshot(RadioEmissionState item)
    {
        var channel = GetRequiredRadioChannel(item.ChannelId);
        return new RadioEmissionSnapshot(item.Id, item.TransmitterId, item.ChannelId, channel.CenterFrequencyMegahertz, channel.BandwidthMegahertz, item.TransmitPowerDbm, item.Utilization, item.IsInService, IsRadioEmissionOperational(item));
    }

    private static void ValidateRadioVector(WorldVector value, string parameterName)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z)) throw new ArgumentOutOfRangeException(parameterName);
    }

    private static WorldVector NormalizeRadioOrientation(WorldVector value, string parameterName)
    {
        ValidateRadioVector(value, parameterName);
        var length = Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
        if (length <= 1e-12) throw new ArgumentOutOfRangeException(parameterName, "Radio antenna orientation must be non-zero.");
        return new WorldVector(value.X / length, value.Y / length, value.Z / length);
    }

    private static void ValidateRadioPowerDbm(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is < -100d or > 100d) throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateRadioFrequencyRange(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || minimum <= 0d) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (!double.IsFinite(maximum) || maximum <= minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
    }

    private static void ValidateRadioUtilization(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d || value > 1d) throw new ArgumentOutOfRangeException(parameterName);
    }
}
