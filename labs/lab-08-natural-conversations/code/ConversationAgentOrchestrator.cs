namespace NaturalConversations;

internal sealed class ConversationAgentOrchestrator
{
    internal string? GetResponse(string? text)
    {
        string message = text?.Trim() ?? string.Empty;

        if (message.Length == 0)
        {
            return null;
        }

        if (message.Contains("help", StringComparison.OrdinalIgnoreCase))
        {
            return "I can respond to questions, status requests, and message reactions.";
        }

        if (message.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return "I am online and ready to participate in this conversation.";
        }

        if (message.EndsWith("?", StringComparison.Ordinal))
        {
            return $"You asked: **{message}**\n\nI received the question and can route it to the next capability.";
        }

        return null;
    }

    internal string GetReactionAddedResponse(IEnumerable<string?> reactionTypes)
    {
        string reactions = string.Join(
            ", ",
            reactionTypes.Where(type => !string.IsNullOrWhiteSpace(type)));

        return reactions.Length == 0
            ? "Thanks for reacting to the message."
            : $"Thanks for the {reactions} reaction.";
    }
}