using System.Text.Json;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static partial class WorldSaveSerializer
{
    private static void ValidateMultimodalTransitCheckpointWithinLimits(MultimodalTransitCheckpoint? transit, WorldSaveLimits limits)
    {
        if (transit is null) return;
        ArgumentNullException.ThrowIfNull(transit.Stops);
        ArgumentNullException.ThrowIfNull(transit.Lines);
        ArgumentNullException.ThrowIfNull(transit.Patterns);
        ArgumentNullException.ThrowIfNull(transit.Trips);
        ArgumentNullException.ThrowIfNull(transit.Vehicles);
        ArgumentNullException.ThrowIfNull(transit.TaxiRequests);
        ArgumentNullException.ThrowIfNull(transit.Journeys);
        ArgumentNullException.ThrowIfNull(transit.Passengers);
        ValidateCount(transit.Stops.Count, limits.MaximumRoadAccessPointCount, "TransitStops");
        ValidateCount(transit.Lines.Count, limits.MaximumRoadSegmentCount, "TransitLines");
        ValidateCount(transit.Patterns.Count, limits.MaximumRoadSegmentCount, "TransitPatterns");
        ValidateCount(transit.Trips.Count, limits.MaximumVehicleCount, "TransitTrips");
        ValidateCount(transit.Vehicles.Count, limits.MaximumVehicleCount, "TransitVehicles");
        ValidateCount(transit.TaxiRequests.Count, limits.MaximumPersonCount, "TaxiRequests");
        ValidateCount(transit.Journeys.Count, limits.MaximumPersonCount, "Journeys");
        ValidateCount(transit.Passengers.Count, limits.MaximumPersonCount, "Passengers");
        var patternStopCount = transit.Patterns.Sum(static item => item.Stops?.Count ?? throw new InvalidDataException("Save Data is missing Transit Pattern stops."));
        var journeyLegCount = transit.Journeys.Sum(static item => item.Legs?.Count ?? throw new InvalidDataException("Save Data is missing Journey legs."));
        ValidateCount(patternStopCount, limits.MaximumLaneConnectionCount, "TransitPatternStops");
        ValidateCount(journeyLegCount, limits.MaximumLaneConnectionCount, "JourneyLegs");
    }

    private static void ValidateMultimodalTransitArrayCounts(ref Utf8JsonReader reader, WorldSaveLimits limits)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return;
        var depth = reader.CurrentDepth;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == depth) return;
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != depth + 1) continue;
            if (reader.ValueTextEquals("stops")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadAccessPointCount, "TransitStop");
            else if (reader.ValueTextEquals("lines")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadSegmentCount, "TransitLine");
            else if (reader.ValueTextEquals("patterns")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadSegmentCount, "TransitPattern");
            else if (reader.ValueTextEquals("trips")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "TransitTrip");
            else if (reader.ValueTextEquals("vehicles")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "TransitVehicle");
            else if (reader.ValueTextEquals("taxiRequests")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPersonCount, "TaxiRequest");
            else if (reader.ValueTextEquals("journeys")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPersonCount, "Journey");
            else if (reader.ValueTextEquals("passengers")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPersonCount, "Passenger");
        }
    }
}
