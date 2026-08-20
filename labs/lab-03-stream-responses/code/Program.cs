using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;
using StreamResponse;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();

IAgentOrchestrator agent = new StreamResponseAgentOrchestrator();

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();

teams.OnMessage(async (context, cancellationToken) =>
{
    TeamsStreamingWriter stream = TeamsStreamingWriter.CreateFromContext(context);

    await foreach (IAgentEvent update in agent.GetUpdatesAsync(
        context.Activity,
        cancellationToken))
    {
        if (update.IsInformative)
        {
            await stream.SendInformativeUpdateAsync(update.Text, cancellationToken);
        }
        else
        {
            await stream.AppendResponseAsync(update.Text, cancellationToken);
        }
    }
    await stream.FinalizeResponseAsync(cancellationToken: cancellationToken);
});

await app.RunAsync();
