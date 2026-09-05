using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed record VersionedObservation<T>(
    ulong ObservationGeneration,
    ulong ObservationRevision,
    T Value);

internal sealed record PopulationPublishSnapshot(
    ulong ObservationGeneration,
    ulong ObservationRevision,
    ulong TickCount,
    PopulationStatistics Statistics,
    IReadOnlyDictionary<ulong, PersonSnapshot> InspectedPersons);
