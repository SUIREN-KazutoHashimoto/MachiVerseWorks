namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private bool ContainsInfrastructureBuildingReference(BuildingId id) =>
        _powerLoads.Any(item => item.BuildingId == id)
        || _waterSewerServicePoints.Any(item => item.BuildingId == id)
        || _gasServicePoints.Any(item => item.BuildingId == id)
        || _opticalEquipment.Any(item => item.BuildingId == id)
        || _opticalDemands.Any(item => item.BuildingId == id)
        || _radioSiteInfrastructure.Values.Any(item => item.BuildingId == id);

    private bool ContainsLogisticsVehicleReference(VehicleId id) =>
        _logisticsShipments.Any(item => item.VehicleId == id);

    private bool ContainsLogisticsRoadAccessPointReference(RoadAccessPointId id) =>
        _logisticsInventories.Values.Any(item => item.RoadAccessPointId == id)
        || _logisticsShipments.Any(item => item.PickupAccessPointId == id || item.DeliveryAccessPointId == id);
}
