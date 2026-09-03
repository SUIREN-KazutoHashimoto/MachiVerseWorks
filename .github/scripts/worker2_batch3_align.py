from pathlib import Path


def replace_once(path_name: str, old: str, new: str) -> None:
    path = Path(path_name)
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path_name}: expected one align target, found {count}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


# Keep TypeScript noUnusedLocals clean after the new relationship validation.
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    "const districtIds = uniquePositiveIds(districts.map((item) => item.districtId), 'District');",
    "uniquePositiveIds(districts.map((item) => item.districtId), 'District');")
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    "const parcelIds = uniquePositiveIds(parcels.map((item) => item.parcelId), 'Parcel');",
    "uniquePositiveIds(parcels.map((item) => item.parcelId), 'Parcel');")
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    "const buildingIds = uniquePositiveIds(buildings.map((item) => item.buildingId), 'Building');",
    "uniquePositiveIds(buildings.map((item) => item.buildingId), 'Building');")

# Keep the Simulation validator free from analyzer-only locals after dictionary-based validation.
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    "var parcelIds = ValidateRegionalIds(snapshot.Parcels.Select(static item => item.Id.Value), \"Parcel\");",
    "_ = ValidateRegionalIds(snapshot.Parcels.Select(static item => item.Id.Value), \"Parcel\");")
replace_once(
    "src/MachiVerseWorks.Simulation/SimulationWorld.RegionalGeneration.cs",
    "var buildingIds = ValidateRegionalIds(snapshot.Buildings.Select(static item => item.Id.Value), \"Generated building\");",
    "_ = ValidateRegionalIds(snapshot.Buildings.Select(static item => item.Id.Value), \"Generated building\");")

# Use the already-proven valid wire fixture for the #291 semantic-negative tests.
path = Path("tests/MachiVerseWorks.Protocol.Tests/RailwayOperationsProtocolTests.cs")
text = path.read_text(encoding="utf-8")
old = '''        var timetable = new ProtocolTimetable(5, [new ProtocolTimetableStop(11, 80, 100, 10, 0)]);
        var service = new ProtocolRailwayServiceState(3, 2, 4, 5, 6, 7, 1, 1, 0, 0, 1);
        var train = new ProtocolTrainState(1, 2, 3, 4, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 6, 0);
'''
new = '''        var timetable = new ProtocolTimetable(5, [new ProtocolTimetableStop(11, 80, 100, 10, 9), new ProtocolTimetableStop(12, 170, 190, 10, 0)]);
        var service = new ProtocolRailwayServiceState(3, 2, 4, 5, 6, 7, 1, 1, 18, 1, 1);
        var train = new ProtocolTrainState(1, 2, 3, 4, 10, 20, 3, 1, 0, 0, 12.5, 4, 8, 9, 10, 0, 140);
'''
if text.count(old) != 1:
    raise SystemExit("RailwayOperations semantic fixture align target mismatch")
path.write_text(text.replace(old, new, 1), encoding="utf-8")

# #287 regression test must construct Journey state through the public planner API.
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/MultimodalTransitTests.cs",
    '''        var world = CreateRoadWorld();
        var journey = world.CreateJourney(new TripRequestId(100), 0, [new JourneyLegSnapshot(TransitMode.Walk, null, null, null, null, null, null, 10)]);
        world.CreatePassenger(new TripRequestId(100), journey);
        var checkpoint = world.CreateCheckpoint();
''',
    '''        var world = CreateRoadWorld(withEndpoints: true);
        var lane = world.CreateRoadNetworkSnapshot().Lanes.Single().Id;
        var first = world.CreateBusStop(lane, new WorldPoint(20, 0, 0));
        var second = world.CreateBusStop(lane, new WorldPoint(80, 0, 0));
        var line = world.CreateTransitLine(TransitMode.Bus);
        world.CreateTransitServicePattern(line, [new(first, 0, 1), new(second, 10, 1)]);
        var request = new TripRequest(new TripRequestId(100), TripEndpoint.ForBuilding(new BuildingId(1)), TripEndpoint.ForBuilding(new BuildingId(2)));
        var journey = world.PlanMultimodalJourney(request);
        world.CreatePassenger(request.Id, journey);
        var checkpoint = world.CreateCheckpoint();
''')

print("Batch 3 alignment applied")
