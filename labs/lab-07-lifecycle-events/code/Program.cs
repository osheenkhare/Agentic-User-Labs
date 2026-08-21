using LifecycleEvents;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();

LifecycleEventOrchestrator agent = new();

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();
ILogger logger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("LifecycleEvents");

teams.OnInstall(async (context, cancellationToken) =>
{
    await context.SendActivityAsync(
        agent.GetInstalledResponse(),
        cancellationToken);
});

teams.OnUnInstall((context, cancellationToken) =>
{
    logger.LogInformation(
        "Agent uninstalled from conversation {ConversationId}",
        context.Activity.Conversation?.Id);
    return Task.CompletedTask;
});

teams.OnMembersAdded(async (context, cancellationToken) =>
{
    int memberCount = context.Activity.MembersAdded?.Count(member =>
        member.Id != context.Activity.Recipient?.Id) ?? 0;
    if (memberCount > 0)
    {
        await context.SendActivityAsync(
            agent.GetMembersAddedResponse(memberCount),
            cancellationToken);
    }
});

teams.OnMembersRemoved(async (context, cancellationToken) =>
{
    int memberCount = context.Activity.MembersRemoved?.Count(member =>
        member.Id != context.Activity.Recipient?.Id) ?? 0;
    if (memberCount > 0)
    {
        await context.SendActivityAsync(
            agent.GetMembersRemovedResponse(memberCount),
            cancellationToken);
    }
});

await app.RunAsync();