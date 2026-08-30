from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(path: str, old: str, new: str) -> None:
    target = ROOT / path
    content = target.read_text(encoding="utf-8")
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one match in {path}, found {count}: {old!r}")
    target.write_text(content.replace(old, new, 1), encoding="utf-8")


replace_once(
    "tests/MachiVerseWorks.Persistence.Tests/RailwayInfrastructureSaveTests.cs",
    '"\\\"formatVersion\\\": 8"',
    '"\\\"formatVersion\\\": 9"',
)
replace_once(
    "src/MachiVerseWorks.Server/HostedServices.cs",
    "var railwayOperationsMessage = connection.NegotiatedVersion.SupportsRailwayOperations ? RailwayOperationsMessageMapper.Create(publishSnapshot.RailwayOperations, snapshot.Trains, snapshot.TickCount) : null;",
    "var railwayOperationsMessage = connection.NegotiatedVersion.SupportsRailwayOperations && snapshot.Trains.Length > 0 ? RailwayOperationsMessageMapper.Create(publishSnapshot.RailwayOperations, snapshot.Trains, snapshot.TickCount) : null;",
)
replace_once(
    "tests/MachiVerseWorks.Server.Tests/RailwayOperationsMessageMapperTests.cs",
    "for (var tick = 0; tick < 150; tick++) world.Step();",
    "for (var tick = 0; tick < 500; tick++) world.Step();",
)
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/RailwayOperationsTests.cs",
    "        Assert.IsTrue(completed.Services.All(static service => service.State == RailwayServiceState.Completed));",
    "        Assert.IsTrue(completed.Services.All(static service => service.State == RailwayServiceState.Completed), Describe(completed));",
)
replace_once(
    "tests/MachiVerseWorks.Simulation.Tests/RailwayOperationsTests.cs",
    "    }\n}\n",
    "    }\n\n    private static string Describe(RailwayOperationsSnapshot snapshot) => string.Join(\" | \", snapshot.Services.Select(static service => $\"S{service.Id.Value}:{service.State}:delay={service.DelayTicks}:next={service.NextStopIndex}\").Concat(snapshot.Trains.Select(static train => $\"T{train.Id.Value}:{train.State}:distance={train.RouteDistanceMeters:F3}:speed={train.SpeedMetersPerSecond:F3}:block={train.CurrentBlockId?.Value}:platform={train.CurrentPlatformId?.Value}:assigned={train.AssignedPlatformId?.Value}\")));\n}\n",
)

print("Phase18 integration refinements applied.")
