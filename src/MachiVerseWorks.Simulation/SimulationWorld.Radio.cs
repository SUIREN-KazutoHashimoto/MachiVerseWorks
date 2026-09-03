namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<RadioSiteState> _radioSites = [];
    private readonly Dictionary<RadioSiteId, RadioSiteState> _radioSiteIndex = [];
    private readonly List<SpectrumBand> _spectrumBands = [];
    private readonly Dictionary<SpectrumBandId, SpectrumBand> _spectrumBandIndex = [];
    private readonly List<FrequencyBlock> _frequencyBlocks = [];
    private readonly Dictionary<FrequencyBlockId, FrequencyBlock> _frequencyBlockIndex = [];
    private readonly List<RadioLinkStateData> _radioLinks = [];
    private readonly Dictionary<RadioLinkId, RadioLinkStateData> _radioLinkIndex = [];
    private readonly List<RadioPeer> _radioPeers = [];
    private readonly Dictionary<RadioPeerId, RadioPeer> _radioPeerIndex = [];
    private readonly IRadioPropagationSolver _radioPropagationSolver;
    private ulong _nextRadioSiteId = 1;
    private ulong _nextSpectrumBandId = 1;
    private ulong _nextFrequencyBlockId = 1;
    private ulong _nextRadioLinkId = 1;
    private ulong _nextRadioPeerId = 1;

    public int RadioSiteCount => _radioSites.Count;
    public int SpectrumBandCount => _spectrumBands.Count;
    public int FrequencyBlockCount => _frequencyBlocks.Count;
    public int RadioLinkCount => _radioLinks.Count;
    public int RadioPeerCount => _radioPeers.Count;

    public RadioSiteId CreateRadioSite(
        WorldPoint position,
        RadioSiteKind kind = RadioSiteKind.Macro,
        double antennaGainDb = 0d,
        double antennaHeightMeters = 10d,
        bool isInService = true)
    {
        ValidatePoint(position);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!double.IsFinite(antennaGainDb)) throw new ArgumentOutOfRangeException(nameof(antennaGainDb));
        if (!double.IsFinite(antennaHeightMeters) || antennaHeightMeters < 0d) throw new ArgumentOutOfRangeException(nameof(antennaHeightMeters));
        EnsureRadioIdCapacity(_nextRadioSiteId, "Radio site");
        var id = new RadioSiteId(_nextRadioSiteId++);
        var state = new RadioSiteState(id, kind, position, antennaGainDb, antennaHeightMeters, isInService);
        _radioSites.Add(state);
        _radioSiteIndex.Add(id, state);
        return id;
    }

    public SpectrumBandId CreateSpectrumBand(string name, double minimumFrequencyMegahertz, double maximumFrequencyMegahertz)
    {
        EnsureRadioIdCapacity(_nextSpectrumBandId, "Spectrum band");
        var band = new SpectrumBand(new SpectrumBandId(_nextSpectrumBandId), name, minimumFrequencyMegahertz, maximumFrequencyMegahertz);
        RadioValidation.ValidateBand(band);
        RadioValidation.ValidateBandNonOverlap(_spectrumBands.Append(band));
        _nextSpectrumBandId++;
        _spectrumBands.Add(band);
        _spectrumBandIndex.Add(band.Id, band);
        return band.Id;
    }

    public FrequencyBlockId CreateFrequencyBlock(SpectrumBandId bandId, double centerFrequencyMegahertz, double bandwidthMegahertz)
    {
        if (!_spectrumBandIndex.TryGetValue(bandId, out var band)) throw new ArgumentException($"Spectrum band {bandId.Value} does not exist.", nameof(bandId));
        EnsureRadioIdCapacity(_nextFrequencyBlockId, "Frequency block");
        var block = new FrequencyBlock(new FrequencyBlockId(_nextFrequencyBlockId), bandId, centerFrequencyMegahertz, bandwidthMegahertz);
        RadioValidation.ValidateFrequencyBlock(block, band);
        _nextFrequencyBlockId++;
        _frequencyBlocks.Add(block);
        _frequencyBlockIndex.Add(block.Id, block);
        return block.Id;
    }

    public RadioLinkId CreateRadioLink(
        RadioSiteId fromSiteId,
        RadioSiteId toSiteId,
        FrequencyBlockId frequencyBlockId,
        RadioLinkBudget linkBudget,
        double utilization = 0d,
        bool isInService = true)
    {
        if (!_radioSiteIndex.ContainsKey(fromSiteId) || !_radioSiteIndex.ContainsKey(toSiteId) || fromSiteId == toSiteId)
            throw new ArgumentException("Radio link references invalid sites.");
        if (!_frequencyBlockIndex.ContainsKey(frequencyBlockId)) throw new ArgumentException($"Frequency block {frequencyBlockId.Value} does not exist.", nameof(frequencyBlockId));
        RadioValidation.ValidateLinkBudget(linkBudget);
        if (!double.IsFinite(utilization) || utilization < 0d || utilization > 1d) throw new ArgumentOutOfRangeException(nameof(utilization));
        EnsureRadioIdCapacity(_nextRadioLinkId, "Radio link");
        var id = new RadioLinkId(_nextRadioLinkId);
        var state = new RadioLinkStateData(id, fromSiteId, toSiteId, frequencyBlockId, linkBudget, utilization, isInService);
        _radioLinks.Add(state);
        _radioLinkIndex.Add(id, state);
        try
        {
            RecalculateRadioPlan();
            _nextRadioLinkId++;
            return id;
        }
        catch
        {
            _radioLinks.Remove(state);
            _radioLinkIndex.Remove(id);
            throw;
        }
    }

    public RadioPeerId CreateRadioPeer(
        IReadOnlyList<RadioSiteId> sourceSiteIds,
        IReadOnlyList<RadioSiteId> destinationSiteIds,
        RadioPeerAmbiguityPolicy ambiguityPolicy = RadioPeerAmbiguityPolicy.Reject)
    {
        ArgumentNullException.ThrowIfNull(sourceSiteIds);
        ArgumentNullException.ThrowIfNull(destinationSiteIds);
        EnsureRadioIdCapacity(_nextRadioPeerId, "Radio peer");
        var peer = new RadioPeer(new RadioPeerId(_nextRadioPeerId), sourceSiteIds.ToArray(), destinationSiteIds.ToArray(), ambiguityPolicy);
        RadioValidation.ValidatePeer(peer, _radioSiteIndex.Keys.ToHashSet());
        _nextRadioPeerId++;
        _radioPeers.Add(peer);
        _radioPeerIndex.Add(peer.Id, peer);
        return peer.Id;
    }

    public (RadioSiteId Source, RadioSiteId Destination) ResolveRadioPeer(RadioPeerId peerId)
    {
        if (!_radioPeerIndex.TryGetValue(peerId, out var peer)) throw new ArgumentException($"Radio peer {peerId.Value} does not exist.", nameof(peerId));
        return RadioValidation.ResolvePeer(peer, _radioSiteIndex.Keys.ToHashSet());
    }

    public void SetRadioSiteInService(RadioSiteId id, bool isInService)
    {
        if (!_radioSiteIndex.TryGetValue(id, out var site)) throw new ArgumentException($"Radio site {id.Value} does not exist.", nameof(id));
        var previous = site.IsInService;
        site.IsInService = isInService;
        try { RecalculateRadioPlan(); } catch { site.IsInService = previous; throw; }
    }

    public void SetRadioLinkInService(RadioLinkId id, bool isInService)
    {
        if (!_radioLinkIndex.TryGetValue(id, out var link)) throw new ArgumentException($"Radio link {id.Value} does not exist.", nameof(id));
        var previous = link.IsInService;
        link.IsInService = isInService;
        try { RecalculateRadioPlan(); } catch { link.IsInService = previous; throw; }
    }

    public void SetRadioLinkUtilization(RadioLinkId id, double utilization)
    {
        if (!_radioLinkIndex.TryGetValue(id, out var link)) throw new ArgumentException($"Radio link {id.Value} does not exist.", nameof(id));
        if (!double.IsFinite(utilization) || utilization < 0d || utilization > 1d) throw new ArgumentOutOfRangeException(nameof(utilization));
        var previous = link.Utilization;
        link.Utilization = utilization;
        try { RecalculateRadioPlan(); } catch { link.Utilization = previous; throw; }
    }

    public bool TryGetRadioLinkSnapshot(RadioLinkId id, out RadioLinkSnapshot snapshot)
    {
        if (_radioLinkIndex.TryGetValue(id, out var link))
        {
            snapshot = CreateRadioLinkSnapshot(link);
            return true;
        }
        snapshot = default;
        return false;
    }

    public RadioSiteSnapshot[] QueryRadioSites(WorldVolume volume) => _radioSites
        .Where(item => volume.Contains(item.Position))
        .OrderBy(static item => item.Id.Value)
        .Select(CreateRadioSiteSnapshot)
        .ToArray();

    public RadioSnapshot CreateRadioSnapshot()
    {
        var conflicts = CreateSpectrumConflicts();
        var serviceAreas = CreateRadioServiceAreas();
        return new RadioSnapshot(
            CreateRadioStatistics(conflicts),
            _radioSites.OrderBy(static item => item.Id.Value).Select(CreateRadioSiteSnapshot).ToArray(),
            _spectrumBands.OrderBy(static item => item.Id.Value).ToArray(),
            _frequencyBlocks.OrderBy(static item => item.Id.Value).ToArray(),
            _radioLinks.OrderBy(static item => item.Id.Value).Select(CreateRadioLinkSnapshot).ToArray(),
            serviceAreas,
            conflicts,
            CreateRadioAntennaSnapshots(),
            CreateRadioTransmitterSnapshots(),
            CreateRadioReceiverSnapshots(),
            CreateRadioEmissionSnapshots());
    }

    public IReadOnlyList<SpectrumConflict> CreateSpectrumConflicts()
    {
        var conflicts = new List<SpectrumConflict>();
        var legacyLinks = _radioLinks
            .Where(item => !_radioLinkEntityBindings.ContainsKey(item.Id) && IsRadioLinkOperational(item))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        for (var leftIndex = 0; leftIndex < legacyLinks.Length; leftIndex++)
        {
            var left = legacyLinks[leftIndex];
            var leftBlock = _frequencyBlockIndex[left.FrequencyBlockId];
            for (var rightIndex = leftIndex + 1; rightIndex < legacyLinks.Length; rightIndex++)
            {
                var right = legacyLinks[rightIndex];
                var rightBlock = _frequencyBlockIndex[right.FrequencyBlockId];
                if (!RadioValidation.FrequencyBlocksOverlap(leftBlock, rightBlock)) continue;
                var distance = DistanceMeters(_radioSiteIndex[left.FromSiteId].Position, _radioSiteIndex[right.FromSiteId].Position);
                if (distance > RadioDefaults.SpectrumConflictDistanceMeters) continue;
                conflicts.Add(new SpectrumConflict(left.FrequencyBlockId, right.FrequencyBlockId, left.FromSiteId, right.FromSiteId, "frequencyReuseWithinConflictDistance"));
            }
        }

        foreach (var emission in _radioEmissions.Where(IsRadioEmissionOperational).OrderBy(static item => item.Id.Value))
        {
            var transmitter = _radioTransmitterIndex[emission.TransmitterId];
            var position = GetRadioTransmitterPosition(transmitter);
            var radius = RadioDefaults.SpectrumConflictDistanceMeters;
            var volume = new WorldVolume(position.X - radius, position.Y - radius, position.Z - radius, position.X + radius, position.Y + radius, position.Z + radius);
            var channel = GetRequiredRadioChannel(emission.ChannelId);
            foreach (var otherId in _radioEmissionSpatialIndex.Query(volume))
            {
                if (otherId.Value <= emission.Id.Value || !_radioEmissionIndex.TryGetValue(otherId, out var other) || !IsRadioEmissionOperational(other)) continue;
                var otherChannel = GetRequiredRadioChannel(other.ChannelId);
                if (!RadioChannelsOverlap(channel, otherChannel)) continue;
                var otherTransmitter = _radioTransmitterIndex[other.TransmitterId];
                var otherPosition = GetRadioTransmitterPosition(otherTransmitter);
                if (DistanceMeters(position, otherPosition) > radius) continue;
                conflicts.Add(new SpectrumConflict(
                    new FrequencyBlockId(channel.Id.Value),
                    new FrequencyBlockId(otherChannel.Id.Value),
                    transmitter.SiteId,
                    otherTransmitter.SiteId,
                    "overlappingEmissionWithinConflictDistance"));
            }
        }

        return conflicts
            .OrderBy(static item => item.FirstSiteId.Value)
            .ThenBy(static item => item.SecondSiteId.Value)
            .ThenBy(static item => item.FirstBlockId.Value)
            .ThenBy(static item => item.SecondBlockId.Value)
            .ToArray();
    }

    public void RecalculateRadioPlan()
    {
        var updates = new List<(RadioLinkStateData Link, RadioPropagationResult Propagation, RadioLinkState State)>();
        foreach (var link in _radioLinks.OrderBy(static item => item.Id.Value))
        {
            if (!IsRadioLinkOperational(link))
            {
                updates.Add((link, default, RadioLinkState.OutOfService));
                continue;
            }
            RadioPropagationRequest request;
            if (_radioLinkEntityBindings.TryGetValue(link.Id, out var binding))
            {
                var interferenceDbm = CalculateExplicitInterferenceDbm(link, binding);
                if (!TryCreateExplicitRadioPropagationRequest(link, interferenceDbm, out request))
                    throw new InvalidOperationException($"Radio link {link.Id.Value} has an invalid explicit entity binding.");
            }
            else
            {
                var transmitter = CreateRadioSiteSnapshot(_radioSiteIndex[link.FromSiteId]);
                var receiver = CreateRadioSiteSnapshot(_radioSiteIndex[link.ToSiteId]);
                var block = _frequencyBlockIndex[link.FrequencyBlockId];
                var interferenceDbm = CalculateLegacyInterferenceDbm(link, receiver);
                var obstruction = CalculateRadioBuildingObstruction(transmitter.Position, receiver.Position);
                request = new RadioPropagationRequest(transmitter, receiver, block, link.LinkBudget, interferenceDbm, RadioDefaults.ThermalNoiseFloorDbm, obstruction.LossDb, obstruction.IsLineOfSight);
            }
            var result = _radioPropagationSolver.Solve(request);
            if (!double.IsFinite(result.DistanceMeters) || !double.IsFinite(result.PathLossDb) || !double.IsFinite(result.ReceivedPowerDbm) || !double.IsFinite(result.InterferenceDbm) || !double.IsFinite(result.SinrDb))
                throw new InvalidOperationException("Radio propagation solver returned a non-finite result.");
            var state = !result.IsReachable ? RadioLinkState.Unreachable
                : result.SinrDb < RadioDefaults.MinimumSinrDb ? RadioLinkState.Interfered
                : result.SinrDb < RadioDefaults.MarginalSinrDb ? RadioLinkState.Marginal : RadioLinkState.Healthy;
            updates.Add((link, result, state));
        }
        foreach (var update in updates)
        {
            update.Link.Propagation = update.Propagation;
            update.Link.State = update.State;
        }
    }

    private void StepRadio(SimulationTime nextTime)
    {
        _ = nextTime;
        RecalculateRadioPlan();
    }

    private double CalculateLegacyInterferenceDbm(RadioLinkStateData target, RadioSiteSnapshot targetReceiver)
    {
        var combinedInterferenceDbm = -300d;
        var hasInterference = false;
        var targetBlock = _frequencyBlockIndex[target.FrequencyBlockId];
        foreach (var other in _radioLinks.OrderBy(static item => item.Id.Value))
        {
            if (other.Id == target.Id || _radioLinkEntityBindings.ContainsKey(other.Id) || !IsRadioLinkOperational(other)) continue;
            var otherBlock = _frequencyBlockIndex[other.FrequencyBlockId];
            if (!RadioValidation.FrequencyBlocksOverlap(targetBlock, otherBlock)) continue;
            var otherTransmitter = CreateRadioSiteSnapshot(_radioSiteIndex[other.FromSiteId]);
            var obstruction = CalculateRadioBuildingObstruction(otherTransmitter.Position, targetReceiver.Position);
            var interferenceRequest = new RadioPropagationRequest(
                otherTransmitter,
                targetReceiver,
                otherBlock,
                other.LinkBudget,
                -300d,
                RadioDefaults.ThermalNoiseFloorDbm,
                obstruction.LossDb,
                obstruction.IsLineOfSight);
            var result = _radioPropagationSolver.Solve(interferenceRequest);
            combinedInterferenceDbm = hasInterference
                ? DeterministicRadioPropagationSolver.CombinePowersDbm(combinedInterferenceDbm, result.ReceivedPowerDbm)
                : result.ReceivedPowerDbm;
            hasInterference = true;
        }
        return hasInterference ? combinedInterferenceDbm : -300d;
    }

    private RadioServiceArea[] CreateRadioServiceAreas() => _radioLinks
        .Where(IsRadioLinkOperational)
        .GroupBy(static link => (link.FromSiteId, link.FrequencyBlockId))
        .OrderBy(static group => group.Key.FromSiteId.Value)
        .ThenBy(static group => group.Key.FrequencyBlockId.Value)
        .Select(group => new RadioServiceArea(
            group.Key.FromSiteId,
            group.Key.FrequencyBlockId,
            group.Where(static item => item.Propagation.IsReachable).Select(static item => item.Propagation.DistanceMeters).DefaultIfEmpty(0d).Max(),
            RadioDefaults.MinimumSinrDb))
        .ToArray();

    private RadioStatistics CreateRadioStatistics(IReadOnlyList<SpectrumConflict> conflicts)
    {
        var peakLinkUtilization = _radioLinks.Select(static item => item.Utilization).DefaultIfEmpty(0d).Max();
        var peakEmissionUtilization = _radioEmissions.Select(static item => item.Utilization).DefaultIfEmpty(0d).Max();
        return new RadioStatistics(
            _radioSites.Count,
            _spectrumBands.Count,
            _frequencyBlocks.Count,
            _radioLinks.Count,
            CreateRadioServiceAreas().Length,
            conflicts.Count,
            _radioLinks.Count(static item => item.State == RadioLinkState.Healthy),
            _radioLinks.Count(static item => item.State is RadioLinkState.Interfered or RadioLinkState.Marginal),
            _radioLinks.Count(static item => item.State == RadioLinkState.Unreachable),
            Math.Max(peakLinkUtilization, peakEmissionUtilization),
            Time.TickCount);
    }

    private static RadioSiteSnapshot CreateRadioSiteSnapshot(RadioSiteState site) => new(site.Id, site.Kind, site.Position, site.AntennaGainDb, site.AntennaHeightMeters, site.IsInService);

    private RadioLinkSnapshot CreateRadioLinkSnapshot(RadioLinkStateData link) => new(
        link.Id,
        link.FromSiteId,
        link.ToSiteId,
        link.FrequencyBlockId,
        link.Propagation.DistanceMeters,
        link.Propagation.PathLossDb,
        link.Propagation.ReceivedPowerDbm,
        link.Propagation.InterferenceDbm,
        link.Propagation.SinrDb,
        link.Utilization,
        link.State,
        link.IsInService);

    private bool IsRadioLinkOperational(RadioLinkStateData link)
    {
        if (!link.IsInService
            || !_radioSiteIndex.TryGetValue(link.FromSiteId, out var from) || !from.IsInService
            || !_radioSiteIndex.TryGetValue(link.ToSiteId, out var to) || !to.IsInService
            || !IsRadioSiteInfrastructureAvailable(link.FromSiteId)
            || !IsRadioSiteInfrastructureAvailable(link.ToSiteId)) return false;

        if (!_radioLinkEntityBindings.TryGetValue(link.Id, out var binding)) return true;
        return _radioEmissionIndex.TryGetValue(binding.EmissionId, out var emission)
            && IsRadioEmissionOperational(emission)
            && _radioReceiverIndex.TryGetValue(binding.ReceiverId, out var receiver)
            && IsRadioReceiverOperational(receiver);
    }

    private static double DistanceMeters(WorldPoint left, WorldPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        var dz = left.Z - right.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static void EnsureRadioIdCapacity(ulong nextId, string entityName)
    {
        if (nextId == 0 || nextId == ulong.MaxValue) throw new InvalidOperationException($"{entityName} ID capacity has been exhausted.");
    }
}
