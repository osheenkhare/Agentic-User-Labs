using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;
using Microsoft.Teams.Apps.Schema;
using ProgressiveUpdates;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddTeamsBotApplication();
builder.Services.AddSingleton<ProgressJobQueue>();
builder.Services.AddSingleton<ProgressiveMessageService>();
builder.Services.AddHostedService<ProgressWorker>();

int demoDurationSeconds =
    builder.Configuration.GetValue<int?>("ProgressiveUpdates:DemoDurationSeconds") ?? 12;
if (demoDurationSeconds < 4)
{
    throw new InvalidOperationException(
        "ProgressiveUpdates:DemoDurationSeconds must be at least 4.");
}

WebApplication app = builder.Build();
TeamsBotApplication teams = app.UseTeamsBotApplication();
ProgressJobQueue queue = app.Services.GetRequiredService<ProgressJobQueue>();
ILoggerFactory loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
ILogger logger = loggerFactory.CreateLogger("ProgressiveUpdates.Handler");

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    sample = "progressive-updates",
}));

teams.OnMessage(async (context, cancellationToken) =>
{
    string commandText = (
        context.Activity.TextWithoutMentions
        ?? context.Activity.Text
        ?? string.Empty).Trim();

    ProgressCommand? command = commandText.ToLowerInvariant() switch
    {
        "/edit-stream" => ProgressCommand.EditStream,
        "/work-plan" => ProgressCommand.WorkPlan,
        _ => null,
    };

    if (command is null)
    {
        await context.SendAsync(
            """
            Send one of these commands:

            - `/edit-stream` progressively edits one message.
            - `/work-plan` updates one Adaptive Card as background work completes.
            """,
            cancellationToken);
        return;
    }

    string inboundActivityId = context.Activity.Id
        ?? throw new InvalidOperationException("Incoming message has no activity ID.");
    string conversationId = context.Activity.Conversation?.Id
        ?? throw new InvalidOperationException("Incoming message has no conversation ID.");

    if (!queue.TryReserve(inboundActivityId))
    {
        logger.LogInformation(
            "Ignoring duplicate delivery for activity {ActivityId}.",
            inboundActivityId);
        return;
    }

    string operationId = Guid.NewGuid().ToString("N")[..8];

    try
    {
        MessageActivity initialActivity = command == ProgressCommand.EditStream
            ? new MessageActivity($"Queued long-running operation `{operationId}`...")
            : WorkPlanCard.CreateMessage(
                DemoContent.CreateWorkPlanSteps(),
                "Long-running task");

        Microsoft.Teams.Core.SendActivityResponse? sent =
            await context.SendAsync(initialActivity, cancellationToken);
        string progressActivityId = sent?.Id
            ?? throw new InvalidOperationException(
                "Teams did not return an activity ID for the progress message.");

        ProgressJob job = new(
            operationId,
            inboundActivityId,
            command.Value,
            conversationId,
            progressActivityId,
            TimeSpan.FromSeconds(demoDurationSeconds));

        if (!queue.TryEnqueue(job))
        {
            queue.MarkFailed(job);
            await context.Api.Conversations.Activities.UpdateAsync(
                conversationId,
                progressActivityId,
                new MessageActivity("The progress queue is full. Try again later."),
                cancellationToken: cancellationToken);
        }
    }
    catch
    {
        queue.Release(inboundActivityId);
        throw;
    }
});

await app.RunAsync();
