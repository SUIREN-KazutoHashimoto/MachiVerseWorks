using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal static class SnapshotDeliveryFailurePolicy
{
    public static bool IsExpectedClientFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is WebSocketException or OperationCanceledException or ObjectDisposedException;
    }
}
