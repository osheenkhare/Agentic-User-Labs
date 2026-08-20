using Microsoft.Teams.Apps.Schema;

namespace RichResponse;

internal interface IAgentEvent;

internal sealed record InformativeAgentEvent(string Text) : IAgentEvent;

internal sealed record TextAgentEvent(string Text) : IAgentEvent;

internal sealed record AdaptiveCardAgentEvent(TeamsAttachment Attachment) : IAgentEvent;

internal interface IAgentOrchestrator
{
    IAsyncEnumerable<IAgentEvent> GetUpdatesAsync(
        MessageActivity activity,
        CancellationToken cancellationToken);
}