using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class PopulationProtocolCodec
{
    private const int InspectPersonPayloadLength = 8;
    private const int PopulationStatisticsPayloadLength = 56;
    private const int PopulationStatisticsWithTransitPayloadLength = 60;
    private const int PersonDebugPayloadLength = 100;
    private const byte NullEnum = byte.MaxValue;

    public static byte[] Serialize(IProtocolMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsPopulation)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Population messages require Protocol 2.5 or newer.");

        var payloadLength = message switch
        {
            InspectPersonMessage => InspectPersonPayloadLength,
            PopulationStatisticsMessage => version.SupportsPopulationTransitCount ? PopulationStatisticsWithTransitPayloadLength : PopulationStatisticsPayloadLength,
            PersonDebugMessage => PersonDebugPayloadLength,
            _ => throw new ArgumentException($"Unsupported population message implementation: {message.GetType().FullName}.", nameof(message)),
        };
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, message.Type, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        switch (message)
        {
            case InspectPersonMessage inspect:
                ValidateStableId(inspect.PersonId, nameof(message));
                WriteUInt64(payload, inspect.PersonId);
                break;
            case PopulationStatisticsMessage statistics:
                WriteStatistics(payload, statistics, version);
                break;
            case PersonDebugMessage person:
                WritePerson(payload, person);
                break;
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsPopulation)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var payload = frame[ProtocolFrameHeader.Size..];
        IProtocolMessage message;
        switch (header.MessageType)
        {
            case MessageType.InspectPerson:
                if (payload.Length != InspectPersonPayloadLength || ReadUInt64(payload) == 0)
                {
                    error = ProtocolDecodeError.InvalidPayload;
                    return false;
                }
                message = new InspectPersonMessage(ReadUInt64(payload));
                break;
            case MessageType.PopulationStatistics:
                var expectedStatisticsLength = header.Version.SupportsPopulationTransitCount
                    ? PopulationStatisticsWithTransitPayloadLength
                    : PopulationStatisticsPayloadLength;
                if (payload.Length != expectedStatisticsLength)
                {
                    error = ProtocolDecodeError.InvalidPayload;
                    return false;
                }
                message = ReadStatistics(payload, header.Version);
                break;
            case MessageType.PersonDebug:
                if (!TryReadPerson(payload, out var person))
                {
                    error = ProtocolDecodeError.InvalidPayload;
                    return false;
                }
                message = person;
                break;
            default:
                error = ProtocolDecodeError.UnknownMessageType;
                return false;
        }

        envelope = new ProtocolEnvelope(header.Version, message);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void WriteStatistics(Span<byte> payload, PopulationStatisticsMessage message, ProtocolVersion version)
    {
        WriteUInt32(payload, message.HouseholdCount);
        WriteUInt32(payload[4..], message.PersonCount);
        WriteUInt32(payload[8..], message.AtActivityCount);
        WriteUInt32(payload[12..], message.WalkingCount);
        WriteUInt32(payload[16..], message.DrivingCount);
        WriteUInt32(payload[20..], message.HomeCount);
        WriteUInt32(payload[24..], message.WorkCount);
        WriteUInt32(payload[28..], message.EducationCount);
        WriteUInt32(payload[32..], message.ShoppingCount);
        WriteUInt32(payload[36..], message.HealthcareCount);
        WriteUInt32(payload[40..], message.RecreationCount);
        WriteUInt32(payload[44..], message.ErrandCount);
        WriteUInt64(payload[48..], message.TickCount);
        if (version.SupportsPopulationTransitCount) WriteUInt32(payload[56..], message.TransitCount);
    }

    private static PopulationStatisticsMessage ReadStatistics(ReadOnlySpan<byte> payload, ProtocolVersion version) => new(
        ReadUInt32(payload),
        ReadUInt32(payload[4..]),
        ReadUInt32(payload[8..]),
        ReadUInt32(payload[12..]),
        ReadUInt32(payload[16..]),
        ReadUInt32(payload[20..]),
        ReadUInt32(payload[24..]),
        ReadUInt32(payload[28..]),
        ReadUInt32(payload[32..]),
        ReadUInt32(payload[36..]),
        ReadUInt32(payload[40..]),
        ReadUInt32(payload[44..]),
        ReadUInt64(payload[48..]),
        version.SupportsPopulationTransitCount ? ReadUInt32(payload[56..]) : 0u);

    private static void WritePerson(Span<byte> payload, PersonDebugMessage message)
    {
        ValidateStableId(message.PersonId, nameof(message));
        ValidateStableId(message.HouseholdId, nameof(message));
        ValidateEndpoint(message.ResidenceBuildingId, message.ResidencePoiId, allowEmpty: false, nameof(message));
        ValidateEndpoint(message.CurrentBuildingId, message.CurrentPoiId, allowEmpty: false, nameof(message));
        ValidateEndpoint(message.DestinationBuildingId, message.DestinationPoiId, allowEmpty: true, nameof(message));
        ValidateEnum(message.CurrentActivity, nameof(message));
        ValidateEnum(message.TravelState, nameof(message));
        if (message.DestinationActivity is { } destinationActivity) ValidateEnum(destinationActivity, nameof(message));
        if (message.ActiveTravelMode is { } activeTravelMode) ValidateEnum(activeTravelMode, nameof(message));

        WriteUInt64(payload, message.PersonId);
        WriteUInt64(payload[8..], message.HouseholdId);
        WriteUInt64(payload[16..], message.ResidenceBuildingId);
        WriteUInt64(payload[24..], message.ResidencePoiId);
        WriteUInt64(payload[32..], message.CurrentBuildingId);
        WriteUInt64(payload[40..], message.CurrentPoiId);
        payload[48] = (byte)message.CurrentActivity;
        payload[49] = (byte)message.TravelState;
        WriteUInt64(payload[50..], message.DestinationBuildingId);
        WriteUInt64(payload[58..], message.DestinationPoiId);
        payload[66] = message.DestinationActivity is { } destination ? (byte)destination : NullEnum;
        WriteUInt64(payload[67..], message.ActiveTripRequestId);
        payload[75] = message.ActiveTravelMode is { } mode ? (byte)mode : NullEnum;
        WriteUInt64(payload[76..], message.PedestrianId);
        WriteUInt64(payload[84..], message.VehicleId);
        WriteUInt64(payload[92..], message.TickCount);
    }

    private static bool TryReadPerson(ReadOnlySpan<byte> payload, out PersonDebugMessage message)
    {
        message = null!;
        if (payload.Length != PersonDebugPayloadLength) return false;
        var personId = ReadUInt64(payload);
        var householdId = ReadUInt64(payload[8..]);
        var residenceBuildingId = ReadUInt64(payload[16..]);
        var residencePoiId = ReadUInt64(payload[24..]);
        var currentBuildingId = ReadUInt64(payload[32..]);
        var currentPoiId = ReadUInt64(payload[40..]);
        var currentActivity = (ProtocolActivityKind)payload[48];
        var travelState = (ProtocolPersonTravelState)payload[49];
        var destinationBuildingId = ReadUInt64(payload[50..]);
        var destinationPoiId = ReadUInt64(payload[58..]);
        var destinationActivityRaw = payload[66];
        var activeTripRequestId = ReadUInt64(payload[67..]);
        var activeTravelModeRaw = payload[75];
        var pedestrianId = ReadUInt64(payload[76..]);
        var vehicleId = ReadUInt64(payload[84..]);
        var tickCount = ReadUInt64(payload[92..]);

        if (personId == 0
            || householdId == 0
            || !IsValidEndpoint(residenceBuildingId, residencePoiId, allowEmpty: false)
            || !IsValidEndpoint(currentBuildingId, currentPoiId, allowEmpty: false)
            || !IsValidEndpoint(destinationBuildingId, destinationPoiId, allowEmpty: true)
            || !Enum.IsDefined(currentActivity)
            || !Enum.IsDefined(travelState)
            || (destinationActivityRaw != NullEnum && !Enum.IsDefined((ProtocolActivityKind)destinationActivityRaw))
            || (activeTravelModeRaw != NullEnum && !Enum.IsDefined((ProtocolTravelMode)activeTravelModeRaw)))
        {
            return false;
        }

        message = new PersonDebugMessage(
            personId,
            householdId,
            residenceBuildingId,
            residencePoiId,
            currentBuildingId,
            currentPoiId,
            currentActivity,
            travelState,
            destinationBuildingId,
            destinationPoiId,
            destinationActivityRaw == NullEnum ? null : (ProtocolActivityKind)destinationActivityRaw,
            activeTripRequestId,
            activeTravelModeRaw == NullEnum ? null : (ProtocolTravelMode)activeTravelModeRaw,
            pedestrianId,
            vehicleId,
            tickCount);
        return true;
    }

    private static void ValidateEndpoint(ulong buildingId, ulong poiId, bool allowEmpty, string parameterName)
    {
        if (!IsValidEndpoint(buildingId, poiId, allowEmpty))
            throw new ArgumentException("Protocol endpoint must reference exactly one Building or POI, or be empty where allowed.", parameterName);
    }

    private static bool IsValidEndpoint(ulong buildingId, ulong poiId, bool allowEmpty)
    {
        if (buildingId == 0 && poiId == 0) return allowEmpty;
        return (buildingId == 0) != (poiId == 0);
    }

    private static void ValidateStableId(ulong value, string parameterName)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(parameterName, "Protocol stable IDs must be greater than zero.");
    }

    private static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(parameterName, value, $"{typeof(T).Name} is invalid.");
    }

    private static void WriteUInt32(Span<byte> destination, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    private static uint ReadUInt32(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt32LittleEndian(source);
    private static void WriteUInt64(Span<byte> destination, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
    private static ulong ReadUInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt64LittleEndian(source);
}
