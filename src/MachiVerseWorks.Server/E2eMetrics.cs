namespace MachiVerseWorks.Server;

internal readonly record struct ProtocolSendMetrics(
    int FrameBytes,
    double EncodeTimeMs,
    double SendTimeMs);

internal sealed record E2eMetricsSnapshot(
    long TotalSnapshotDeliveries,
    long TotalMessages,
    long TotalBytes,
    double TotalEncodeTimeMs,
    double TotalSendTimeMs,
    int LastAgentCount,
    int LastMessageCount,
    long LastBytes,
    double LastEncodeTimeMs,
    double LastSendTimeMs);

internal sealed class E2eMetrics
{
    private readonly object _gate = new();
    private long _totalSnapshotDeliveries;
    private long _totalMessages;
    private long _totalBytes;
    private double _totalEncodeTimeMs;
    private double _totalSendTimeMs;
    private int _lastAgentCount;
    private int _lastMessageCount;
    private long _lastBytes;
    private double _lastEncodeTimeMs;
    private double _lastSendTimeMs;

    public void RecordSnapshotDelivery(
        int agentCount,
        int messageCount,
        long bytes,
        double encodeTimeMs,
        double sendTimeMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(agentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(messageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        if (!double.IsFinite(encodeTimeMs) || encodeTimeMs < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(encodeTimeMs));
        }

        if (!double.IsFinite(sendTimeMs) || sendTimeMs < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(sendTimeMs));
        }

        lock (_gate)
        {
            _totalSnapshotDeliveries = checked(_totalSnapshotDeliveries + 1);
            _totalMessages = checked(_totalMessages + messageCount);
            _totalBytes = checked(_totalBytes + bytes);
            _totalEncodeTimeMs += encodeTimeMs;
            _totalSendTimeMs += sendTimeMs;
            _lastAgentCount = agentCount;
            _lastMessageCount = messageCount;
            _lastBytes = bytes;
            _lastEncodeTimeMs = encodeTimeMs;
            _lastSendTimeMs = sendTimeMs;
        }
    }

    public E2eMetricsSnapshot Capture()
    {
        lock (_gate)
        {
            return new E2eMetricsSnapshot(
                _totalSnapshotDeliveries,
                _totalMessages,
                _totalBytes,
                _totalEncodeTimeMs,
                _totalSendTimeMs,
                _lastAgentCount,
                _lastMessageCount,
                _lastBytes,
                _lastEncodeTimeMs,
                _lastSendTimeMs);
        }
    }
}
