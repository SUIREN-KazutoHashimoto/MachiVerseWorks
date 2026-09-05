using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class LogisticsProtocolCodec
{
    private const int FixedPayloadLength = 68;
    private const int InventoryPayloadLength = 32;
    private const int ShipmentPayloadLength = 65;

    public static byte[] Serialize(LogisticsSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Inventories);
        ArgumentNullException.ThrowIfNull(message.Shipments);
        if (!version.SupportsLogistics)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Logistics messages require Protocol 2.11 or newer.");
        if (message.Inventories.Count > ushort.MaxValue || message.Shipments.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(message), "Logistics debug entry counts must fit in UInt16.");
        ValidateStatistics(message.Statistics, nameof(message));

        var payloadLength = checked(FixedPayloadLength
            + (message.Inventories.Count * InventoryPayloadLength)
            + (message.Shipments.Count * ShipmentPayloadLength));
        var frame = new byte[ProtocolFrameHeader.GetFrameLength(payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.LogisticsSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteStatistics(payload, message.Statistics);
        WriteUInt16(payload[64..], checked((ushort)message.Inventories.Count));
        WriteUInt16(payload[66..], checked((ushort)message.Shipments.Count));

        var offset = FixedPayloadLength;
        foreach (var inventory in message.Inventories)
        {
            ValidateInventory(inventory, nameof(message));
            WriteInventory(payload.Slice(offset, InventoryPayloadLength), inventory);
            offset += InventoryPayloadLength;
        }
        foreach (var shipment in message.Shipments)
        {
            ValidateShipment(shipment, nameof(message));
            WriteShipment(payload.Slice(offset, ShipmentPayloadLength), shipment);
            offset += ShipmentPayloadLength;
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsLogistics || header.MessageType != MessageType.LogisticsSnapshot)
        {
            error = header.MessageType == MessageType.LogisticsSnapshot ? ProtocolDecodeError.InvalidPayload : ProtocolDecodeError.UnknownMessageType;
            return false;
        }

        var payload = frame[ProtocolFrameHeader.Size..];
        if (payload.Length < FixedPayloadLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        var inventoryCount = ReadUInt16(payload[64..]);
        var shipmentCount = ReadUInt16(payload[66..]);
        int expectedLength;
        try
        {
            expectedLength = checked(FixedPayloadLength
                + (inventoryCount * InventoryPayloadLength)
                + (shipmentCount * ShipmentPayloadLength));
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

        var inventories = new ProtocolInventory[inventoryCount];
        var offset = FixedPayloadLength;
        for (var index = 0; index < inventories.Length; index++)
        {
            var inventory = ReadInventory(payload.Slice(offset, InventoryPayloadLength));
            if (!IsValidInventory(inventory))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            inventories[index] = inventory;
            offset += InventoryPayloadLength;
        }

        var shipments = new ProtocolShipment[shipmentCount];
        for (var index = 0; index < shipments.Length; index++)
        {
            var shipment = ReadShipment(payload.Slice(offset, ShipmentPayloadLength));
            if (!IsValidShipment(shipment))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            shipments[index] = shipment;
            offset += ShipmentPayloadLength;
        }

        envelope = new ProtocolEnvelope(header.Version, new LogisticsSnapshotMessage(
            statistics,
            Array.AsReadOnly(inventories),
            Array.AsReadOnly(shipments)));
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void WriteStatistics(Span<byte> payload, ProtocolLogisticsStatistics value)
    {
        WriteUInt32(payload, value.CommodityCount);
        WriteUInt32(payload[4..], value.InventoryCount);
        WriteUInt32(payload[8..], value.OpenOrderCount);
        WriteUInt32(payload[12..], value.ShipmentCount);
        WriteUInt32(payload[16..], value.InTransitShipmentCount);
        WriteUInt32(payload[20..], value.DelayedShipmentCount);
        WriteDouble(payload[24..], value.InventoryUnits);
        WriteDouble(payload[32..], value.InTransitUnits);
        WriteUInt64(payload[40..], value.DeliveredShipmentCount);
        WriteUInt64(payload[48..], value.LogisticsCycle);
        WriteUInt64(payload[56..], value.TickCount);
    }

    private static ProtocolLogisticsStatistics ReadStatistics(ReadOnlySpan<byte> payload) => new(
        ReadUInt32(payload),
        ReadUInt32(payload[4..]),
        ReadUInt32(payload[8..]),
        ReadUInt32(payload[12..]),
        ReadUInt32(payload[16..]),
        ReadUInt32(payload[20..]),
        ReadDouble(payload[24..]),
        ReadDouble(payload[32..]),
        ReadUInt64(payload[40..]),
        ReadUInt64(payload[48..]),
        ReadUInt64(payload[56..]));

    private static void WriteInventory(Span<byte> payload, ProtocolInventory value)
    {
        WriteUInt64(payload, value.EstablishmentId);
        WriteUInt64(payload[8..], value.CommodityId);
        WriteDouble(payload[16..], value.Quantity);
        WriteDouble(payload[24..], value.Capacity);
    }

    private static ProtocolInventory ReadInventory(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload),
        ReadUInt64(payload[8..]),
        ReadDouble(payload[16..]),
        ReadDouble(payload[24..]));

    private static void WriteShipment(Span<byte> payload, ProtocolShipment value)
    {
        WriteUInt64(payload, value.ShipmentId);
        WriteUInt64(payload[8..], value.OrderId);
        WriteUInt64(payload[16..], value.SourceEstablishmentId);
        WriteUInt64(payload[24..], value.DestinationEstablishmentId);
        WriteUInt64(payload[32..], value.CommodityId);
        WriteDouble(payload[40..], value.Quantity);
        payload[48] = (byte)value.State;
        WriteUInt64(payload[49..], value.VehicleId);
        WriteUInt64(payload[57..], value.DelayTicks);
    }

    private static ProtocolShipment ReadShipment(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload),
        ReadUInt64(payload[8..]),
        ReadUInt64(payload[16..]),
        ReadUInt64(payload[24..]),
        ReadUInt64(payload[32..]),
        ReadDouble(payload[40..]),
        (ProtocolShipmentState)payload[48],
        ReadUInt64(payload[49..]),
        ReadUInt64(payload[57..]));

    private static void ValidateStatistics(ProtocolLogisticsStatistics value, string parameterName)
    {
        if (!IsValidStatistics(value)) throw new ArgumentOutOfRangeException(parameterName, "Logistics statistics contain invalid values.");
    }

    private static bool IsValidStatistics(ProtocolLogisticsStatistics value) =>
        double.IsFinite(value.InventoryUnits) && value.InventoryUnits >= 0d
        && double.IsFinite(value.InTransitUnits) && value.InTransitUnits >= 0d;

    private static void ValidateInventory(ProtocolInventory value, string parameterName)
    {
        if (!IsValidInventory(value)) throw new ArgumentOutOfRangeException(parameterName, "Logistics inventory entry contains invalid values.");
    }

    private static bool IsValidInventory(ProtocolInventory value) =>
        value.EstablishmentId != 0 && value.CommodityId != 0
        && double.IsFinite(value.Quantity) && value.Quantity >= 0d
        && double.IsFinite(value.Capacity) && value.Capacity > 0d
        && value.Quantity <= value.Capacity;

    private static void ValidateShipment(ProtocolShipment value, string parameterName)
    {
        if (!IsValidShipment(value)) throw new ArgumentOutOfRangeException(parameterName, "Logistics shipment entry contains invalid values.");
    }

    private static bool IsValidShipment(ProtocolShipment value) =>
        value.ShipmentId != 0 && value.OrderId != 0 && value.SourceEstablishmentId != 0
        && value.DestinationEstablishmentId != 0 && value.CommodityId != 0
        && double.IsFinite(value.Quantity) && value.Quantity > 0d
        && Enum.IsDefined(value.State);

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
