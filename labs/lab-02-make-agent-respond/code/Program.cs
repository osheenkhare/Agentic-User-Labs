using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;
using HelloWorldStreamingApp;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();

IAgentOrchestrator agent = new HelloWorldAgentOrchestrator();

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();

teams.OnMessage(async (context, cancellationToken) =>
{
    string response = agent.GetResponse(context.Activity);
    await context.SendActivityAsync(response, cancellationToken: cancellationToken);
});

await app.RunAsync();
