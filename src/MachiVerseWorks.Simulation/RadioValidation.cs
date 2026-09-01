namespace MachiVerseWorks.Simulation;

public static class RadioValidation
{
    public static void ValidateBand(SpectrumBand band)
    {
        if (band.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(band), "Spectrum band ID must be greater than zero.");
        if (string.IsNullOrWhiteSpace(band.Name)) throw new ArgumentException("Spectrum band name is required.", nameof(band));
        ValidatePositiveFinite(band.MinimumFrequencyMegahertz, nameof(band));
        ValidatePositiveFinite(band.MaximumFrequencyMegahertz, nameof(band));
        if (band.MaximumFrequencyMegahertz <= band.MinimumFrequencyMegahertz)
            throw new ArgumentException("Spectrum band maximum frequency must exceed the minimum frequency.", nameof(band));
    }

    public static void ValidateFrequencyBlock(FrequencyBlock block, SpectrumBand band)
    {
        ValidateBand(band);
        if (block.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(block), "Frequency block ID must be greater than zero.");
        if (block.BandId != band.Id) throw new ArgumentException("Frequency block references a different spectrum band.", nameof(block));
        ValidatePositiveFinite(block.CenterFrequencyMegahertz, nameof(block));
        ValidatePositiveFinite(block.BandwidthMegahertz, nameof(block));
        var half = block.BandwidthMegahertz / 2d;
        if (block.CenterFrequencyMegahertz - half < band.MinimumFrequencyMegahertz
            || block.CenterFrequencyMegahertz + half > band.MaximumFrequencyMegahertz)
            throw new ArgumentOutOfRangeException(nameof(block), "Frequency block must fit inside its spectrum band.");
    }

    public static void ValidateBandNonOverlap(IEnumerable<SpectrumBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        var ordered = bands.OrderBy(static item => item.MinimumFrequencyMegahertz).ThenBy(static item => item.Id.Value).ToArray();
        for (var index = 0; index < ordered.Length; index++) ValidateBand(ordered[index]);
        for (var index = 1; index < ordered.Length; index++)
            if (ordered[index].MinimumFrequencyMegahertz < ordered[index - 1].MaximumFrequencyMegahertz)
                throw new ArgumentException($"Spectrum bands {ordered[index - 1].Id.Value} and {ordered[index].Id.Value} overlap.", nameof(bands));
    }

    public static void ValidatePeer(RadioPeer peer, IReadOnlySet<RadioSiteId> siteIds)
    {
        ArgumentNullException.ThrowIfNull(peer.SourceSiteIds);
        ArgumentNullException.ThrowIfNull(peer.DestinationSiteIds);
        ArgumentNullException.ThrowIfNull(siteIds);
        if (peer.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(peer));
        if (!Enum.IsDefined(peer.AmbiguityPolicy)) throw new ArgumentOutOfRangeException(nameof(peer));
        if (peer.SourceSiteIds.Count == 0 || peer.DestinationSiteIds.Count == 0)
            throw new ArgumentException("Radio peer requires at least one source and one destination site.", nameof(peer));
        if (peer.SourceSiteIds.Distinct().Count() != peer.SourceSiteIds.Count || peer.DestinationSiteIds.Distinct().Count() != peer.DestinationSiteIds.Count)
            throw new ArgumentException("Radio peer site IDs must be unique within each endpoint set.", nameof(peer));
        if (peer.SourceSiteIds.Any(id => !siteIds.Contains(id)) || peer.DestinationSiteIds.Any(id => !siteIds.Contains(id)))
            throw new ArgumentException("Radio peer references a missing site.", nameof(peer));
        if (peer.SourceSiteIds.Intersect(peer.DestinationSiteIds).Any())
            throw new ArgumentException("Radio peer source and destination sets must not overlap.", nameof(peer));
        if (peer.AmbiguityPolicy == RadioPeerAmbiguityPolicy.Reject
            && (peer.SourceSiteIds.Count != 1 || peer.DestinationSiteIds.Count != 1))
            throw new ArgumentException("Ambiguous multi-source or multi-destination RadioPeer requires an explicit deterministic ambiguity policy.", nameof(peer));
    }

    public static (RadioSiteId Source, RadioSiteId Destination) ResolvePeer(RadioPeer peer, IReadOnlySet<RadioSiteId> siteIds)
    {
        ValidatePeer(peer, siteIds);
        return peer.AmbiguityPolicy switch
        {
            RadioPeerAmbiguityPolicy.Reject => (peer.SourceSiteIds[0], peer.DestinationSiteIds[0]),
            RadioPeerAmbiguityPolicy.LowestSiteId => (
                peer.SourceSiteIds.OrderBy(static id => id.Value).First(),
                peer.DestinationSiteIds.OrderBy(static id => id.Value).First()),
            _ => throw new ArgumentOutOfRangeException(nameof(peer)),
        };
    }

    public static void ValidateLinkBudget(RadioLinkBudget budget)
    {
        ValidateFinite(budget.Transmitter.EffectiveRadiatedPower.Dbm, nameof(budget));
        ValidateNonNegativeFinite(budget.Transmitter.FeederLossDb, nameof(budget));
        ValidateNonNegativeFinite(budget.Transmitter.MiscellaneousLossDb, nameof(budget));
        ValidateFinite(budget.ReceiveAntennaGainDb, nameof(budget));
        ValidateFinite(budget.ReceiverSensitivityDbm, nameof(budget));
        ValidateNonNegativeFinite(budget.FadeMarginDb, nameof(budget));
        if (budget.Transmitter.EffectiveRadiatedPower.Dbm is < -100d or > 100d)
            throw new ArgumentOutOfRangeException(nameof(budget), "Effective radiated power is outside the supported engineering range.");
        if (budget.ReceiverSensitivityDbm >= 0d)
            throw new ArgumentOutOfRangeException(nameof(budget), "Receiver sensitivity must be below 0 dBm.");
    }

    public static void ValidatePropagationRequest(RadioPropagationRequest request)
    {
        ValidateSiteSnapshot(request.Transmitter, nameof(request));
        ValidateSiteSnapshot(request.Receiver, nameof(request));
        if (request.Transmitter.Id == request.Receiver.Id) throw new ArgumentException("Radio propagation requires two different sites.", nameof(request));
        if (request.FrequencyBlock.Id.Value == 0 || request.FrequencyBlock.BandId.Value == 0) throw new ArgumentOutOfRangeException(nameof(request));
        ValidatePositiveFinite(request.FrequencyBlock.CenterFrequencyMegahertz, nameof(request));
        ValidatePositiveFinite(request.FrequencyBlock.BandwidthMegahertz, nameof(request));
        ValidateLinkBudget(request.LinkBudget);
        ValidateFinite(request.InterferenceDbm, nameof(request));
        ValidateFinite(request.NoiseFloorDbm, nameof(request));
        ValidateNonNegativeFinite(request.ObstructionLossDb, nameof(request));
    }

    public static void ValidateSiteSnapshot(RadioSiteSnapshot site, string paramName)
    {
        if (site.Id.Value == 0) throw new ArgumentOutOfRangeException(paramName);
        if (!Enum.IsDefined(site.Kind)) throw new ArgumentOutOfRangeException(paramName);
        if (!double.IsFinite(site.Position.X) || !double.IsFinite(site.Position.Y) || !double.IsFinite(site.Position.Z)) throw new ArgumentOutOfRangeException(paramName);
        ValidateFinite(site.AntennaGainDb, paramName);
        ValidateNonNegativeFinite(site.AntennaHeightMeters, paramName);
    }

    public static bool FrequencyBlocksOverlap(FrequencyBlock first, FrequencyBlock second)
    {
        var firstHalf = first.BandwidthMegahertz / 2d;
        var secondHalf = second.BandwidthMegahertz / 2d;
        return first.CenterFrequencyMegahertz - firstHalf < second.CenterFrequencyMegahertz + secondHalf
            && second.CenterFrequencyMegahertz - secondHalf < first.CenterFrequencyMegahertz + firstHalf;
    }

    private static void ValidatePositiveFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0d) throw new ArgumentOutOfRangeException(paramName);
    }

    private static void ValidateNonNegativeFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0d) throw new ArgumentOutOfRangeException(paramName);
    }

    private static void ValidateFinite(double value, string paramName)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(paramName);
    }
}
