using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;
using NaturalConversations;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();

ConversationAgentOrchestrator agent = new();

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();
ILogger logger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("NaturalConversations");

teams.OnMessage(async (context, cancellationToken) =>
{
    string? response = agent.GetResponse(context.Activity.Text);
    if (response is not null)
    {
        await context.ReplyAsync(response, cancellationToken);
    }
});

teams.OnMessageReactionAdded(async (context, cancellationToken) =>
{
    string response = agent.GetReactionAddedResponse(
        context.Activity.ReactionsAdded?.Select(reaction => reaction.Type)
            ?? []);

    await context.ReplyAsync(response, cancellationToken);
});

teams.OnMessageReactionRemoved((context, cancellationToken) =>
{
    string reactions = string.Join(
        ", ",
        context.Activity.ReactionsRemoved?
            .Select(reaction => reaction.Type)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            ?? []);

    logger.LogInformation(
        "Message reactions removed: {ReactionTypes}",
        reactions);
    return Task.CompletedTask;
});

await app.RunAsync();