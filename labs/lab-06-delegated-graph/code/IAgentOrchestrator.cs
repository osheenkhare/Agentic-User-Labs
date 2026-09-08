namespace DelegatedGraph;

internal interface IAgentEvent
{
    string Text { get; }
    bool IsInformative { get; }
    string? SignInUrl { get; }
}

internal sealed record AgentEvent(
    string Text,
    bool IsInformative = false,
    string? SignInUrl = null) : IAgentEvent;

internal interface IAgentOrchestrator
{
    IAsyncEnumerable<IAgentEvent> GetUpdatesAsync(
        Microsoft.Teams.Apps.Schema.MessageActivity activity,
        CancellationToken cancellationToken);
}