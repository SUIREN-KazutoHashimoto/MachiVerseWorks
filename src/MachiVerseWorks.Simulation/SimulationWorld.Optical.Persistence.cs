namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private EconomyCheckpoint CreateEconomyCheckpointWithOptical() =>
        CreateEconomyCheckpointWithGas() with { Optical = CreateOpticalCheckpoint() };

    private OpticalCheckpoint CreateOpticalCheckpoint() => new(
        _nextOpticalNodeId,
        _nextFiberCableId,
        _nextOpticalEquipmentId,
        _nextOpticalBackhaulId,
        _nextOpticalDemandId,
        _opticalNodes.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationOpticalNodeCheckpoint(item.Id, item.Kind, item.Position)).ToArray(),
        _fiberCables.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationFiberCableCheckpoint(
                item.Id, item.FromNodeId, item.ToNodeId, item.CapacityGigabitsPerSecond, item.LoadGigabitsPerSecond, item.IsInService)).ToArray(),
        _opticalEquipment.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationOpticalEquipmentCheckpoint(
                item.Id, item.NodeId, item.Kind, item.BuildingId, item.EstablishmentId,
                item.CapacityGigabitsPerSecond, item.RequiresPower, item.IsInService, item.IsPowered)).ToArray(),
        _opticalBackhauls.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationOpticalBackhaulCheckpoint(
                item.Id, item.NodeId, item.CapacityGigabitsPerSecond, item.AllocatedGigabitsPerSecond, item.IsInService)).ToArray(),
        _opticalDemands.OrderBy(static item => item.Id.Value)
            .Select(static item => new SimulationOpticalDemandCheckpoint(
                item.Id, item.NodeId, item.Kind, item.BuildingId, item.EstablishmentId,
                item.BaseDemandGigabitsPerSecond, item.DemandGigabitsPerSecond, item.AllocatedGigabitsPerSecond,
                item.QualityState, item.BackhaulId, item.RouteCableIds.ToArray())).ToArray());

    private void RestoreOptical(OpticalCheckpoint? checkpoint)
    {
        if (checkpoint is null) return;
        _nextOpticalNodeId = checkpoint.NextNodeId;
        _nextFiberCableId = checkpoint.NextFiberCableId;
        _nextOpticalEquipmentId = checkpoint.NextEquipmentId;
        _nextOpticalBackhaulId = checkpoint.NextBackhaulId;
        _nextOpticalDemandId = checkpoint.NextDemandId;

        foreach (var item in checkpoint.Nodes.OrderBy(static item => item.Id.Value))
        {
            var state = new OpticalNodeState(item.Id, item.Kind, item.Position);
            _opticalNodes.Add(state);
            _opticalNodeIndex.Add(item.Id, state);
        }
        foreach (var item in checkpoint.FiberCables.OrderBy(static item => item.Id.Value))
        {
            var state = new FiberCableState(item.Id, item.FromNodeId, item.ToNodeId, item.CapacityGigabitsPerSecond, item.IsInService)
            {
                LoadGigabitsPerSecond = item.LoadGigabitsPerSecond,
            };
            _fiberCables.Add(state);
            _fiberCableIndex.Add(item.Id, state);
        }
        foreach (var item in checkpoint.Equipment.OrderBy(static item => item.Id.Value))
        {
            var state = new OpticalEquipmentState(
                item.Id, item.NodeId, item.Kind, item.BuildingId, item.EstablishmentId,
                item.CapacityGigabitsPerSecond, item.RequiresPower, item.IsInService)
            {
                IsPowered = item.IsPowered,
            };
            _opticalEquipment.Add(state);
            _opticalEquipmentIndex.Add(item.Id, state);
        }
        foreach (var item in checkpoint.Backhauls.OrderBy(static item => item.Id.Value))
        {
            var state = new OpticalBackhaulState(item.Id, item.NodeId, item.CapacityGigabitsPerSecond, item.IsInService)
            {
                AllocatedGigabitsPerSecond = item.AllocatedGigabitsPerSecond,
            };
            _opticalBackhauls.Add(state);
            _opticalBackhaulIndex.Add(item.Id, state);
        }
        foreach (var item in checkpoint.Demands.OrderBy(static item => item.Id.Value))
        {
            var state = new OpticalDemandState(
                item.Id, item.NodeId, item.Kind, item.BuildingId, item.EstablishmentId, item.BaseDemandGigabitsPerSecond)
            {
                DemandGigabitsPerSecond = item.DemandGigabitsPerSecond,
                AllocatedGigabitsPerSecond = item.AllocatedGigabitsPerSecond,
                QualityState = item.QualityState,
                BackhaulId = item.BackhaulId,
                RouteCableIds = item.RouteCableIds.ToArray(),
            };
            _opticalDemands.Add(state);
            _opticalDemandIndex.Add(item.Id, state);
        }
    }

    private static void ValidateOpticalCheckpoint(SimulationCheckpoint checkpoint)
    {
        var optical = checkpoint.Economy?.Optical;
        if (optical is null) return;
        ArgumentNullException.ThrowIfNull(optical.Nodes);
        ArgumentNullException.ThrowIfNull(optical.FiberCables);
        ArgumentNullException.ThrowIfNull(optical.Equipment);
        ArgumentNullException.ThrowIfNull(optical.Backhauls);
        ArgumentNullException.ThrowIfNull(optical.Demands);

        var nodeIds = ValidateOpticalCheckpointIds(
            optical.Nodes.Select(static item => item.Id.Value), optical.NextNodeId, "Optical node");
        var cableIds = ValidateOpticalCheckpointIds(
            optical.FiberCables.Select(static item => item.Id.Value), optical.NextFiberCableId, "Fiber cable");
        _ = ValidateOpticalCheckpointIds(
            optical.Equipment.Select(static item => item.Id.Value), optical.NextEquipmentId, "Optical equipment");
        var backhaulIds = ValidateOpticalCheckpointIds(
            optical.Backhauls.Select(static item => item.Id.Value), optical.NextBackhaulId, "Optical backhaul");
        _ = ValidateOpticalCheckpointIds(
            optical.Demands.Select(static item => item.Id.Value), optical.NextDemandId, "Optical demand");
        var buildings = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var establishments = checkpoint.Economy?.Establishments.Select(static item => item.Id).ToHashSet() ?? [];

        foreach (var item in optical.Nodes)
        {
            ValidateOpticalEnum(item.Kind, nameof(checkpoint));
            ValidatePoint(item.Position);
        }
        foreach (var item in optical.FiberCables)
        {
            if (!nodeIds.Contains(item.FromNodeId.Value) || !nodeIds.Contains(item.ToNodeId.Value) || item.FromNodeId == item.ToNodeId)
                throw new ArgumentException($"Fiber cable {item.Id.Value} references invalid Optical nodes.", nameof(checkpoint));
            ValidateOpticalPositiveFinite(item.CapacityGigabitsPerSecond, nameof(checkpoint));
            if (!IsOpticalNonNegativeFinite(item.LoadGigabitsPerSecond)
                || item.LoadGigabitsPerSecond > item.CapacityGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
                throw new ArgumentOutOfRangeException(nameof(checkpoint), "Fiber cable load must be within capacity.");
        }
        foreach (var item in optical.Equipment)
        {
            if (!nodeIds.Contains(item.NodeId.Value))
                throw new ArgumentException($"Optical equipment {item.Id.Value} references a missing node.", nameof(checkpoint));
            ValidateOpticalEnum(item.Kind, nameof(checkpoint));
            ValidateOpticalPositiveFinite(item.CapacityGigabitsPerSecond, nameof(checkpoint));
            if (item.BuildingId is { } buildingId && !buildings.Contains(buildingId))
                throw new ArgumentException($"Optical equipment {item.Id.Value} references a missing Building.", nameof(checkpoint));
            if (item.EstablishmentId is { } establishmentId && !establishments.Contains(establishmentId))
                throw new ArgumentException($"Optical equipment {item.Id.Value} references a missing Establishment.", nameof(checkpoint));
        }
        foreach (var item in optical.Backhauls)
        {
            if (!nodeIds.Contains(item.NodeId.Value))
                throw new ArgumentException($"Optical backhaul {item.Id.Value} references a missing node.", nameof(checkpoint));
            ValidateOpticalPositiveFinite(item.CapacityGigabitsPerSecond, nameof(checkpoint));
            if (!IsOpticalNonNegativeFinite(item.AllocatedGigabitsPerSecond)
                || item.AllocatedGigabitsPerSecond > item.CapacityGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
                throw new ArgumentOutOfRangeException(nameof(checkpoint), "Optical backhaul allocation must be within capacity.");
        }
        foreach (var item in optical.Demands)
        {
            if (!nodeIds.Contains(item.NodeId.Value))
                throw new ArgumentException($"Optical demand {item.Id.Value} references a missing node.", nameof(checkpoint));
            ValidateOpticalEnum(item.Kind, nameof(checkpoint));
            ValidateOpticalEnum(item.QualityState, nameof(checkpoint));
            ValidateOpticalPositiveFinite(item.BaseDemandGigabitsPerSecond, nameof(checkpoint));
            if (!IsOpticalNonNegativeFinite(item.DemandGigabitsPerSecond)
                || !IsOpticalNonNegativeFinite(item.AllocatedGigabitsPerSecond)
                || item.AllocatedGigabitsPerSecond > item.DemandGigabitsPerSecond + OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
                throw new ArgumentOutOfRangeException(nameof(checkpoint), "Optical demand allocation must be within demand.");
            if (item.BuildingId is { } buildingId && !buildings.Contains(buildingId))
                throw new ArgumentException($"Optical demand {item.Id.Value} references a missing Building.", nameof(checkpoint));
            if (item.EstablishmentId is { } establishmentId && !establishments.Contains(establishmentId))
                throw new ArgumentException($"Optical demand {item.Id.Value} references a missing Establishment.", nameof(checkpoint));
            if (item.BackhaulId is { } backhaulId && !backhaulIds.Contains(backhaulId.Value))
                throw new ArgumentException($"Optical demand {item.Id.Value} references a missing Backhaul.", nameof(checkpoint));
            foreach (var cableId in item.RouteCableIds)
                if (!cableIds.Contains(cableId.Value))
                    throw new ArgumentException($"Optical demand {item.Id.Value} route references a missing FiberCable.", nameof(checkpoint));
        }
    }

    private static HashSet<ulong> ValidateOpticalCheckpointIds(IEnumerable<ulong> ids, ulong nextId, string name)
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

    private static void EnsureOpticalIdCapacity(ulong nextId, string entityName)
    {
        if (nextId == 0 || nextId == ulong.MaxValue)
            throw new InvalidOperationException($"{entityName} ID capacity has been exhausted.");
    }

    private static void ValidateOpticalPositiveFinite(double value, string paramName)
    {
        if (!IsOpticalPositiveFinite(value)) throw new ArgumentOutOfRangeException(paramName, "Value must be finite and greater than zero.");
    }

    private static bool IsOpticalPositiveFinite(double value) => double.IsFinite(value) && value > 0d;
    private static bool IsOpticalNonNegativeFinite(double value) => double.IsFinite(value) && value >= 0d;

    private static void ValidateOpticalEnum<TEnum>(TEnum value, string paramName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(paramName, value, "Value is outside the supported Optical enum range.");
    }
}
