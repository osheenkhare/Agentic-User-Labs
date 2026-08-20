namespace HelloWorldStreamingApp;

internal interface IAgentOrchestrator
{
    string GetResponse(Microsoft.Teams.Apps.Schema.MessageActivity activity);
}