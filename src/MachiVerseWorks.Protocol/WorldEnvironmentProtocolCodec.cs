using System.Text.Json;

namespace MachiVerseWorks.Protocol;

public static class WorldEnvironmentProtocolCodec
{
    private const int MaximumSamples = 1_024;
    private const int MaximumFeatures = 256;
    private const int MaximumGeometryPointsPerFeature = 256;
    private const int MaximumToponyms = 256;
    private const int MaximumTextLength = 128;
    private static readonly JsonSerializerOptions SerializerOptions = new() { MaxDepth = 16 };

    public static byte[] Serialize(WorldEnvironmentSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsWorldEnvironment)
            throw new ArgumentOutOfRangeException(nameof(version), version, "World environment messages require Protocol 2.17 or newer.");
        Validate(message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if ((uint)payload.Length > ProtocolFrameHeader.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(message), "World environment snapshot exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payload.Length];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.WorldEnvironmentSnapshot, checked((uint)payload.Length)));
        payload.CopyTo(frame.AsSpan(ProtocolFrameHeader.Size));
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.WorldEnvironmentSnapshot)
        {
            error = ProtocolDecodeError.UnknownMessageType;
            return false;
        }
        if (!header.Version.SupportsWorldEnvironment)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        try
        {
            var message = JsonSerializer.Deserialize<WorldEnvironmentSnapshotMessage>(frame[ProtocolFrameHeader.Size..], SerializerOptions);
            if (message is null || !IsValid(message))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            envelope = new ProtocolEnvelope(header.Version, message);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (JsonException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        catch (NotSupportedException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static void Validate(WorldEnvironmentSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Samples);
        ArgumentNullException.ThrowIfNull(message.TerrainSamples);
        ArgumentNullException.ThrowIfNull(message.Features);
        ArgumentNullException.ThrowIfNull(message.Toponyms);
        if (!IsValid(message)) throw new ArgumentOutOfRangeException(nameof(message), "World environment snapshot contains invalid values.");
    }

    private static bool IsValid(WorldEnvironmentSnapshotMessage message)
    {
        if (message.Samples is null || message.TerrainSamples is null || message.Features is null || message.Toponyms is null) return false;
        if (message.Samples.Count > MaximumSamples || message.TerrainSamples.Count > MaximumSamples || message.Features.Count > MaximumFeatures || message.Toponyms.Count > MaximumToponyms) return false;
        if (message.TerrainSamples.Count != message.Samples.Count) return false;
        if (!Finite(message.MinX) || !Finite(message.MinY) || !Finite(message.MinZ) || !Finite(message.MaxX) || !Finite(message.MaxY) || !Finite(message.MaxZ)) return false;
        if (message.MaxX < message.MinX || message.MaxY < message.MinY || message.MaxZ < message.MinZ) return false;
        if (message.Config.WorldSeed == 0 || !Finite(message.Config.GeographicNorthX) || !Finite(message.Config.GeographicNorthY) || !Finite(message.Config.LatitudeDegrees) || message.Config.LatitudeDegrees is < -90d or > 90d) return false;
        if (!Finite(message.Config.SeaLevelMeters) || !Unit(message.Config.Continentality) || !Unit(message.Config.MaritimeInfluence) || !Finite(message.Config.MeanAnnualTemperatureCelsius) || !NonNegative(message.Config.SeasonalityCelsius) || !NonNegative(message.Config.AnnualPrecipitationMillimeters) || !Positive(message.Config.GlobalScaleMeters) || !Positive(message.Config.TerrainDetailScaleMeters)) return false;
        if (message.Config.HasConfiguredCoastlineDistance && !NonNegative(message.Config.ConfiguredCoastlineDistanceMeters)) return false;

        foreach (var sample in message.Samples)
        {
            if (!Finite(sample.X) || !Finite(sample.Y) || !Finite(sample.ElevationMeters) || !Finite(sample.CoastlineDistanceMeters) || sample.CoastlineDistanceMeters < 0d || !Finite(sample.LatitudeDegrees) || sample.LatitudeDegrees is < -90d or > 90d || !Finite(sample.MeanAnnualTemperatureCelsius) || !NonNegative(sample.SeasonalAmplitudeCelsius) || !NonNegative(sample.AnnualPrecipitationMillimeters) || !Unit(sample.MaritimeInfluence) || !Unit(sample.Continentality) || !Unit(sample.Drainage) || !Unit(sample.RiverStrength) || !Unit(sample.FloodRisk) || !Finite(sample.FlowDirectionX) || !Finite(sample.FlowDirectionY) || !Unit(sample.TerrainRuggedness) || !Unit(sample.Buildability) || !Unit(sample.SettlementScore)) return false;
        }
        foreach (var sample in message.TerrainSamples)
        {
            if (!Finite(sample.X) || !Finite(sample.Y) || !Finite(sample.Z) || !Finite(sample.NormalX) || !Finite(sample.NormalY) || !Finite(sample.NormalZ) || !NonNegative(sample.SlopeDegrees) || !Unit(sample.Roughness)) return false;
        }

        var featureIds = new HashSet<ulong>();
        foreach (var feature in message.Features)
        {
            if (feature is null || feature.FeatureId == 0 || !featureIds.Add(feature.FeatureId) || feature.Geometry is null || feature.Geometry.Count == 0 || feature.Geometry.Count > MaximumGeometryPointsPerFeature || !Positive(feature.AreaSquareMeters) || !Finite(feature.MinimumElevationMeters) || !Finite(feature.MaximumElevationMeters) || feature.MaximumElevationMeters < feature.MinimumElevationMeters) return false;
            if (!Finite(feature.MinX) || !Finite(feature.MinY) || !Finite(feature.MinZ) || !Finite(feature.MaxX) || !Finite(feature.MaxY) || !Finite(feature.MaxZ) || feature.MaxX < feature.MinX || feature.MaxY < feature.MinY || feature.MaxZ < feature.MinZ) return false;
            foreach (var point in feature.Geometry) if (!Finite(point.X) || !Finite(point.Y) || !Finite(point.Z)) return false;
        }
        foreach (var feature in message.Features) if (feature.ParentFeatureId != 0 && !featureIds.Contains(feature.ParentFeatureId)) return false;

        var toponymIds = new HashSet<ulong>();
        foreach (var toponym in message.Toponyms)
        {
            if (toponym is null || toponym.ToponymId == 0 || !toponymIds.Add(toponym.ToponymId) || !featureIds.Contains(toponym.FeatureId) || !featureIds.Contains(toponym.SourceFeatureId) || string.IsNullOrWhiteSpace(toponym.Name) || toponym.Name.Length > MaximumTextLength || string.IsNullOrWhiteSpace(toponym.GeneratorKey) || toponym.GeneratorKey.Length > MaximumTextLength) return false;
        }
        return true;
    }

    private static bool Finite(double value) => double.IsFinite(value);
    private static bool Unit(double value) => Finite(value) && value is >= 0d and <= 1d;
    private static bool NonNegative(double value) => Finite(value) && value >= 0d;
    private static bool Positive(double value) => Finite(value) && value > 0d;
}
