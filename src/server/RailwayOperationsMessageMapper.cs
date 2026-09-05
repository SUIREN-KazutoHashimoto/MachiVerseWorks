using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class RailwayOperationsMessageMapper
{
    public static RailwayOperationsSnapshotMessage Create(RailwayOperationsSnapshot operations, TrainSnapshot[] visibleTrains, ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(operations); ArgumentNullException.ThrowIfNull(visibleTrains);
        var orderedVisibleTrains = visibleTrains.ToArray();
        Array.Sort(orderedVisibleTrains, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var serviceIds = orderedVisibleTrains.Select(static train => train.ServiceId).ToHashSet();
        var services = operations.Services.Where(service => serviceIds.Contains(service.Id)).OrderBy(static service => service.Id.Value).ToArray();
        var timetableIds = services.Select(static service => service.TimetableId).ToHashSet();
        var timetables = operations.Timetables.Where(timetable => timetableIds.Contains(timetable.Id)).OrderBy(static timetable => timetable.Id.Value).ToArray();
        return new RailwayOperationsSnapshotMessage(
            tickCount,
            orderedVisibleTrains.Select(static train => new ProtocolTrainState(
                train.Id.Value, train.FormationId.Value, train.ServiceId.Value, train.RouteId.Value,
                train.Position.X, train.Position.Y, train.Position.Z, train.Forward.X, train.Forward.Y, train.Forward.Z,
                train.SpeedMetersPerSecond, (byte)train.State,
                train.CurrentBlockId?.Value ?? 0, train.CurrentPlatformId?.Value ?? 0, train.AssignedPlatformId?.Value ?? 0, train.CurrentDepotId?.Value ?? 0, train.DwellDepartureTick)).ToArray(),
            services.Select(static service => new ProtocolRailwayServiceState(
                service.Id.Value, service.FormationId.Value, service.RouteId.Value, service.TimetableId.Value,
                service.OriginDepotId.Value, service.DestinationDepotId.Value, service.PlannedStartTick, (byte)service.State,
                service.DelayTicks, service.NextStopIndex, service.TrainId?.Value ?? 0)).ToArray(),
            timetables.Select(static timetable => new ProtocolTimetable(
                timetable.Id.Value,
                timetable.Stops.Select(static stop => new ProtocolTimetableStop(stop.StationId.Value, stop.PlannedArrivalTick, stop.PlannedDepartureTick, stop.MinimumDwellTicks, stop.PreferredPlatformId?.Value ?? 0)).ToArray())).ToArray());
    }
}
