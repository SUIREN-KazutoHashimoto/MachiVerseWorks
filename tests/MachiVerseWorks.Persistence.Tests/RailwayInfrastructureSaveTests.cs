using System.Text;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class RailwayInfrastructureSaveTests
{
    [TestMethod]
    public void FormatEightRoundTripPreservesRailwayInfrastructure()
    {
        var world = new SimulationWorld();
        RailwayInfrastructureFixtures.SeedDeterministic(world);
        var expected = world.CreateCheckpoint();

        var bytes = WorldSaveSerializer.Serialize(world);
        var json = Encoding.UTF8.GetString(bytes);
        var restored = WorldSaveSerializer.Deserialize(bytes);
        var actual = restored.CreateCheckpoint();

        StringAssert.Contains(json, "\"formatVersion\": 8");
        Assert.AreEqual(expected.NextTrackNodeId, actual.NextTrackNodeId);
        Assert.AreEqual(expected.NextTrackSegmentId, actual.NextTrackSegmentId);
        Assert.AreEqual(expected.NextTrackConnectionId, actual.NextTrackConnectionId);
        Assert.AreEqual(expected.NextStationId, actual.NextStationId);
        Assert.AreEqual(expected.NextPlatformId, actual.NextPlatformId);
        Assert.AreEqual(expected.NextDepotId, actual.NextDepotId);
        CollectionAssert.AreEqual(expected.TrackNodes!.Select(static item => item.Id.Value).ToArray(), actual.TrackNodes!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.TrackSegments!.Select(static item => item.Id.Value).ToArray(), actual.TrackSegments!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.TrackConnections!.Select(static item => item.Id.Value).ToArray(), actual.TrackConnections!.Select(static item => item.Id.Value).ToArray());
        CollectionAssert.AreEqual(expected.PlatformAccessPoints!.Select(static item => item.RoadAccessPointId.Value).ToArray(), actual.PlatformAccessPoints!.Select(static item => item.RoadAccessPointId.Value).ToArray());
    }

    [TestMethod]
    public void PopulationFormatSevenMigratesWithEmptyRailwayState()
    {
        var world = new SimulationWorld();
        var json = Encoding.UTF8.GetString(WorldSaveSerializer.Serialize(world));
        json = json.Replace("\"formatVersion\": 8", "\"formatVersion\": 7", StringComparison.Ordinal);
        var railwayProperties = new[]
        {
            "nextTrackNodeId", "trackNodes", "nextTrackSegmentId", "trackSegments", "nextTrackConnectionId", "trackConnections",
            "nextBlockSectionId", "blockSections", "nextStationId", "stations", "nextPlatformId", "platforms",
            "nextPlatformAccessPointId", "platformAccessPoints", "nextDepotId", "depots",
        };
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        var simulation = root.GetProperty("simulation");
        var output = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 7);
            writer.WritePropertyName("simulation");
            writer.WriteStartObject();
            foreach (var property in simulation.EnumerateObject())
            {
                if (railwayProperties.Contains(property.Name, StringComparer.Ordinal)) continue;
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var restored = WorldSaveSerializer.Deserialize(output.ToArray());
        var checkpoint = restored.CreateCheckpoint();

        Assert.AreEqual(1UL, checkpoint.NextTrackNodeId);
        Assert.AreEqual(0, checkpoint.TrackNodes!.Count);
        Assert.AreEqual(0, checkpoint.TrackSegments!.Count);
        Assert.AreEqual(0, checkpoint.Stations!.Count);
    }
}
