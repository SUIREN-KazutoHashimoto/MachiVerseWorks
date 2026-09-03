using System.Globalization;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class MultimodalTransitMessageMapper
{
    public const string TooLargeDetailCode = "multimodalTransitSnapshotTooLarge";

    public static IProtocolMessage Create(MultimodalTransitSnapshot transit, ulong tickCount) =>
        CreateResult(CreateSnapshot(transit, tickCount), "snapshot");

    public static IProtocolMessage Create(MultimodalTransitSnapshot transit, ulong tickCount, WorldVolume volume) =>
        CreateResult(CreateSnapshot(transit, tickCount, volume), "volume");

    private static IProtocolMessage CreateResult(MultimodalTransitSnapshotMessage message, string field)
    {
        var payloadLength = MultimodalTransitProtocolCodec.GetPayloadLength(message);
        if ((ulong)payloadLength <= ProtocolFrameHeader.MaxPayloadLength) return message;
        return new ProtocolErrorMessage(ProtocolErrorCode.InvalidRequest,
        [
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, field),
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, TooLargeDetailCode),
            new ProtocolErrorParameter("payloadBytes", payloadLength.ToString(CultureInfo.InvariantCulture)),
            new ProtocolErrorParameter("maximumPayloadBytes", ProtocolFrameHeader.MaxPayloadLength.ToString(CultureInfo.InvariantCulture)),
        ]);
    }

    internal static MultimodalTransitSnapshotMessage CreateSnapshot(MultimodalTransitSnapshot transit, ulong tickCount) =>
        CreateSnapshot(transit, tickCount, volume: null);

    internal static MultimodalTransitSnapshotMessage CreateSnapshot(MultimodalTransitSnapshot transit, ulong tickCount, WorldVolume volume) =>
        CreateSnapshot(transit, tickCount, (WorldVolume?)volume);

    private static MultimodalTransitSnapshotMessage CreateSnapshot(
        MultimodalTransitSnapshot transit,
        ulong tickCount,
        WorldVolume? volume)
    {
        ArgumentNullException.ThrowIfNull(transit);

        var tripById = transit.Trips.ToDictionary(static item => item.Id);
        var patternById = transit.Patterns.ToDictionary(static item => item.Id);

        IReadOnlyList<TransitVehicleSnapshot> selectedVehicles;
        IReadOnlySet<TransitServicePatternId>? selectedPatternIds = null;
        IReadOnlySet<TransitStopId>? selectedStopIds = null;
        if (volume is { } selectedVolume)
        {
            var visibleStopIds = transit.Stops
                .Where(item => selectedVolume.Contains(item.Position))
                .Select(static item => item.Id)
                .ToHashSet();
            var visibleVehicles = transit.Vehicles
                .Where(item => selectedVolume.Contains(item.Position))
                .ToArray();
            var patternIds = transit.Patterns
                .Where(pattern => pattern.Stops.Any(stop => visibleStopIds.Contains(stop.StopId)))
                .Select(static pattern => pattern.Id)
                .ToHashSet();
            foreach (var vehicle in visibleVehicles)
            {
                if (vehicle.TripId is { } tripId && tripById.TryGetValue(tripId, out var trip))
                    patternIds.Add(trip.PatternId);
            }

            // Pattern validation requires every referenced stop to be present. Pull the complete topology
            // only for patterns touching the subscription instead of publishing every world-wide pattern.
            var stopIds = new HashSet<TransitStopId>(visibleStopIds);
            foreach (var patternId in patternIds)
                if (patternById.TryGetValue(patternId, out var pattern))
                    foreach (var stop in pattern.Stops) stopIds.Add(stop.StopId);

            selectedVehicles = visibleVehicles;
            selectedPatternIds = patternIds;
            selectedStopIds = stopIds;
        }
        else
        {
            selectedVehicles = transit.Vehicles;
        }

        var selectedPatterns = selectedPatternIds is null
            ? transit.Patterns
            : transit.Patterns.Where(item => selectedPatternIds.Contains(item.Id)).ToArray();
        var lineIds = selectedPatterns.Select(static item => item.LineId).ToHashSet();
        var selectedLines = selectedPatternIds is null
            ? transit.Lines
            : transit.Lines.Where(item => lineIds.Contains(item.Id)).ToArray();
        var selectedStops = selectedStopIds is null
            ? transit.Stops
            : transit.Stops.Where(item => selectedStopIds.Contains(item.Id)).ToArray();

        var lines = selectedLines.Select(static item => new ProtocolTransitLine(item.Id.Value, (ProtocolTransitMode)item.Mode)).ToArray();
        var stops = selectedStops.Select(static item => new ProtocolTransitStop(
            item.Id.Value, (ProtocolTransitStopKind)item.Kind, item.Position.X, item.Position.Y, item.Position.Z,
            item.LaneId?.Value ?? 0, item.StationId?.Value ?? 0, item.PlatformId?.Value ?? 0)).ToArray();
        var patterns = selectedPatterns.Select(static item => new ProtocolTransitPattern(
            item.Id.Value, item.LineId.Value, item.RailwayServiceId?.Value ?? 0,
            item.Stops.Select(static stop => new ProtocolTransitPatternStop(stop.StopId.Value, stop.TravelTicksFromPrevious, stop.DwellTicks)).ToArray())).ToArray();
        var vehicles = selectedVehicles.Select(static item => new ProtocolTransitVehicle(
            item.Id.Value, (ProtocolTransitVehicleKind)item.Kind, item.TripId?.Value ?? 0, item.RoadVehicleId?.Value ?? 0, item.StopIndex,
            item.Position.X, item.Position.Y, item.Position.Z, (ProtocolTransitVehicleState)item.State, item.EstimatedArrivalTick, item.DwellUntilTick)).ToArray();

        var includedStopIds = selectedStops.Select(static item => item.Id).ToHashSet();
        var includedLineIds = selectedLines.Select(static item => item.Id).ToHashSet();
        var includedVehicleIds = selectedVehicles.Select(static item => item.Id).ToHashSet();
        var arrivals = new List<ProtocolTransitArrivalEstimate>();
        foreach (var vehicle in selectedVehicles)
        {
            if (vehicle.Kind != TransitVehicleKind.Bus || vehicle.State == TransitVehicleMovementState.Completed || vehicle.TripId is not { } tripId || !tripById.TryGetValue(tripId, out var trip) || !patternById.TryGetValue(trip.PatternId, out var pattern)) continue;
            var nextIndex = vehicle.StopIndex + 1;
            if (nextIndex >= pattern.Stops.Count) continue;
            var nextStopId = pattern.Stops[nextIndex].StopId;
            if (!includedStopIds.Contains(nextStopId) || !includedLineIds.Contains(pattern.LineId) || !includedVehicleIds.Contains(vehicle.Id)) continue;
            var travelTicks = pattern.Stops[nextIndex].TravelTicksFromPrevious;
            var eta = vehicle.State == TransitVehicleMovementState.EnRouteToStop
                ? vehicle.EstimatedArrivalTick
                : checked(Math.Max(vehicle.DwellUntilTick, trip.PlannedStartTick) + travelTicks);
            arrivals.Add(new ProtocolTransitArrivalEstimate(nextStopId.Value, pattern.LineId.Value, vehicle.Id.Value, eta));
        }
        return new MultimodalTransitSnapshotMessage(tickCount, lines, stops, patterns, vehicles, arrivals);
    }
}
