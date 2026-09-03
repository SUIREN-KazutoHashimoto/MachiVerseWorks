using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace MachiVerseWorks.Server;

internal readonly record struct RegionalGenerationObservation(
    ulong Generation,
    bool HasSnapshot,
    ulong SourceTick,
    RegionalGenerationSnapshot? Snapshot,
    ulong TickCount);

internal sealed record EntityInspectionObservation(
    PopulationPublishSnapshot Population,
    IReadOnlyDictionary<ulong, VehicleSnapshot> Vehicles,
    IReadOnlyDictionary<ulong, TrainSnapshot> Trains,
    IReadOnlyDictionary<ulong, ulong> GeneratedBuildingIds,
    PersistentRegionalEvolutionSnapshot? Regional);

/// <summary>
/// Detached, read-only boundary from the authoritative Simulation runtime into the Observation Gateway.
/// Gateway services may consume this contract but must not retain or mutate SimulationWorld state.
/// </summary>
internal interface IObservationSource
{
    int TickRate { get; }
    double SpatialCellSize { get; }

    SimulationPublishSnapshot CapturePublishSnapshot();
    SimulationPublishSnapshot CapturePublishSnapshot(WorldVolume volume) => CapturePublishSnapshot();
    PopulationPublishSnapshot CapturePopulationPublishSnapshot(IReadOnlySet<ulong> inspectedPersonIds);
    IReadOnlyDictionary<ulong, TrainSnapshot> CaptureTrainSnapshots(IReadOnlySet<ulong> inspectedTrainIds);
    IReadOnlyDictionary<ulong, VehicleSnapshot> CaptureVehicleSnapshots(IReadOnlySet<ulong> inspectedVehicleIds);
    IReadOnlyDictionary<ulong, ulong> CaptureGeneratedBuildingIds(IReadOnlySet<ulong> materializedBuildingIds) =>
        new Dictionary<ulong, ulong>();
    EconomySnapshot CaptureEconomySnapshot();
    LogisticsSnapshot CaptureLogisticsSnapshot();
    PowerSnapshot CapturePowerSnapshot();
    WaterSewerSnapshot CaptureWaterSewerSnapshot();
    (GasSnapshot Gas, LogisticsSnapshot Logistics) CaptureGasSnapshot();
    OpticalSnapshot CaptureOpticalSnapshot();
    RadioSnapshot CaptureRadioSnapshot();
    VersionedObservation<WorldEnvironmentSnapshot> CaptureWorldEnvironmentSnapshot(WorldVolume volume);
    RegionalGenerationObservation CaptureRegionalGenerationIdentity() => CaptureRegionalGenerationObservation() with { Snapshot = null };
    RegionalGenerationObservation CaptureRegionalGenerationObservation() => new(0, false, 0, null, 0);
    (PersistentRegionalEvolutionSnapshot Evolution, RegionalInteractionSnapshot Interactions)? CapturePersistentRegionalEvolutionSnapshot();
    EntityInspectionObservation CaptureEntityInspectionObservation(
        IReadOnlySet<ulong> inspectedPersonIds,
        IReadOnlySet<ulong> inspectedVehicleIds,
        IReadOnlySet<ulong> inspectedTrainIds,
        IReadOnlySet<ulong> materializedBuildingIds,
        EntityInspectionTarget? regionalTarget)
    {
        var population = CapturePopulationPublishSnapshot(inspectedPersonIds);
        var regional = CapturePersistentRegionalEvolutionSnapshot()?.Evolution;
        return new EntityInspectionObservation(
            population,
            CaptureVehicleSnapshots(inspectedVehicleIds),
            CaptureTrainSnapshots(inspectedTrainIds),
            CaptureGeneratedBuildingIds(materializedBuildingIds),
            regional);
    }
    bool PersonExists(ulong personId);
}

/// <summary>
/// The only Observation Gateway adapter allowed to read SimulationRuntime directly.
/// Every returned value is a detached snapshot/read model captured under the Simulation lock.
/// </summary>
internal sealed class SimulationObservationSource(SimulationRuntime simulation) : IObservationSource
{
    public int TickRate => simulation.TickRate;
    public double SpatialCellSize => simulation.SpatialCellSize;

    public SimulationPublishSnapshot CapturePublishSnapshot() => simulation.CapturePublishSnapshot();
    public SimulationPublishSnapshot CapturePublishSnapshot(WorldVolume volume) => simulation.CapturePublishSnapshot(volume);

    public PopulationPublishSnapshot CapturePopulationPublishSnapshot(IReadOnlySet<ulong> inspectedPersonIds) =>
        simulation.CapturePopulationPublishSnapshot(inspectedPersonIds);

    public IReadOnlyDictionary<ulong, TrainSnapshot> CaptureTrainSnapshots(IReadOnlySet<ulong> inspectedTrainIds)
    {
        ArgumentNullException.ThrowIfNull(inspectedTrainIds);
        if (inspectedTrainIds.Count == 0) return new Dictionary<ulong, TrainSnapshot>();
        return simulation.Read(world =>
        {
            var result = new Dictionary<ulong, TrainSnapshot>(inspectedTrainIds.Count);
            foreach (var id in inspectedTrainIds)
            {
                if (world.TryGetTrainSnapshot(new TrainId(id), out var snapshot)) result.Add(id, snapshot);
            }
            return result;
        });
    }

    public IReadOnlyDictionary<ulong, VehicleSnapshot> CaptureVehicleSnapshots(IReadOnlySet<ulong> inspectedVehicleIds)
    {
        ArgumentNullException.ThrowIfNull(inspectedVehicleIds);
        if (inspectedVehicleIds.Count == 0) return new Dictionary<ulong, VehicleSnapshot>();
        return simulation.Read(world =>
        {
            var result = new Dictionary<ulong, VehicleSnapshot>(inspectedVehicleIds.Count);
            foreach (var id in inspectedVehicleIds)
            {
                if (world.TryGetVehicleSnapshot(new VehicleId(id), out var snapshot)) result.Add(id, snapshot);
            }
            return result;
        });
    }

    public IReadOnlyDictionary<ulong, ulong> CaptureGeneratedBuildingIds(IReadOnlySet<ulong> materializedBuildingIds)
    {
        ArgumentNullException.ThrowIfNull(materializedBuildingIds);
        if (materializedBuildingIds.Count == 0) return new Dictionary<ulong, ulong>();
        return simulation.Read(world =>
        {
            var result = new Dictionary<ulong, ulong>(materializedBuildingIds.Count);
            foreach (var id in materializedBuildingIds)
            {
                if (world.TryGetGeneratedBuildingId(new BuildingId(id), out var generatedId))
                    result.Add(id, generatedId.Value);
            }
            return result;
        });
    }

    public EconomySnapshot CaptureEconomySnapshot() => simulation.Read(static world => world.CreateEconomySnapshot());
    public LogisticsSnapshot CaptureLogisticsSnapshot() => simulation.Read(static world => world.CreateLogisticsSnapshot());
    public PowerSnapshot CapturePowerSnapshot() => simulation.Read(static world => world.CreatePowerSnapshot());
    public WaterSewerSnapshot CaptureWaterSewerSnapshot() => simulation.Read(static world => world.CreateWaterSewerSnapshot());

    public (GasSnapshot Gas, LogisticsSnapshot Logistics) CaptureGasSnapshot() =>
        simulation.Read(static world => (world.CreateGasSnapshot(), world.CreateLogisticsSnapshot()));

    public OpticalSnapshot CaptureOpticalSnapshot() => simulation.Read(static world => world.CreateOpticalSnapshot());
    public RadioSnapshot CaptureRadioSnapshot() => simulation.Read(static world => world.CreateRadioSnapshot());

    public VersionedObservation<WorldEnvironmentSnapshot> CaptureWorldEnvironmentSnapshot(WorldVolume volume)
    {
        var context = simulation.Read(world => new EnvironmentObservationContext(
            simulation.ObservationGeneration,
            simulation.ObservationRevision,
            world.Time.TickCount,
            world.WorldEnvironment));
        var snapshot = SimulationWorld.CreateDetachedDetailedWorldEnvironmentSnapshot(context.Config, context.TickCount, volume);
        return new VersionedObservation<WorldEnvironmentSnapshot>(context.Generation, context.Revision, snapshot);
    }

    public RegionalGenerationObservation CaptureRegionalGenerationIdentity() =>
        simulation.Read(world =>
        {
            var hasSnapshot = world.TryGetRegionalGenerationSourceTick(out var sourceTick);
            return new RegionalGenerationObservation(
                simulation.ObservationGeneration,
                hasSnapshot,
                sourceTick,
                null,
                world.Time.TickCount);
        });

    public RegionalGenerationObservation CaptureRegionalGenerationObservation() =>
        simulation.Read(world =>
        {
            var snapshot = world.TryCreateRegionalGenerationSnapshot(out var captured) ? captured : null;
            return new RegionalGenerationObservation(
                simulation.ObservationGeneration,
                snapshot is not null,
                snapshot?.TickCount ?? 0UL,
                snapshot,
                world.Time.TickCount);
        });

    public (PersistentRegionalEvolutionSnapshot Evolution, RegionalInteractionSnapshot Interactions)? CapturePersistentRegionalEvolutionSnapshot() =>
        simulation.Read<(PersistentRegionalEvolutionSnapshot Evolution, RegionalInteractionSnapshot Interactions)?>(static world =>
        {
            if (!world.TryCreatePersistentRegionalEvolutionSnapshot(out var evolution) || evolution is null) return null;
            return (evolution, world.CreateRegionalInteractionSnapshot());
        });

    public EntityInspectionObservation CaptureEntityInspectionObservation(
        IReadOnlySet<ulong> inspectedPersonIds,
        IReadOnlySet<ulong> inspectedVehicleIds,
        IReadOnlySet<ulong> inspectedTrainIds,
        IReadOnlySet<ulong> materializedBuildingIds,
        EntityInspectionTarget? regionalTarget)
    {
        ArgumentNullException.ThrowIfNull(inspectedPersonIds);
        ArgumentNullException.ThrowIfNull(inspectedVehicleIds);
        ArgumentNullException.ThrowIfNull(inspectedTrainIds);
        ArgumentNullException.ThrowIfNull(materializedBuildingIds);

        return simulation.Read(world =>
        {
            var generation = simulation.ObservationGeneration;
            var revision = simulation.ObservationRevision;
            var tickCount = world.Time.TickCount;
            var persons = new Dictionary<ulong, PersonSnapshot>(inspectedPersonIds.Count);
            foreach (var id in inspectedPersonIds)
                if (world.TryGetPersonSnapshot(new PersonId(id), out var person)) persons.Add(id, person);
            var population = new PopulationPublishSnapshot(
                generation,
                revision,
                tickCount,
                world.CreatePopulationStatistics(),
                persons);

            var vehicles = new Dictionary<ulong, VehicleSnapshot>(inspectedVehicleIds.Count);
            foreach (var id in inspectedVehicleIds)
                if (world.TryGetVehicleSnapshot(new VehicleId(id), out var vehicle)) vehicles.Add(id, vehicle);

            var trains = new Dictionary<ulong, TrainSnapshot>(inspectedTrainIds.Count);
            foreach (var id in inspectedTrainIds)
                if (world.TryGetTrainSnapshot(new TrainId(id), out var train)) trains.Add(id, train);

            var generatedBuildingIds = new Dictionary<ulong, ulong>(materializedBuildingIds.Count);
            foreach (var id in materializedBuildingIds)
                if (world.TryGetGeneratedBuildingId(new BuildingId(id), out var generatedId))
                    generatedBuildingIds.Add(id, generatedId.Value);

            PersistentRegionalEvolutionSnapshot? regional = regionalTarget switch
            {
                { EntityType: ProtocolEntityType.Settlement } target =>
                    world.CreatePersistentRegionalEvolutionInspectionSnapshot(settlementId: new SettlementId(target.EntityId)),
                { EntityType: ProtocolEntityType.Parcel } target =>
                    world.CreatePersistentRegionalEvolutionInspectionSnapshot(parcelId: new ParcelId(target.EntityId)),
                { EntityType: ProtocolEntityType.Building } target =>
                    world.CreatePersistentRegionalEvolutionInspectionSnapshot(buildingId: new GeneratedBuildingId(target.EntityId)),
                _ => null,
            };

            return new EntityInspectionObservation(population, vehicles, trains, generatedBuildingIds, regional);
        });
    }

    public bool PersonExists(ulong personId) =>
        personId != 0 && simulation.TryGetPersonSnapshot(new PersonId(personId), out _);

    private readonly record struct EnvironmentObservationContext(
        ulong Generation,
        ulong Revision,
        ulong TickCount,
        WorldEnvironmentConfig Config);
}

/// <summary>
/// Gateway-owned Protocol adaptation. Domain payload semantics are produced before this boundary;
/// this type only selects the codec required by the negotiated wire contract.
/// </summary>
internal static class ObservationProtocolAdapter
{
    public static byte[] Serialize(IProtocolMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message switch
        {
            IntersectionControlSnapshotMessage intersection => IntersectionControlProtocolCodec.Serialize(intersection, version),
            RailwayInfrastructureSnapshotMessage railway => RailwayInfrastructureProtocolCodec.Serialize(railway, version),
            RailwayOperationsSnapshotMessage railwayOperations => RailwayOperationsProtocolCodec.Serialize(railwayOperations, version),
            MultimodalTransitSnapshotMessage multimodalTransit => MultimodalTransitProtocolCodec.Serialize(multimodalTransit, version),
            EconomySnapshotMessage economy => EconomyProtocolCodec.Serialize(economy, version),
            LogisticsSnapshotMessage logistics => LogisticsProtocolCodec.Serialize(logistics, version),
            PowerSnapshotMessage power => PowerProtocolCodec.Serialize(power, version),
            WaterSewerSnapshotMessage waterSewer => WaterSewerProtocolCodec.Serialize(waterSewer, version),
            GasSnapshotMessage gas => GasProtocolCodec.Serialize(gas, version),
            OpticalSnapshotMessage optical => OpticalProtocolCodec.Serialize(optical, version),
            RadioSnapshotMessage radio => RadioProtocolCodec.Serialize(radio, version),
            SpectrumSnapshotMessage spectrum => RadioProtocolCodec.Serialize(spectrum, version),
            WorldEnvironmentSnapshotMessage worldEnvironment => WorldEnvironmentProtocolCodec.Serialize(worldEnvironment, version),
            RegionalGenerationSnapshotMessage regionalGeneration => RegionalGenerationProtocolCodec.Serialize(regionalGeneration, version),
            RegionalGenerationSnapshotChunkMessage regionalGenerationChunk => RegionalGenerationSnapshotChunkProtocolCodec.Serialize(regionalGenerationChunk, version),
            PersistentRegionalEvolutionSnapshotMessage regionalEvolution => PersistentRegionalEvolutionProtocolCodec.Serialize(regionalEvolution, version),
            InspectEntityMessage or ClearEntityInspectionMessage or EntityInspectionSnapshotMessage => EntityInspectionProtocolCodec.Serialize(message, version),
            InspectPersonMessage or PopulationStatisticsMessage or PersonDebugMessage => PopulationProtocolCodec.Serialize(message, version),
            _ => ProtocolCodec.Serialize(message, version),
        };
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error))
        {
            envelope = null;
            return false;
        }

        if (header.MessageType == MessageType.ClearPersonInspection)
            return PersonInspectionProtocolCodec.TryDeserialize(frame, out envelope, out error);
        if (header.MessageType is MessageType.InspectEntity or MessageType.ClearEntityInspection or MessageType.EntityInspectionSnapshot)
            return EntityInspectionProtocolCodec.TryDeserialize(frame, out envelope, out error);

        return header.MessageType is MessageType.InspectPerson or MessageType.PopulationStatistics or MessageType.PersonDebug
            ? PopulationProtocolCodec.TryDeserialize(frame, out envelope, out error)
            : ProtocolCodec.TryDeserialize(frame, out envelope, out error);
    }
}

internal static class ObservationGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddObservationGateway(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IObservationSource, SimulationObservationSource>();
        services.AddSingleton<ObservationCache>();
        services.AddSingleton<SnapshotDeliveryScheduler>();
        services.AddSingleton<ClientConnectionRegistry>();
        services.AddSingleton<ObservationDeliveryCoordinator>();
        services.AddSingleton<ObservationRequestQueue>();
        services.AddSingleton<EntityInspectionRegistry>();
        services.AddSingleton<WebSocketSessionHandler>();
        services.AddHostedService<ObservationRequestProcessor>();
        services.AddHostedService<SnapshotPublishService>();
        services.AddHostedService<PopulationPublishService>();
        services.AddHostedService<EconomyPublishService>();
        services.AddHostedService<LogisticsPublishService>();
        services.AddHostedService<PowerPublishService>();
        services.AddHostedService<WaterSewerPublishService>();
        services.AddHostedService<GasPublishService>();
        services.AddHostedService<OpticalPublishService>();
        services.AddHostedService<RadioPublishService>();
        services.AddHostedService<WorldEnvironmentPublishService>();
        services.AddHostedService<RegionalGenerationPublishService>();
        services.AddHostedService<PersistentRegionalEvolutionPublishService>();
        return services;
    }
}
