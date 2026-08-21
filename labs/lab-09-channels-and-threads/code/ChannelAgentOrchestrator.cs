using Microsoft.Teams.Apps.Schema;

namespace ChannelsAndThreads;

internal sealed class ChannelAgentOrchestrator
{
    internal string GetResponse(MessageActivity activity)
    {
        string sender = activity.From?.Name ?? "there";
        string text = activity.Text?.Trim() ?? string.Empty;
        string scope = activity.Conversation?.ConversationType ?? "conversation";

        return text.Length == 0
            ? $"Hi {sender}. I received this {scope} activity and kept the reply in its thread."
            : $"Hi {sender}. I received **{text}** in this {scope} and kept the reply in its thread.";
    }
}