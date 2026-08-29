using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class SaveDataRequiredFieldTests
{
    [TestMethod]
    public void MissingPoiBuildingIdFieldIsRejected()
    {
        var json = """
            {
              "formatVersion": 3,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": [],
                "nextBuildingId": 1,
                "buildings": [],
                "nextPoiId": 2,
                "pois": [
                  { "id": 1, "kind": 0, "x": 0, "y": 0, "z": 0 }
                ]
              }
            }
            """u8.ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [TestMethod]
    public void ExplicitNullPoiBuildingIdIsAccepted()
    {
        var json = """
            {
              "formatVersion": 3,
              "simulation": {
                "tickRate": 30,
                "seed": 1,
                "spatialCellSize": 64,
                "tickCount": 0,
                "elapsedTicks": 0,
                "randomState": 1,
                "nextAgentId": 1,
                "agents": [],
                "nextBuildingId": 1,
                "buildings": [],
                "nextPoiId": 2,
                "pois": [
                  { "id": 1, "kind": 0, "x": 0, "y": 0, "z": 0, "buildingId": null }
                ]
              }
            }
            """u8.ToArray();

        var world = WorldSaveSerializer.Deserialize(json);
        var pois = world.CreatePoiSnapshot();

        Assert.AreEqual(1, pois.Length);
        Assert.IsNull(pois[0].BuildingId);
    }
}
