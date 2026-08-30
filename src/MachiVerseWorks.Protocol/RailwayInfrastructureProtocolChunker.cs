namespace MachiVerseWorks.Protocol;

public static class RailwayInfrastructureProtocolChunker
{
    private const int SnapshotHeaderLength = 41;
    private const int NodeLength = 33;
    private const int SegmentLength = 43;
    private const int ConnectionLength = 32;
    private const int StationLength = 56;
    private const int PlatformLength = 88;
    private const int AccessPointLength = 24;
    private const int BlockHeaderLength = 12;
    private const int DepotHeaderLength = 60;

    public static IReadOnlyList<RailwayInfrastructureSnapshotMessage> Split(RailwayInfrastructureSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Segments);
        ArgumentNullException.ThrowIfNull(message.Connections);
        ArgumentNullException.ThrowIfNull(message.Blocks);
        ArgumentNullException.ThrowIfNull(message.Stations);
        ArgumentNullException.ThrowIfNull(message.Platforms);
        ArgumentNullException.ThrowIfNull(message.PlatformAccessPoints);
        ArgumentNullException.ThrowIfNull(message.Depots);

        var chunks = new List<RailwayInfrastructureSnapshotMessage>();
        var builder = new ChunkBuilder(message.Revision, message.IsFullSnapshot);

        foreach (var item in message.Nodes) builder.AddNode(item, chunks);
        foreach (var item in message.Segments) builder.AddSegment(item, chunks);
        foreach (var item in message.Connections) builder.AddConnection(item, chunks);
        foreach (var item in message.Blocks) builder.AddBlock(item, chunks);
        foreach (var item in message.Stations) builder.AddStation(item, chunks);
        foreach (var item in message.Platforms) builder.AddPlatform(item, chunks);
        foreach (var item in message.PlatformAccessPoints) builder.AddPlatformAccessPoint(item, chunks);
        foreach (var item in message.Depots) builder.AddDepot(item, chunks);

        builder.Flush(chunks, allowEmpty: true);
        return chunks;
    }

    private sealed class ChunkBuilder(ulong revision, bool firstChunkIsFullSnapshot)
    {
        private readonly List<ProtocolTrackNode> _nodes = [];
        private readonly List<ProtocolTrackSegment> _segments = [];
        private readonly List<ProtocolTrackConnection> _connections = [];
        private readonly List<ProtocolBlockSection> _blocks = [];
        private readonly List<ProtocolStation> _stations = [];
        private readonly List<ProtocolPlatform> _platforms = [];
        private readonly List<ProtocolPlatformAccessPoint> _platformAccessPoints = [];
        private readonly List<ProtocolDepot> _depots = [];
        private int _payloadLength = SnapshotHeaderLength;
        private bool _isFirstChunk = true;

        public void AddNode(ProtocolTrackNode item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            EnsureCapacity(NodeLength, chunks);
            _nodes.Add(item);
            _payloadLength = checked(_payloadLength + NodeLength);
        }

        public void AddSegment(ProtocolTrackSegment item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            EnsureCapacity(SegmentLength, chunks);
            _segments.Add(item);
            _payloadLength = checked(_payloadLength + SegmentLength);
        }

        public void AddConnection(ProtocolTrackConnection item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            EnsureCapacity(ConnectionLength, chunks);
            _connections.Add(item);
            _payloadLength = checked(_payloadLength + ConnectionLength);
        }

        public void AddBlock(ProtocolBlockSection item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(item.SegmentIds);
            var length = checked(BlockHeaderLength + checked(item.SegmentIds.Count * sizeof(ulong)));
            EnsureCapacity(length, chunks);
            _blocks.Add(item);
            _payloadLength = checked(_payloadLength + length);
        }

        public void AddStation(ProtocolStation item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            EnsureCapacity(StationLength, chunks);
            _stations.Add(item);
            _payloadLength = checked(_payloadLength + StationLength);
        }

        public void AddPlatform(ProtocolPlatform item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            EnsureCapacity(PlatformLength, chunks);
            _platforms.Add(item);
            _payloadLength = checked(_payloadLength + PlatformLength);
        }

        public void AddPlatformAccessPoint(ProtocolPlatformAccessPoint item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            EnsureCapacity(AccessPointLength, chunks);
            _platformAccessPoints.Add(item);
            _payloadLength = checked(_payloadLength + AccessPointLength);
        }

        public void AddDepot(ProtocolDepot item, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(item.TrackSegmentIds);
            var length = checked(DepotHeaderLength + checked(item.TrackSegmentIds.Count * sizeof(ulong)));
            EnsureCapacity(length, chunks);
            _depots.Add(item);
            _payloadLength = checked(_payloadLength + length);
        }

        public void Flush(List<RailwayInfrastructureSnapshotMessage> chunks, bool allowEmpty = false)
        {
            if (!allowEmpty && !HasItems) return;
            if (allowEmpty && chunks.Count > 0 && !HasItems) return;

            chunks.Add(new RailwayInfrastructureSnapshotMessage(
                revision,
                _isFirstChunk && firstChunkIsFullSnapshot,
                _nodes.ToArray(),
                _segments.ToArray(),
                _connections.ToArray(),
                _blocks.ToArray(),
                _stations.ToArray(),
                _platforms.ToArray(),
                _platformAccessPoints.ToArray(),
                _depots.ToArray()));

            _isFirstChunk = false;
            _nodes.Clear();
            _segments.Clear();
            _connections.Clear();
            _blocks.Clear();
            _stations.Clear();
            _platforms.Clear();
            _platformAccessPoints.Clear();
            _depots.Clear();
            _payloadLength = SnapshotHeaderLength;
        }

        private bool HasItems => _nodes.Count != 0 || _segments.Count != 0 || _connections.Count != 0 || _blocks.Count != 0 || _stations.Count != 0 || _platforms.Count != 0 || _platformAccessPoints.Count != 0 || _depots.Count != 0;

        private void EnsureCapacity(int encodedLength, List<RailwayInfrastructureSnapshotMessage> chunks)
        {
            if ((uint)checked(SnapshotHeaderLength + encodedLength) > ProtocolFrameHeader.MaxPayloadLength)
                throw new ArgumentOutOfRangeException(nameof(encodedLength), "A single railway infrastructure item exceeds the maximum protocol payload size.");

            if ((uint)checked(_payloadLength + encodedLength) <= ProtocolFrameHeader.MaxPayloadLength) return;
            Flush(chunks);
        }
    }
}
