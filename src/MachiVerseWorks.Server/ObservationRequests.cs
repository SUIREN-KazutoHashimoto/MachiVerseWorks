using System.Threading.Channels;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

/// <summary>
/// Connection-local read-only observation intent. Processing these requests may change
/// subscription/inspection state for a client, but must not mutate authoritative Simulation state.
/// </summary>
internal abstract record ObservationRequest(Guid ConnectionId);

internal sealed record SubscribeVolumeObservationRequest(Guid ConnectionId, WorldVolume Volume) : ObservationRequest(ConnectionId);
internal sealed record InspectPersonObservationRequest(Guid ConnectionId, ulong PersonId) : ObservationRequest(ConnectionId);
internal sealed record ClearPersonInspectionObservationRequest(Guid ConnectionId) : ObservationRequest(ConnectionId);

internal sealed class ObservationRequestQueue
{
    private const int Capacity = 1024;
    private readonly Channel<ObservationRequest> _channel = Channel.CreateBounded<ObservationRequest>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
    });

    public ValueTask WriteAsync(ObservationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<ObservationRequest> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class ObservationRequestProcessor(
    ObservationRequestQueue queue,
    ClientConnectionRegistry connections,
    ILogger<ObservationRequestProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in queue.ReadAllAsync(stoppingToken))
            {
                if (!connections.TryGet(request.ConnectionId, out var connection) || connection is null) continue;

                switch (request)
                {
                    case SubscribeVolumeObservationRequest subscribe:
                        connection.SetSubscription(subscribe.Volume);
                        break;
                    case InspectPersonObservationRequest inspect:
                        connection.SetInspectedPerson(inspect.PersonId);
                        break;
                    case ClearPersonInspectionObservationRequest:
                        connection.ClearPersonInspection();
                        break;
                    default:
                        ServerLog.UnsupportedObservationRequest(logger, request.GetType().Name);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
