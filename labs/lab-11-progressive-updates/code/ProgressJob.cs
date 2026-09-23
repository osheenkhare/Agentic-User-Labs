namespace ProgressiveUpdates;

internal enum ProgressCommand
{
    EditStream,
    WorkPlan,
}

internal enum ProgressJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
}

internal sealed record ProgressJob(
    string OperationId,
    string InboundActivityId,
    ProgressCommand Command,
    string ConversationId,
    string ProgressActivityId,
    TimeSpan Duration);
