using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Schema;

namespace ProgressiveUpdates;

internal sealed class ProgressiveMessageService(
    TeamsBotApplication teams,
    ILogger<ProgressiveMessageService> logger)
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1.5);

    public Task RunAsync(ProgressJob job, CancellationToken cancellationToken) =>
        job.Command switch
        {
            ProgressCommand.EditStream => RunEditStreamAsync(job, cancellationToken),
            ProgressCommand.WorkPlan => RunWorkPlanAsync(job, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(job), job.Command, "Unknown command."),
        };

    public async Task ReportFailureAsync(
        ProgressJob job,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Operation {OperationId} failed: {ErrorMessage}",
            job.OperationId,
            errorMessage);

        await UpdateActivityAsync(
            job,
            new MessageActivity(
                $"The operation could not be completed. Reference: `{job.OperationId}`."),
            cancellationToken);
    }

    private async Task RunEditStreamAsync(
        ProgressJob job,
        CancellationToken cancellationToken)
    {
        StringBuilder response = new();
        int publishedLength = 0;
        Stopwatch updateClock = Stopwatch.StartNew();

        await foreach (string chunk in DemoContent.GenerateChunksAsync(
            job.Duration,
            cancellationToken))
        {
            response.Append(chunk);

            if (updateClock.Elapsed < UpdateInterval)
            {
                continue;
            }

            await UpdateActivityAsync(
                job,
                new MessageActivity(response.ToString()),
                cancellationToken);
            publishedLength = response.Length;
            updateClock.Restart();
        }

        if (response.Length == 0)
        {
            throw new InvalidOperationException("The response source produced no text.");
        }

        if (publishedLength != response.Length)
        {
            await UpdateActivityAsync(
                job,
                new MessageActivity(response.ToString()),
                cancellationToken);
        }
    }

    private async Task RunWorkPlanAsync(
        ProgressJob job,
        CancellationToken cancellationToken)
    {
        List<WorkPlanStep> steps = DemoContent.CreateWorkPlanSteps();
        TimeSpan stepDelay = TimeSpan.FromMilliseconds(
            Math.Max(500, job.Duration.TotalMilliseconds / steps.Count));

        steps[0].Status = WorkPlanStatus.InProgress;
        await UpdateWorkPlanAsync(job, steps, cancellationToken);

        for (int index = 0; index < steps.Count; index++)
        {
            await Task.Delay(stepDelay, cancellationToken);
            steps[index].Status = WorkPlanStatus.Done;

            if (index + 1 < steps.Count)
            {
                steps[index + 1].Status = WorkPlanStatus.InProgress;
            }

            await UpdateWorkPlanAsync(job, steps, cancellationToken);
        }
    }

    private Task UpdateWorkPlanAsync(
        ProgressJob job,
        IReadOnlyList<WorkPlanStep> steps,
        CancellationToken cancellationToken) =>
        UpdateActivityAsync(
            job,
            WorkPlanCard.CreateMessage(steps, "Long-running task"),
            cancellationToken);

    private async Task UpdateActivityAsync(
        ProgressJob job,
        MessageActivity activity,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await teams.Api.Conversations.Activities.UpdateAsync(
                    job.ConversationId,
                    job.ProgressActivityId,
                    activity,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (HttpRequestException exception)
                when (attempt < maxAttempts && IsTransient(exception.StatusCode))
            {
                TimeSpan retryDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                logger.LogWarning(
                    exception,
                    "Progress update {Attempt}/{MaxAttempts} failed for operation {OperationId}. Retrying in {RetryDelay}.",
                    attempt,
                    maxAttempts,
                    job.OperationId,
                    retryDelay);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
