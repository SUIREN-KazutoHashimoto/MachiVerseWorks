using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class LegacyElapsedSaveTests
{
    [TestMethod]
    public void FormatFiveLegacyRoundedElapsedIsNormalizedOnLoad()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 17));
        for (var index = 0; index < 3; index++) world.Step();

        var serialized = WorldSaveSerializer.Serialize(world);
        var root = JsonNode.Parse(Encoding.UTF8.GetString(serialized))!.AsObject();
        var simulation = root["simulation"]!.AsObject();
        var legacyElapsedTicks = checked(TimeSpan.FromSeconds(1d / 30d).Ticks * 3L);
        Assert.AreEqual(999_999L, legacyElapsedTicks);
        simulation["elapsedTicks"] = legacyElapsedTicks;

        var restored = WorldSaveSerializer.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));

        Assert.AreEqual(3UL, restored.Time.TickCount);
        Assert.AreEqual(1_000_000L, restored.Time.Elapsed.Ticks);
        using var normalizedDocument = JsonDocument.Parse(WorldSaveSerializer.Serialize(restored));
        Assert.AreEqual(1_000_000L, normalizedDocument.RootElement.GetProperty("simulation").GetProperty("elapsedTicks").GetInt64());
    }
}
