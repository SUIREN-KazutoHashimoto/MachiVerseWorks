using System.Globalization;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class MultimodalTransitMessageMapper
{
    public const string TooLargeDetailCode = "multimodalTransitSnapshotTooLarge";

    public static IProtocolMessage Create(MultimodalTransitSnapshot transit, ulong tickCount)
    {
        var message = CreateSnapshot(transit, tickCount);
        var payloadLength = MultimodalTransitProtocolCodec.GetPayloadLength(message);
        if ((ulong)payloadLength <= ProtocolFrameHeader.MaxPayloadLength) return message;
        return new ProtocolErrorMessage(ProtocolErrorCode.InvalidRequest,
        [
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "snapshot"),
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, TooLargeDetailCode),
            new ProtocolErrorParameter("payloadBytes", payloadLength.ToString(CultureInfo.InvariantCulture)),
            new ProtocolErrorParameter("maximumPayloadBytes", ProtocolFrameHeader.MaxPayloadLength.ToString(CultureInfo.InvariantCulture)),
        ]);
    }

    internal static MultimodalTransitSnapshotMessage CreateSnapshot(MultimodalTransitSnapshot transit, ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(transit);
        var lines = transit.Lines.Select(static item => new ProtocolTransitLine(item.Id.Value, (ProtocolTransitMode)item.Mode)).ToArray();
        var stops = transit.Stops.Select(static item => new ProtocolTransitStop(
            item.Id.Value, (ProtocolTransitStopKind)item.Kind, item.Position.X, item.Position.Y, item.Position.Z,
            item.LaneId?.Value ?? 0, item.StationId?.Value ?? 0, item.PlatformId?.Value ?? 0)).ToArray();
        var patterns = transit.Patterns.Select(static item => new ProtocolTransitPattern(
            item.Id.Value, item.LineId.Value, item.RailwayServiceId?.Value ?? 0,
            item.Stops.Select(static stop => new ProtocolTransitPatternStop(stop.StopId.Value, stop.TravelTicksFromPrevious, stop.DwellTicks)).ToArray())).ToArray();
        var vehicles = transit.Vehicles.Select(static item => new ProtocolTransitVehicle(
            item.Id.Value, (ProtocolTransitVehicleKind)item.Kind, item.TripId?.Value ?? 0, item.RoadVehicleId?.Value ?? 0, item.StopIndex,
            item.Position.X, item.Position.Y, item.Position.Z, (ProtocolTransitVehicleState)item.State, item.EstimatedArrivalTick, item.DwellUntilTick)).ToArray();

        var tripById = transit.Trips.ToDictionary(static item => item.Id);
        var patternById = transit.Patterns.ToDictionary(static item => item.Id);
        var arrivals = new List<ProtocolTransitArrivalEstimate>();
        foreach (var vehicle in transit.Vehicles)
        {
            if (vehicle.Kind != TransitVehicleKind.Bus || vehicle.State == TransitVehicleMovementState.Completed || vehicle.TripId is not { } tripId || !tripById.TryGetValue(tripId, out var trip) || !patternById.TryGetValue(trip.PatternId, out var pattern)) continue;
            var nextIndex = vehicle.StopIndex + 1;
            if (nextIndex >= pattern.Stops.Count) continue;
            var travelTicks = pattern.Stops[nextIndex].TravelTicksFromPrevious;
            var eta = vehicle.State == TransitVehicleMovementState.EnRouteToStop
                ? vehicle.EstimatedArrivalTick
                : checked(Math.Max(vehicle.DwellUntilTick, trip.PlannedStartTick) + travelTicks);
            arrivals.Add(new ProtocolTransitArrivalEstimate(pattern.Stops[nextIndex].StopId.Value, pattern.LineId.Value, vehicle.Id.Value, eta));
        }
        return new MultimodalTransitSnapshotMessage(tickCount, lines, stops, patterns, vehicles, arrivals);
    }
}
