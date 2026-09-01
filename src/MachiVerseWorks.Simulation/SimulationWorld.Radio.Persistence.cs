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
        _radioPeers.OrderBy(static item => item.Id.Value).ToArray());

    private void RestoreRadio(RadioCheckpoint? checkpoint)
    {
        if (checkpoint is null) return;
        _nextRadioSiteId = checkpoint.NextSiteId;
        _nextSpectrumBandId = checkpoint.NextBandId;
        _nextFrequencyBlockId = checkpoint.NextFrequencyBlockId;
        _nextRadioLinkId = checkpoint.NextLinkId;
        _nextRadioPeerId = checkpoint.NextPeerId;

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

        var siteIds = ValidateRadioCheckpointIds(radio.Sites.Select(static item => item.Id.Value), radio.NextSiteId, "Radio site");
        var bandIds = ValidateRadioCheckpointIds(radio.Bands.Select(static item => item.Id.Value), radio.NextBandId, "Spectrum band");
        var blockIds = ValidateRadioCheckpointIds(radio.FrequencyBlocks.Select(static item => item.Id.Value), radio.NextFrequencyBlockId, "Frequency block");
        _ = ValidateRadioCheckpointIds(radio.Links.Select(static item => item.Id.Value), radio.NextLinkId, "Radio link");
        _ = ValidateRadioCheckpointIds(radio.Peers.Select(static item => item.Id.Value), radio.NextPeerId, "Radio peer");

        RadioValidation.ValidateBandNonOverlap(radio.Bands);
        var bands = radio.Bands.ToDictionary(static item => item.Id);
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
