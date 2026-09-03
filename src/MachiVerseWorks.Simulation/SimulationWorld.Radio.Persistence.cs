namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private EconomyCheckpoint CreateEconomyCheckpointWithRadio() =>
        CreateEconomyCheckpointWithOptical() with { Radio = CreateRadioCheckpoint() };

    private RadioCheckpoint CreateRadioCheckpoint() => new(
        _nextRadioSiteId,
        _nextSpectrumBandId,
        _nextFrequencyBlockId,
        _nextRadioLinkId,
        _nextRadioPeerId,
        _radioSites.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationRadioSiteCheckpoint(item.Id, item.Kind, item.Position, item.AntennaGainDb, item.AntennaHeightMeters, item.IsInService)).ToArray(),
        _spectrumBands.OrderBy(static item => item.Id.Value).ToArray(),
        _frequencyBlocks.OrderBy(static item => item.Id.Value).ToArray(),
        _radioLinks.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationRadioLinkCheckpoint(item.Id, item.FromSiteId, item.ToSiteId, item.FrequencyBlockId, item.LinkBudget, item.Utilization, item.IsInService)).ToArray(),
        _radioPeers.OrderBy(static item => item.Id.Value).ToArray(),
        _nextRadioAntennaId,
        _nextRadioTransmitterId,
        _nextRadioReceiverId,
        _nextRadioEmissionId,
        _radioAntennas.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationRadioAntennaCheckpoint(item.Id, item.SiteId, item.PositionOffset, item.Orientation, item.GainDb, item.PatternKind, item.BeamwidthDegrees, item.FrontToBackRatioDb, item.IsInService)).ToArray(),
        _radioTransmitters.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationRadioTransmitterCheckpoint(item.Id, item.SiteId, item.AntennaId, item.MaximumTransmitPowerDbm, item.IsInService)).ToArray(),
        _radioReceivers.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationRadioReceiverCheckpoint(item.Id, item.SiteId, item.AntennaId, item.MinimumFrequencyMegahertz, item.MaximumFrequencyMegahertz, item.SensitivityDbm, item.IsInService)).ToArray(),
        _radioEmissions.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationRadioEmissionCheckpoint(item.Id, item.TransmitterId, item.ChannelId, item.TransmitPowerDbm, item.Utilization, item.IsInService)).ToArray(),
        _radioSiteInfrastructure.Values.OrderBy(static item => item.SiteId.Value).ToArray(),
        _radioLinkEntityBindings.Values.OrderBy(static item => item.LinkId.Value).ToArray());

    private void RestoreRadio(RadioCheckpoint? checkpoint)
    {
        if (checkpoint is null) return;
        _nextRadioSiteId = checkpoint.NextSiteId;
        _nextSpectrumBandId = checkpoint.NextBandId;
        _nextFrequencyBlockId = checkpoint.NextFrequencyBlockId;
        _nextRadioLinkId = checkpoint.NextLinkId;
        _nextRadioPeerId = checkpoint.NextPeerId;
        _nextRadioAntennaId = checkpoint.NextAntennaId;
        _nextRadioTransmitterId = checkpoint.NextTransmitterId;
        _nextRadioReceiverId = checkpoint.NextReceiverId;
        _nextRadioEmissionId = checkpoint.NextEmissionId;

        foreach (var item in checkpoint.Sites.OrderBy(static item => item.Id.Value))
        {
            var state = new RadioSiteState(item.Id, item.Kind, item.Position, item.AntennaGainDb, item.AntennaHeightMeters, item.IsInService);
            _radioSites.Add(state);
            _radioSiteIndex.Add(item.Id, state);
        }
        foreach (var item in checkpoint.Bands.OrderBy(static item => item.Id.Value))
        {
            _spectrumBands.Add(item);
            _spectrumBandIndex.Add(item.Id, item);
        }
        foreach (var item in checkpoint.FrequencyBlocks.OrderBy(static item => item.Id.Value))
        {
            _frequencyBlocks.Add(item);
            _frequencyBlockIndex.Add(item.Id, item);
        }
        foreach (var item in checkpoint.Links.OrderBy(static item => item.Id.Value))
        {
            var state = new RadioLinkStateData(item.Id, item.FromSiteId, item.ToSiteId, item.FrequencyBlockId, item.LinkBudget, item.Utilization, item.IsInService);
            _radioLinks.Add(state);
            _radioLinkIndex.Add(item.Id, state);
        }
        foreach (var item in checkpoint.Peers.OrderBy(static item => item.Id.Value))
        {
            _radioPeers.Add(item);
            _radioPeerIndex.Add(item.Id, item);
        }
        foreach (var item in (checkpoint.Antennas ?? []).OrderBy(static item => item.Id.Value))
        {
            var state = new RadioAntennaState(item.Id, item.SiteId, item.PositionOffset, item.Orientation, item.GainDb, item.PatternKind, item.BeamwidthDegrees, item.FrontToBackRatioDb, item.IsInService);
            _radioAntennas.Add(state);
            _radioAntennaIndex.Add(item.Id, state);
        }
        foreach (var item in (checkpoint.Transmitters ?? []).OrderBy(static item => item.Id.Value))
        {
            var state = new RadioTransmitterState(item.Id, item.SiteId, item.AntennaId, item.MaximumTransmitPowerDbm, item.IsInService);
            _radioTransmitters.Add(state);
            _radioTransmitterIndex.Add(item.Id, state);
        }
        foreach (var item in (checkpoint.Receivers ?? []).OrderBy(static item => item.Id.Value))
        {
            var state = new RadioReceiverState(item.Id, item.SiteId, item.AntennaId, item.MinimumFrequencyMegahertz, item.MaximumFrequencyMegahertz, item.SensitivityDbm, item.IsInService);
            _radioReceivers.Add(state);
            _radioReceiverIndex.Add(item.Id, state);
        }
        _radioEmissionSpatialIndex.Clear();
        foreach (var item in (checkpoint.Emissions ?? []).OrderBy(static item => item.Id.Value))
        {
            var state = new RadioEmissionState(item.Id, item.TransmitterId, item.ChannelId, item.TransmitPowerDbm, item.Utilization, item.IsInService);
            _radioEmissions.Add(state);
            _radioEmissionIndex.Add(item.Id, state);
            _radioEmissionSpatialIndex.Add(item.Id, GetRadioTransmitterPosition(_radioTransmitterIndex[item.TransmitterId]));
        }
        foreach (var item in checkpoint.SiteInfrastructure ?? []) _radioSiteInfrastructure.Add(item.SiteId, item);
        foreach (var item in checkpoint.LinkEntityBindings ?? []) _radioLinkEntityBindings.Add(item.LinkId, item);
        RecalculateRadioPlan();
    }

    private static void ValidateRadioCheckpoint(SimulationCheckpoint checkpoint)
    {
        var radio = checkpoint.Economy?.Radio;
        if (radio is null) return;
        ArgumentNullException.ThrowIfNull(radio.Sites);
        ArgumentNullException.ThrowIfNull(radio.Bands);
        ArgumentNullException.ThrowIfNull(radio.FrequencyBlocks);
        ArgumentNullException.ThrowIfNull(radio.Links);
        ArgumentNullException.ThrowIfNull(radio.Peers);

        var antennas = radio.Antennas ?? [];
        var transmitters = radio.Transmitters ?? [];
        var receivers = radio.Receivers ?? [];
        var emissions = radio.Emissions ?? [];
        var infrastructure = radio.SiteInfrastructure ?? [];
        var linkBindings = radio.LinkEntityBindings ?? [];

        var siteIds = ValidateRadioCheckpointIds(radio.Sites.Select(static item => item.Id.Value), radio.NextSiteId, "Radio site");
        var bandIds = ValidateRadioCheckpointIds(radio.Bands.Select(static item => item.Id.Value), radio.NextBandId, "Spectrum band");
        var blockIds = ValidateRadioCheckpointIds(radio.FrequencyBlocks.Select(static item => item.Id.Value), radio.NextFrequencyBlockId, "Frequency block");
        var linkIds = ValidateRadioCheckpointIds(radio.Links.Select(static item => item.Id.Value), radio.NextLinkId, "Radio link");
        _ = ValidateRadioCheckpointIds(radio.Peers.Select(static item => item.Id.Value), radio.NextPeerId, "Radio peer");
        var antennaIds = ValidateRadioCheckpointIds(antennas.Select(static item => item.Id.Value), radio.NextAntennaId, "Radio antenna");
        var transmitterIds = ValidateRadioCheckpointIds(transmitters.Select(static item => item.Id.Value), radio.NextTransmitterId, "Radio transmitter");
        var receiverIds = ValidateRadioCheckpointIds(receivers.Select(static item => item.Id.Value), radio.NextReceiverId, "Radio receiver");
        var emissionIds = ValidateRadioCheckpointIds(emissions.Select(static item => item.Id.Value), radio.NextEmissionId, "Radio emission");

        RadioValidation.ValidateBandNonOverlap(radio.Bands);
        var bands = radio.Bands.ToDictionary(static item => item.Id);
        var blocks = radio.FrequencyBlocks.ToDictionary(static item => item.Id);
        foreach (var site in radio.Sites)
        {
            if (!Enum.IsDefined(site.Kind)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidatePoint(site.Position);
            if (!double.IsFinite(site.AntennaGainDb) || !double.IsFinite(site.AntennaHeightMeters) || site.AntennaHeightMeters < 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }
        foreach (var block in radio.FrequencyBlocks)
        {
            if (!bandIds.Contains(block.BandId.Value) || !bands.TryGetValue(block.BandId, out var band))
                throw new ArgumentException($"Frequency block {block.Id.Value} references a missing SpectrumBand.", nameof(checkpoint));
            RadioValidation.ValidateFrequencyBlock(block, band);
        }
        foreach (var link in radio.Links)
        {
            if (!siteIds.Contains(link.FromSiteId.Value) || !siteIds.Contains(link.ToSiteId.Value) || link.FromSiteId == link.ToSiteId)
                throw new ArgumentException($"Radio link {link.Id.Value} references invalid sites.", nameof(checkpoint));
            if (!blockIds.Contains(link.FrequencyBlockId.Value))
                throw new ArgumentException($"Radio link {link.Id.Value} references a missing FrequencyBlock.", nameof(checkpoint));
            RadioValidation.ValidateLinkBudget(link.LinkBudget);
            if (!double.IsFinite(link.Utilization) || link.Utilization < 0d || link.Utilization > 1d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }
        var typedSiteIds = radio.Sites.Select(static item => item.Id).ToHashSet();
        foreach (var peer in radio.Peers) RadioValidation.ValidatePeer(peer, typedSiteIds);

        var antennaById = antennas.ToDictionary(static item => item.Id);
        foreach (var antenna in antennas)
        {
            if (!siteIds.Contains(antenna.SiteId.Value) || !Enum.IsDefined(antenna.PatternKind)) throw new ArgumentException("Radio antenna references invalid state.", nameof(checkpoint));
            ValidateVector(antenna.PositionOffset);
            ValidateVector(antenna.Orientation);
            var orientationLength = Math.Sqrt((antenna.Orientation.X * antenna.Orientation.X) + (antenna.Orientation.Y * antenna.Orientation.Y) + (antenna.Orientation.Z * antenna.Orientation.Z));
            if (orientationLength <= 1e-12 || Math.Abs(orientationLength - 1d) > 1e-9
                || !double.IsFinite(antenna.GainDb) || !double.IsFinite(antenna.BeamwidthDegrees) || antenna.BeamwidthDegrees <= 0d || antenna.BeamwidthDegrees > 360d
                || !double.IsFinite(antenna.FrontToBackRatioDb) || antenna.FrontToBackRatioDb < 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            if (antenna.PatternKind == RadioAntennaPatternKind.Omnidirectional && antenna.BeamwidthDegrees != 360d)
                throw new ArgumentException("Omnidirectional Radio antennas must use a 360 degree beamwidth.", nameof(checkpoint));
        }

        var transmitterById = transmitters.ToDictionary(static item => item.Id);
        foreach (var transmitter in transmitters)
        {
            if (!siteIds.Contains(transmitter.SiteId.Value) || !antennaIds.Contains(transmitter.AntennaId.Value) || antennaById[transmitter.AntennaId].SiteId != transmitter.SiteId)
                throw new ArgumentException("Radio transmitter references invalid site or antenna.", nameof(checkpoint));
            if (!double.IsFinite(transmitter.MaximumTransmitPowerDbm) || transmitter.MaximumTransmitPowerDbm is < -100d or > 100d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        var receiverById = receivers.ToDictionary(static item => item.Id);
        foreach (var receiver in receivers)
        {
            if (!siteIds.Contains(receiver.SiteId.Value) || !antennaIds.Contains(receiver.AntennaId.Value) || antennaById[receiver.AntennaId].SiteId != receiver.SiteId)
                throw new ArgumentException("Radio receiver references invalid site or antenna.", nameof(checkpoint));
            if (!double.IsFinite(receiver.MinimumFrequencyMegahertz) || receiver.MinimumFrequencyMegahertz <= 0d
                || !double.IsFinite(receiver.MaximumFrequencyMegahertz) || receiver.MaximumFrequencyMegahertz <= receiver.MinimumFrequencyMegahertz
                || !double.IsFinite(receiver.SensitivityDbm) || receiver.SensitivityDbm >= 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        foreach (var emission in emissions)
        {
            if (!transmitterIds.Contains(emission.TransmitterId.Value) || !blockIds.Contains(emission.ChannelId.Value))
                throw new ArgumentException("Radio emission references invalid transmitter or channel.", nameof(checkpoint));
            var transmitter = transmitterById[emission.TransmitterId];
            if (!double.IsFinite(emission.TransmitPowerDbm) || emission.TransmitPowerDbm is < -100d or > 100d || emission.TransmitPowerDbm > transmitter.MaximumTransmitPowerDbm
                || !double.IsFinite(emission.Utilization) || emission.Utilization < 0d || emission.Utilization > 1d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        var buildingIds = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var backhaulIds = checkpoint.Economy?.Optical?.Backhauls.Select(static item => item.Id).ToHashSet() ?? [];
        var seenInfrastructureSites = new HashSet<RadioSiteId>();
        foreach (var binding in infrastructure)
        {
            if (!seenInfrastructureSites.Add(binding.SiteId) || !typedSiteIds.Contains(binding.SiteId)) throw new ArgumentException("Radio site infrastructure binding is invalid or duplicated.", nameof(checkpoint));
            if (binding.BuildingId is { } buildingId && !buildingIds.Contains(buildingId)) throw new ArgumentException("Radio site infrastructure references a missing Building.", nameof(checkpoint));
            if (binding.OpticalBackhaulId is { } backhaulId && !backhaulIds.Contains(backhaulId)) throw new ArgumentException("Radio site infrastructure references a missing Optical backhaul.", nameof(checkpoint));
            if (binding.RequiresPower && binding.BuildingId is null) throw new ArgumentException("Power-dependent Radio site infrastructure must reference a Building.", nameof(checkpoint));
        }

        var linksById = radio.Links.ToDictionary(static item => item.Id);
        var emissionById = emissions.ToDictionary(static item => item.Id);
        var seenBoundLinks = new HashSet<RadioLinkId>();
        foreach (var binding in linkBindings)
        {
            if (!seenBoundLinks.Add(binding.LinkId) || !linkIds.Contains(binding.LinkId.Value) || !emissionIds.Contains(binding.EmissionId.Value) || !receiverIds.Contains(binding.ReceiverId.Value))
                throw new ArgumentException("Radio link entity binding is invalid or duplicated.", nameof(checkpoint));
            var link = linksById[binding.LinkId];
            var emission = emissionById[binding.EmissionId];
            var transmitter = transmitterById[emission.TransmitterId];
            var receiver = receiverById[binding.ReceiverId];
            if (link.FromSiteId != transmitter.SiteId || link.ToSiteId != receiver.SiteId || link.FrequencyBlockId.Value != emission.ChannelId.Value)
                throw new ArgumentException("Radio link entity binding does not match link endpoints or channel.", nameof(checkpoint));
            var block = blocks[link.FrequencyBlockId];
            var half = block.BandwidthMegahertz / 2d;
            if (block.CenterFrequencyMegahertz - half < receiver.MinimumFrequencyMegahertz || block.CenterFrequencyMegahertz + half > receiver.MaximumFrequencyMegahertz)
                throw new ArgumentException("Bound Radio receiver does not support the link channel.", nameof(checkpoint));
        }
    }

    private static HashSet<ulong> ValidateRadioCheckpointIds(IEnumerable<ulong> ids, ulong nextId, string name)
    {
        if (nextId == 0) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {name} ID must be greater than zero.");
        var seen = new HashSet<ulong>();
        var maximum = 0UL;
        foreach (var id in ids)
        {
            if (id == 0 || !seen.Add(id)) throw new ArgumentException($"{name} IDs must be unique and greater than zero.", nameof(ids));
            maximum = Math.Max(maximum, id);
        }
        if (nextId <= maximum) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {name} ID must exceed stored IDs.");
        return seen;
    }
}
