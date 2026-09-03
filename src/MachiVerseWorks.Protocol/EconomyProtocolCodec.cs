using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class EconomyProtocolCodec
{
    private const int FixedPayloadLength = 96;
    private const int CompanyPayloadLength = 57;
    private const int HouseholdPayloadLength = 32;

    public static byte[] Serialize(EconomySnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Companies);
        ArgumentNullException.ThrowIfNull(message.Households);
        if (!version.SupportsEconomy)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Economy messages require Protocol 2.10 or newer.");
        if (message.Companies.Count > ushort.MaxValue || message.Households.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(message), "Economy debug entry counts must fit in UInt16.");

        ValidateStatistics(message.Statistics, nameof(message));
        var payloadLength = checked(FixedPayloadLength
            + (message.Companies.Count * CompanyPayloadLength)
            + (message.Households.Count * HouseholdPayloadLength));
        var frame = new byte[ProtocolFrameHeader.GetFrameLength(payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.EconomySnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteStatistics(payload, message.Statistics);
        WriteUInt16(payload[92..], checked((ushort)message.Companies.Count));
        WriteUInt16(payload[94..], checked((ushort)message.Households.Count));

        var offset = FixedPayloadLength;
        for (var index = 0; index < message.Companies.Count; index++)
        {
            var company = message.Companies[index];
            ValidateCompany(company, nameof(message));
            WriteCompany(payload.Slice(offset, CompanyPayloadLength), company);
            offset += CompanyPayloadLength;
        }
        for (var index = 0; index < message.Households.Count; index++)
        {
            var household = message.Households[index];
            ValidateHousehold(household, nameof(message));
            WriteHousehold(payload.Slice(offset, HouseholdPayloadLength), household);
            offset += HouseholdPayloadLength;
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsEconomy || header.MessageType != MessageType.EconomySnapshot)
        {
            error = header.MessageType == MessageType.EconomySnapshot
                ? ProtocolDecodeError.InvalidPayload
                : ProtocolDecodeError.UnknownMessageType;
            return false;
        }

        var payload = frame[ProtocolFrameHeader.Size..];
        if (payload.Length < FixedPayloadLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        var companyCount = ReadUInt16(payload[92..]);
        var householdCount = ReadUInt16(payload[94..]);
        int expectedLength;
        try
        {
            expectedLength = checked(FixedPayloadLength
                + (companyCount * CompanyPayloadLength)
                + (householdCount * HouseholdPayloadLength));
        }
        catch (OverflowException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        if (payload.Length != expectedLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var statistics = ReadStatistics(payload);
        if (!IsValidStatistics(statistics))
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var companies = new ProtocolCompanyEconomy[companyCount];
        var offset = FixedPayloadLength;
        for (var index = 0; index < companies.Length; index++)
        {
            var company = ReadCompany(payload.Slice(offset, CompanyPayloadLength));
            if (!IsValidCompany(company))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            companies[index] = company;
            offset += CompanyPayloadLength;
        }

        var households = new ProtocolHouseholdEconomy[householdCount];
        for (var index = 0; index < households.Length; index++)
        {
            var household = ReadHousehold(payload.Slice(offset, HouseholdPayloadLength));
            if (!IsValidHousehold(household))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            households[index] = household;
            offset += HouseholdPayloadLength;
        }

        envelope = new ProtocolEnvelope(
            header.Version,
            new EconomySnapshotMessage(statistics, Array.AsReadOnly(companies), Array.AsReadOnly(households)));
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void WriteStatistics(Span<byte> payload, ProtocolEconomyStatistics value)
    {
        WriteUInt32(payload, value.CompanyCount);
        WriteUInt32(payload[4..], value.EstablishmentCount);
        WriteUInt32(payload[8..], value.JobCount);
        WriteUInt32(payload[12..], value.EmployedPersonCount);
        WriteUInt32(payload[16..], value.VacantPositionCount);
        WriteInt64(payload[20..], value.HouseholdCashBalance);
        WriteInt64(payload[28..], value.HouseholdIncome);
        WriteInt64(payload[36..], value.HouseholdSpending);
        WriteInt64(payload[44..], value.CompanyCashBalance);
        WriteInt64(payload[52..], value.CompanyRevenue);
        WriteInt64(payload[60..], value.CompanyExpense);
        WriteDouble(payload[68..], value.ProducedUnits);
        WriteUInt64(payload[76..], value.EconomicCycle);
        WriteUInt64(payload[84..], value.TickCount);
    }

    private static ProtocolEconomyStatistics ReadStatistics(ReadOnlySpan<byte> payload) => new(
        ReadUInt32(payload),
        ReadUInt32(payload[4..]),
        ReadUInt32(payload[8..]),
        ReadUInt32(payload[12..]),
        ReadUInt32(payload[16..]),
        ReadInt64(payload[20..]),
        ReadInt64(payload[28..]),
        ReadInt64(payload[36..]),
        ReadInt64(payload[44..]),
        ReadInt64(payload[52..]),
        ReadInt64(payload[60..]),
        ReadDouble(payload[68..]),
        ReadUInt64(payload[76..]),
        ReadUInt64(payload[84..]));

    private static void WriteCompany(Span<byte> payload, ProtocolCompanyEconomy value)
    {
        WriteUInt64(payload, value.CompanyId);
        payload[8] = (byte)value.Sector;
        WriteInt64(payload[9..], value.CashBalance);
        WriteInt64(payload[17..], value.Revenue);
        WriteInt64(payload[25..], value.Expense);
        WriteDouble(payload[33..], value.DailyProductionCapacity);
        WriteDouble(payload[41..], value.ProducedUnits);
        WriteUInt32(payload[49..], value.EstablishmentCount);
        WriteUInt32(payload[53..], value.EmployeeCount);
    }

    private static ProtocolCompanyEconomy ReadCompany(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload),
        (ProtocolIndustrySector)payload[8],
        ReadInt64(payload[9..]),
        ReadInt64(payload[17..]),
        ReadInt64(payload[25..]),
        ReadDouble(payload[33..]),
        ReadDouble(payload[41..]),
        ReadUInt32(payload[49..]),
        ReadUInt32(payload[53..]));

    private static void WriteHousehold(Span<byte> payload, ProtocolHouseholdEconomy value)
    {
        WriteUInt64(payload, value.HouseholdId);
        WriteInt64(payload[8..], value.CashBalance);
        WriteInt64(payload[16..], value.Income);
        WriteInt64(payload[24..], value.Spending);
    }

    private static ProtocolHouseholdEconomy ReadHousehold(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload),
        ReadInt64(payload[8..]),
        ReadInt64(payload[16..]),
        ReadInt64(payload[24..]));

    private static void ValidateStatistics(ProtocolEconomyStatistics value, string parameterName)
    {
        if (!IsValidStatistics(value)) throw new ArgumentOutOfRangeException(parameterName, "Economy statistics contain invalid values.");
    }

    private static bool IsValidStatistics(ProtocolEconomyStatistics value) =>
        value.HouseholdCashBalance >= 0
        && value.HouseholdIncome >= 0
        && value.HouseholdSpending >= 0
        && value.CompanyCashBalance >= 0
        && value.CompanyRevenue >= 0
        && value.CompanyExpense >= 0
        && double.IsFinite(value.ProducedUnits)
        && value.ProducedUnits >= 0d;

    private static void ValidateCompany(ProtocolCompanyEconomy value, string parameterName)
    {
        if (!IsValidCompany(value)) throw new ArgumentOutOfRangeException(parameterName, "Company economy state contains invalid values.");
    }

    private static bool IsValidCompany(ProtocolCompanyEconomy value) =>
        value.CompanyId != 0
        && Enum.IsDefined(value.Sector)
        && value.CashBalance >= 0
        && value.Revenue >= 0
        && value.Expense >= 0
        && double.IsFinite(value.DailyProductionCapacity)
        && value.DailyProductionCapacity >= 0d
        && double.IsFinite(value.ProducedUnits)
        && value.ProducedUnits >= 0d;

    private static void ValidateHousehold(ProtocolHouseholdEconomy value, string parameterName)
    {
        if (!IsValidHousehold(value)) throw new ArgumentOutOfRangeException(parameterName, "Household economy state contains invalid values.");
    }

    private static bool IsValidHousehold(ProtocolHouseholdEconomy value) =>
        value.HouseholdId != 0 && value.CashBalance >= 0 && value.Income >= 0 && value.Spending >= 0;

    private static void WriteUInt16(Span<byte> destination, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
    private static ushort ReadUInt16(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    private static void WriteUInt32(Span<byte> destination, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    private static uint ReadUInt32(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt32LittleEndian(source);
    private static void WriteUInt64(Span<byte> destination, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
    private static ulong ReadUInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt64LittleEndian(source);
    private static void WriteInt64(Span<byte> destination, long value) => BinaryPrimitives.WriteInt64LittleEndian(destination, value);
    private static long ReadInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadInt64LittleEndian(source);
    private static void WriteDouble(Span<byte> destination, double value) => WriteInt64(destination, BitConverter.DoubleToInt64Bits(value));
    private static double ReadDouble(ReadOnlySpan<byte> source) => BitConverter.Int64BitsToDouble(ReadInt64(source));
}
