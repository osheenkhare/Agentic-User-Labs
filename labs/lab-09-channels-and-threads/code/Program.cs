using ChannelsAndThreads;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();

ChannelAgentOrchestrator agent = new();

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();

teams.OnMessage(async (context, cancellationToken) =>
{
    string response = agent.GetResponse(context.Activity);

    await context.ReplyAsync(response, cancellationToken);
});

await app.RunAsync();