using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ProgressiveUpdates;

internal sealed class ProgressJobQueue
{
    private const int Capacity = 25;

    private readonly Channel<ProgressJob> _jobs =
        Channel.CreateBounded<ProgressJob>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

    private readonly ConcurrentDictionary<string, ProgressJobStatus> _statuses = new();

    public bool TryReserve(string inboundActivityId) =>
        _statuses.TryAdd(inboundActivityId, ProgressJobStatus.Queued);

    public bool TryEnqueue(ProgressJob job) => _jobs.Writer.TryWrite(job);

    public IAsyncEnumerable<ProgressJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _jobs.Reader.ReadAllAsync(cancellationToken);

    public void MarkRunning(ProgressJob job) =>
        _statuses[job.InboundActivityId] = ProgressJobStatus.Running;

    public void MarkCompleted(ProgressJob job) =>
        _statuses[job.InboundActivityId] = ProgressJobStatus.Completed;

    public void MarkFailed(ProgressJob job) =>
        _statuses[job.InboundActivityId] = ProgressJobStatus.Failed;

    public void Release(string inboundActivityId) =>
        _statuses.TryRemove(inboundActivityId, out _);
}
