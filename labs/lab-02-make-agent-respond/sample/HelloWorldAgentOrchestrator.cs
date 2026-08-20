namespace HelloWorldStreamingApp;

internal sealed class HelloWorldAgentOrchestrator : IAgentOrchestrator
{
    public string GetResponse(Microsoft.Teams.Apps.Schema.MessageActivity activity)
    {
        string fromId = activity.From?.AadObjectId ?? "unknown";
        string agenticUserId = activity.Recipient?.AgenticUserId ?? "unknown";
        string text = activity.Text ?? string.Empty;

        return $"User `{fromId}` sent a message to agentic user `{agenticUserId}` with the following content: `{text}`";
    }
}
