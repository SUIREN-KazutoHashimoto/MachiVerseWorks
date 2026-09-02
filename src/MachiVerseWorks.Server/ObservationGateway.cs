using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace MachiVerseWorks.Server;

/// <summary>
/// Detached, read-only boundary from the authoritative Simulation runtime into the Observation Gateway.
/// Gateway services may consume this contract but must not retain or mutate SimulationWorld state.
/// </summary>
internal interface IObservationSource
{
    int TickRate { get; }
    double SpatialCellSize { get; }

    SimulationPublishSnapshot CapturePublishSnapshot();
    PopulationPublishSnapshot CapturePopulationPublishSnapshot(IReadOnlySet<ulong> inspectedPersonIds);
    EconomySnapshot CaptureEconomySnapshot();
    LogisticsSnapshot CaptureLogisticsSnapshot();
    PowerSnapshot CapturePowerSnapshot();
    WaterSewerSnapshot CaptureWaterSewerSnapshot();
    (GasSnapshot Gas, LogisticsSnapshot Logistics) CaptureGasSnapshot();
    OpticalSnapshot CaptureOpticalSnapshot();
    RadioSnapshot CaptureRadioSnapshot();
    VersionedObservation<WorldEnvironmentSnapshot> CaptureWorldEnvironmentSnapshot(WorldVolume volume);
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

    public PopulationPublishSnapshot CapturePopulationPublishSnapshot(IReadOnlySet<ulong> inspectedPersonIds) =>
        simulation.CapturePopulationPublishSnapshot(inspectedPersonIds);

    public EconomySnapshot CaptureEconomySnapshot() => simulation.Read(static world => world.CreateEconomySnapshot());
    public LogisticsSnapshot CaptureLogisticsSnapshot() => simulation.Read(static world => world.CreateLogisticsSnapshot());
    public PowerSnapshot CapturePowerSnapshot() => simulation.Read(static world => world.CreatePowerSnapshot());
    public WaterSewerSnapshot CaptureWaterSewerSnapshot() => simulation.Read(static world => world.CreateWaterSewerSnapshot());

    public (GasSnapshot Gas, LogisticsSnapshot Logistics) CaptureGasSnapshot() =>
        simulation.Read(static world => (world.CreateGasSnapshot(), world.CreateLogisticsSnapshot()));

    public OpticalSnapshot CaptureOpticalSnapshot() => simulation.Read(static world => world.CreateOpticalSnapshot());
    public RadioSnapshot CaptureRadioSnapshot() => simulation.Read(static world => world.CreateRadioSnapshot());

    public VersionedObservation<WorldEnvironmentSnapshot> CaptureWorldEnvironmentSnapshot(WorldVolume volume) =>
        simulation.CaptureWorldEnvironmentSnapshot(volume);

    public bool PersonExists(ulong personId) =>
        personId != 0 && simulation.TryGetPersonSnapshot(new PersonId(personId), out _);
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
        services.AddSingleton<ClientConnectionRegistry>();
        services.AddSingleton<ObservationRequestQueue>();
        services.AddSingleton<SnapshotDeliveryScheduler>();
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
        return services;
    }
}
