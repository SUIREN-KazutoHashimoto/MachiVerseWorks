namespace MachiVerseWorks.Simulation;

public enum SettlementScale : byte { Hamlet = 0, Village = 1, Town = 2, City = 3, Metropolis = 4 }
public enum SettlementTrend : byte { Growing = 0, Stable = 1, Declining = 2, Recovering = 3, Dormant = 4 }
public enum BuildingLifecycleStatus : byte { Active = 0, Vacant = 1, Renovating = 2, Repurposing = 3, Abandoned = 4, Demolished = 5 }
public enum RegionalServiceKind : byte { Commerce = 0, Education = 1, Medical = 2 }
public enum InfrastructureDemandKind : byte { Road = 0, Transit = 1, Utility = 2 }
public enum RegionalRelationKind : byte { Commuting = 0, Trade = 1, Service = 2, Metro = 3 }
public enum RegionalEvolutionEventKind : byte
{
    Growth = 0, Decline = 1, ClassificationChanged = 2, ParcelDevelopment = 3,
    BuildingConstructed = 4, BuildingRenovated = 5, BuildingUseChanged = 6,
    BuildingVacated = 7, BuildingAbandoned = 8, BuildingDemolished = 9,
    SettlementEmergence = 10, SettlementDormancy = 11, SettlementRecovery = 12,
    RegionalRelationFormed = 13, RegionalRelationEnded = 14
}

public readonly record struct RegionalEvolutionEventId(ulong Value);
public readonly record struct RegionalRelationId(ulong Value);

public sealed record SettlementEvolutionState(
    SettlementId SettlementId, WorldPoint Center, int Population, int Jobs,
    double ServiceIndex, double Density, double Accessibility, double InfluenceRadiusMeters,
    SettlementScale Scale, SettlementTrend Trend, bool IsActive, int EstablishedYear, int? DormantSinceYear);

public sealed record ParcelEvolutionState(
    ParcelId ParcelId, SettlementId SettlementId, double DevelopmentDemand, double LandValue,
    ParcelDevelopmentState DevelopmentState, GeneratedBuildingId? BuildingId);

public sealed record BuildingLifecycleState(
    GeneratedBuildingId BuildingId, ParcelId ParcelId, GeneratedBuildingUse Use,
    int BuiltYear, int LastChangedYear, double Condition, double Occupancy,
    int Capacity, BuildingLifecycleStatus Status);

public sealed record ServiceCatchment(
    SettlementId SettlementId, RegionalServiceKind Kind, double RadiusMeters, double Coverage);

public sealed record InfrastructureDemandSignal(
    SettlementId SettlementId, InfrastructureDemandKind Kind, double Demand, string Reason);

public sealed record RegionalRelation(
    RegionalRelationId Id, SettlementId FromSettlementId, SettlementId ToSettlementId,
    RegionalRelationKind Kind, double Strength, bool IsActive, int SinceYear);

public sealed record RegionalEvolutionEvent(
    RegionalEvolutionEventId Id, int Year, RegionalEvolutionEventKind Kind,
    SettlementId SettlementId, GeneratedBuildingId? BuildingId, string Reason);

public sealed record PersistentRegionalEvolutionSnapshot(
    int CurrentYear, ulong TickCount,
    IReadOnlyList<SettlementEvolutionState> Settlements,
    IReadOnlyList<ParcelEvolutionState> Parcels,
    IReadOnlyList<BuildingLifecycleState> Buildings,
    IReadOnlyList<ServiceCatchment> ServiceCatchments,
    IReadOnlyList<InfrastructureDemandSignal> InfrastructureDemands,
    IReadOnlyList<RegionalRelation> Relations,
    IReadOnlyList<RegionalEvolutionEvent> Events);

public sealed record PersistentRegionalEvolutionCheckpoint(PersistentRegionalEvolutionSnapshot Snapshot);

public sealed record PersistentRegionalEvolutionOptions
{
    public const ulong DefaultTicksPerYear = EconomyDefaults.TicksPerEconomicDay * 365UL;
    public PersistentRegionalEvolutionOptions(ulong ticksPerYear = DefaultTicksPerYear)
    {
        if (ticksPerYear == 0) throw new ArgumentOutOfRangeException(nameof(ticksPerYear));
        TicksPerYear = ticksPerYear;
    }
    public ulong TicksPerYear { get; }
}

public readonly record struct RegionalEvolutionDrivers(
    double PopulationPressure, double JobPressure, double ServicePressure,
    double LogisticsPressure, double Connectivity, double InfrastructureCapacity)
{
    public static RegionalEvolutionDrivers Neutral => new(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);
}

public static class PersistentRegionalEvolutionEngine
{
    public static PersistentRegionalEvolutionSnapshot Initialize(RegionalGenerationSnapshot generation, int startYear = 0)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var settlements = generation.Settlements.OrderBy(x => x.Id.Value).Select(s =>
        {
            var accessibility = Math.Clamp((s.Suitability.TransportPotential + (1d - s.Suitability.Isolation)) * 0.5, 0d, 1d);
            var service = ServiceIndex(s.Role, s.Jobs, s.Population);
            var density = Density(s.Population, s.InfluenceRadiusMeters);
            return new SettlementEvolutionState(s.Id, s.Center, s.Population, s.Jobs, service, density, accessibility,
                s.InfluenceRadiusMeters, Classify(s.Population, s.Jobs, service, density, accessibility),
                SettlementTrend.Stable, true, startYear, null);
        }).ToArray();
        var parcels = generation.Parcels.OrderBy(x => x.Id.Value)
            .Select(p => new ParcelEvolutionState(p.Id, p.SettlementId, 0d, p.LandValue, p.DevelopmentState, p.BuildingId)).ToArray();
        var buildings = generation.Buildings.OrderBy(x => x.Id.Value)
            .Select(b => new BuildingLifecycleState(b.Id, b.ParcelId, b.Use, startYear - Math.Max(0, b.HistoricalStage * 12), startYear,
                Math.Clamp(1d - b.HistoricalStage * 0.08, 0.35, 1d), 0.85, b.Capacity, BuildingLifecycleStatus.Active)).ToArray();
        return BuildDerived(startYear, generation.TickCount, settlements, parcels, buildings, Array.Empty<RegionalEvolutionEvent>());
    }

    public static PersistentRegionalEvolutionSnapshot AdvanceYears(
        PersistentRegionalEvolutionSnapshot source, RegionalGenerationSnapshot generation, int years,
        Func<SettlementEvolutionState, RegionalEvolutionDrivers>? driverProvider = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(generation);
        if (years < 0) throw new ArgumentOutOfRangeException(nameof(years));
        var state = source;
        for (var i = 0; i < years; i++) state = AdvanceOneYear(state, generation, driverProvider);
        return state;
    }

    public static SettlementScale Classify(int population, int jobs, double services, double density, double accessibility)
    {
        if (population < 0 || jobs < 0) throw new ArgumentOutOfRangeException(nameof(population));
        var functional = jobs * 0.55 + population * 0.45;
        var quality = Math.Clamp((services + density + accessibility) / 3d, 0d, 1d);
        var score = functional * (0.65 + quality * 0.35);
        return score switch { < 500 => SettlementScale.Hamlet, < 2_500 => SettlementScale.Village, < 20_000 => SettlementScale.Town, < 150_000 => SettlementScale.City, _ => SettlementScale.Metropolis };
    }

    public static bool ShouldEmerge(int population, int jobs, double connectivity, double nearestInfluence)
        => population >= 300 && jobs >= 80 && connectivity >= 0.35 && nearestInfluence <= 0.65;

    private static PersistentRegionalEvolutionSnapshot AdvanceOneYear(
        PersistentRegionalEvolutionSnapshot source, RegionalGenerationSnapshot generation,
        Func<SettlementEvolutionState, RegionalEvolutionDrivers>? driverProvider)
    {
        var year = checked(source.CurrentYear + 1);
        var events = source.Events.ToList();
        var nextEventId = events.Count == 0 ? 1UL : checked(events.Max(x => x.Id.Value) + 1UL);
        var settlements = new SettlementEvolutionState[source.Settlements.Count];
        for (var i = 0; i < settlements.Length; i++)
        {
            var old = source.Settlements[i];
            var d = driverProvider?.Invoke(old) ?? RegionalEvolutionDrivers.Neutral;
            var balance = 0.28 * (d.JobPressure - 0.5) + 0.18 * (d.ServicePressure - 0.5) + 0.18 * (d.LogisticsPressure - 0.5)
                + 0.24 * (d.Connectivity - 0.5) + 0.12 * (d.InfrastructureCapacity - 0.5);
            var populationDelta = (int)Math.Round(old.Population * Math.Clamp(balance * 0.035, -0.025, 0.035));
            var jobsDelta = (int)Math.Round(old.Jobs * Math.Clamp(balance * 0.045, -0.035, 0.045));
            var population = Math.Max(0, old.Population + populationDelta);
            var jobs = Math.Max(0, old.Jobs + jobsDelta);
            var accessibility = Math.Clamp(old.Accessibility * 0.8 + d.Connectivity * 0.2, 0d, 1d);
            var service = Math.Clamp(old.ServiceIndex * 0.85 + d.ServicePressure * 0.15, 0d, 1d);
            var radius = Math.Clamp(250d + Math.Sqrt(Math.Max(1, population)) * 28d + jobs * 0.9d, 250d, 60_000d);
            var density = Density(population, radius);
            var scale = Classify(population, jobs, service, density, accessibility);
            var trend = populationDelta > Math.Max(1, old.Population / 500) ? (old.Trend == SettlementTrend.Declining ? SettlementTrend.Recovering : SettlementTrend.Growing)
                : populationDelta < -Math.Max(1, old.Population / 500) ? SettlementTrend.Declining : SettlementTrend.Stable;
            var active = population >= 20 || jobs >= 10;
            if (!active) trend = SettlementTrend.Dormant;
            var next = old with { Population = population, Jobs = jobs, ServiceIndex = service, Accessibility = accessibility,
                InfluenceRadiusMeters = radius, Density = density, Scale = scale, Trend = trend, IsActive = active,
                DormantSinceYear = active ? null : old.DormantSinceYear ?? year };
            settlements[i] = next;
            if (scale != old.Scale) events.Add(new(new(nextEventId++), year, RegionalEvolutionEventKind.ClassificationChanged, old.SettlementId, null, $"{old.Scale}->{scale}"));
            if (populationDelta != 0) events.Add(new(new(nextEventId++), year, populationDelta > 0 ? RegionalEvolutionEventKind.Growth : RegionalEvolutionEventKind.Decline, old.SettlementId, null, $"population {populationDelta:+#;-#;0}"));
            if (!active && old.IsActive) events.Add(new(new(nextEventId++), year, RegionalEvolutionEventKind.SettlementDormancy, old.SettlementId, null, "population and jobs fell below persistence threshold"));
            if (active && !old.IsActive) events.Add(new(new(nextEventId++), year, RegionalEvolutionEventKind.SettlementRecovery, old.SettlementId, null, "activity recovered"));
        }

        var settlementById = settlements.ToDictionary(x => x.SettlementId);
        var parcels = source.Parcels.Select(p =>
        {
            var s = settlementById[p.SettlementId];
            var pressure = Math.Clamp((s.Population / 20_000d + s.Jobs / 10_000d + s.Accessibility + s.ServiceIndex) / 4d, 0d, 1d);
            var demand = Math.Clamp(pressure * (0.45 + p.LandValue * 0.55), 0d, 1d);
            var development = p.DevelopmentState;
            if (p.BuildingId is null && demand >= 0.58) development = ParcelDevelopmentState.Developing;
            else if (p.BuildingId is not null) development = ParcelDevelopmentState.Occupied;
            return p with { DevelopmentDemand = demand, DevelopmentState = development, LandValue = Math.Clamp(p.LandValue * 0.9 + pressure * 0.1, 0d, 1d) };
        }).ToArray();

        var parcelById = parcels.ToDictionary(x => x.ParcelId);
        var buildings = source.Buildings.Select(b =>
        {
            var age = year - b.BuiltYear;
            var condition = Math.Clamp(b.Condition - (0.004 + age * 0.00008), 0d, 1d);
            var parcel = parcelById[b.ParcelId];
            var occupancy = Math.Clamp(b.Occupancy * 0.82 + parcel.DevelopmentDemand * 0.18, 0d, 1d);
            var status = occupancy < 0.08 && condition < 0.25 ? BuildingLifecycleStatus.Abandoned
                : occupancy < 0.18 ? BuildingLifecycleStatus.Vacant
                : condition < 0.35 && parcel.DevelopmentDemand > 0.62 ? BuildingLifecycleStatus.Renovating
                : BuildingLifecycleStatus.Active;
            if (age > 120 && condition < 0.12 && parcel.DevelopmentDemand < 0.35) status = BuildingLifecycleStatus.Demolished;
            if (status != b.Status) events.Add(new(new(nextEventId++), year, status switch
            {
                BuildingLifecycleStatus.Vacant => RegionalEvolutionEventKind.BuildingVacated,
                BuildingLifecycleStatus.Abandoned => RegionalEvolutionEventKind.BuildingAbandoned,
                BuildingLifecycleStatus.Demolished => RegionalEvolutionEventKind.BuildingDemolished,
                BuildingLifecycleStatus.Renovating => RegionalEvolutionEventKind.BuildingRenovated,
                _ => RegionalEvolutionEventKind.BuildingUseChanged
            }, parcel.SettlementId, b.BuildingId, $"{b.Status}->{status}"));
            if (status == BuildingLifecycleStatus.Renovating) condition = Math.Min(1d, condition + 0.35);
            return b with { Condition = condition, Occupancy = occupancy, Status = status, LastChangedYear = status == b.Status ? b.LastChangedYear : year };
        }).ToArray();
        return BuildDerived(year, source.TickCount, settlements, parcels, buildings, events);
    }

    private static PersistentRegionalEvolutionSnapshot BuildDerived(int year, ulong tickCount,
        IReadOnlyList<SettlementEvolutionState> settlements, IReadOnlyList<ParcelEvolutionState> parcels,
        IReadOnlyList<BuildingLifecycleState> buildings, IReadOnlyList<RegionalEvolutionEvent> events)
    {
        var catchments = settlements.Where(x => x.IsActive).SelectMany(s => Enum.GetValues<RegionalServiceKind>().Select(k =>
            new ServiceCatchment(s.SettlementId, k, s.InfluenceRadiusMeters * (0.65 + (int)k * 0.12), Math.Clamp(s.ServiceIndex * (0.9 + s.Accessibility * 0.1), 0d, 1d)))).ToArray();
        var demands = settlements.Where(x => x.IsActive).SelectMany(s => new[]
        {
            new InfrastructureDemandSignal(s.SettlementId, InfrastructureDemandKind.Road, Math.Clamp((s.Population / 50000d + s.Jobs / 25000d) * (1.1 - s.Accessibility), 0d, 1d), "population/jobs/accessibility"),
            new InfrastructureDemandSignal(s.SettlementId, InfrastructureDemandKind.Transit, Math.Clamp((s.Density + s.ServiceIndex) * 0.5 * (1.1 - s.Accessibility * 0.4), 0d, 1d), "density/services/accessibility"),
            new InfrastructureDemandSignal(s.SettlementId, InfrastructureDemandKind.Utility, Math.Clamp((s.Population / 80000d + s.Jobs / 40000d) * 0.5, 0d, 1d), "population/jobs")
        }).ToArray();
        var relations = BuildRelations(settlements, year);
        return new(year, tickCount, settlements.ToArray(), parcels.ToArray(), buildings.ToArray(), catchments, demands, relations, events.OrderBy(x => x.Id.Value).ToArray());
    }

    private static RegionalRelation[] BuildRelations(IReadOnlyList<SettlementEvolutionState> settlements, int year)
    {
        var result = new List<RegionalRelation>(); ulong id = 1;
        for (var i = 0; i < settlements.Count; i++) for (var j = i + 1; j < settlements.Count; j++)
        {
            var a = settlements[i]; var b = settlements[j]; if (!a.IsActive || !b.IsActive) continue;
            var dx = a.Center.X - b.Center.X; var dy = a.Center.Y - b.Center.Y; var distance = Math.Sqrt(dx * dx + dy * dy);
            var range = Math.Max(1d, a.InfluenceRadiusMeters + b.InfluenceRadiusMeters);
            var proximity = Math.Clamp(1d - distance / (range * 2d), 0d, 1d);
            var complement = Math.Clamp((Math.Min(a.Jobs, b.Population) + Math.Min(b.Jobs, a.Population)) / 40000d, 0d, 1d);
            var strength = Math.Clamp(proximity * 0.65 + complement * 0.35, 0d, 1d); if (strength < 0.15) continue;
            var kind = strength > 0.72 ? RegionalRelationKind.Metro : complement > proximity ? RegionalRelationKind.Trade : RegionalRelationKind.Commuting;
            result.Add(new(new RegionalRelationId(id++), a.SettlementId, b.SettlementId, kind, strength, true, year));
        }
        return result.ToArray();
    }

    private static double ServiceIndex(RegionalRole role, int jobs, int population)
        => Math.Clamp((role is RegionalRole.Market or RegionalRole.Administrative or RegionalRole.TransportHub ? 0.35 : 0.12) + jobs / Math.Max(1d, population + jobs) * 0.65, 0d, 1d);
    private static double Density(int population, double radiusMeters)
    {
        var km2 = Math.PI * Math.Pow(Math.Max(50d, radiusMeters) / 1000d, 2d);
        return Math.Clamp(population / Math.Max(1d, km2) / 10_000d, 0d, 1d);
    }
}
