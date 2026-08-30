using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed record VehicleSnapshotMessagePlan(
    IReadOnlyList<IProtocolMessage> Messages,
    HashSet<ulong> CurrentVehicleIds);

internal static class VehicleSnapshotMessagePlanner
{
    public static VehicleSnapshotMessagePlan Create(
        VehicleSnapshot[] snapshots,
        IReadOnlySet<ulong> knownVehicleIds,
        ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(knownVehicleIds);
        Array.Sort(snapshots, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var current = new HashSet<ulong>(snapshots.Length);
        var messages = new List<IProtocolMessage>(snapshots.Length + knownVehicleIds.Count);
        foreach (var snapshot in snapshots)
        {
            var id = snapshot.Id.Value;
            current.Add(id);
            var state = (ProtocolVehicleMovementState)snapshot.State;
            messages.Add(knownVehicleIds.Contains(id)
                ? new VehicleUpdateMessage(
                    id,
                    snapshot.LaneId.Value,
                    snapshot.Position.X,
                    snapshot.Position.Y,
                    snapshot.Position.Z,
                    snapshot.Forward.X,
                    snapshot.Forward.Y,
                    snapshot.Forward.Z,
                    snapshot.SpeedMetersPerSecond,
                    snapshot.Dimensions.LengthMeters,
                    snapshot.Dimensions.WidthMeters,
                    snapshot.Dimensions.HeightMeters,
                    state,
                    snapshot.TickCount)
                : new VehicleSpawnMessage(
                    id,
                    snapshot.LaneId.Value,
                    snapshot.Position.X,
                    snapshot.Position.Y,
                    snapshot.Position.Z,
                    snapshot.Forward.X,
                    snapshot.Forward.Y,
                    snapshot.Forward.Z,
                    snapshot.SpeedMetersPerSecond,
                    snapshot.Dimensions.LengthMeters,
                    snapshot.Dimensions.WidthMeters,
                    snapshot.Dimensions.HeightMeters,
                    state,
                    snapshot.TickCount));
        }
        foreach (var id in knownVehicleIds)
        {
            if (!current.Contains(id)) messages.Add(new VehicleRemoveMessage(id, tickCount));
        }
        return new VehicleSnapshotMessagePlan(messages, current);
    }
}

internal static class IntersectionControlMessageMapper
{
    public static IntersectionControlSnapshotMessage Create(IntersectionControllerSnapshot controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (controller.Movements.Count != controller.MovementStates.Count)
            throw new InvalidOperationException("Intersection movement metadata and state counts differ.");
        var movements = new ProtocolIntersectionMovementState[controller.Movements.Count];
        for (var index = 0; index < movements.Length; index++)
        {
            var movement = controller.Movements[index];
            var state = controller.MovementStates[index];
            if (movement.Id != state.MovementId)
                throw new InvalidOperationException("Intersection movement metadata and state order differ.");
            movements[index] = new ProtocolIntersectionMovementState(
                movement.Id.Value,
                movement.ConnectionId.Value,
                movement.FromLaneId.Value,
                movement.ToLaneId.Value,
                (ProtocolTurnMovement)movement.TurnMovement,
                movement.StopLinePosition.X,
                movement.StopLinePosition.Y,
                movement.StopLinePosition.Z,
                (ProtocolSignalIndication)state.Indication,
                checked((uint)state.QueueLength),
                state.EntryGrantedThisTick);
        }
        return new IntersectionControlSnapshotMessage(
            controller.TickCount,
            controller.IntersectionNodeId.Value,
            (ProtocolIntersectionControlMode)controller.Mode,
            checked((ushort)controller.PhaseIndex),
            controller.PhaseTick,
            movements);
    }
}
