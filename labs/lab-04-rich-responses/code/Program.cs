using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;
using Microsoft.Teams.Apps.Schema;
using RichResponse;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();

IAgentOrchestrator agent = new RichResponseAgentOrchestrator();

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();

teams.OnMessage(async (context, cancellationToken) =>
{
    TeamsStreamingWriter stream = TeamsStreamingWriter.CreateFromContext(context);
    MessageActivity? finalResponse = null;

    await foreach (IAgentEvent update in agent.GetUpdatesAsync(
        context.Activity,
        cancellationToken))
    {
        switch (update)
        {
            case InformativeAgentEvent informative:
                await stream.SendInformativeUpdateAsync(informative.Text, cancellationToken);
                break;
            case TextAgentEvent text:
                await stream.AppendResponseAsync(text.Text, cancellationToken);
                break;
            case AdaptiveCardAgentEvent card:
                finalResponse = new MessageActivity { Text = string.Empty }
                    .AddAttachment(card.Attachment);
                break;
        }
    }

    await stream.FinalizeResponseAsync(finalResponse, cancellationToken);
});

await app.RunAsync();