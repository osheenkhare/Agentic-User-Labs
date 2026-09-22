namespace ProgressiveUpdates;

internal sealed class ProgressWorker(
    ProgressJobQueue queue,
    ProgressiveMessageService messageService,
    ILogger<ProgressWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (ProgressJob job in queue.ReadAllAsync(stoppingToken))
            {
                queue.MarkRunning(job);
                logger.LogInformation(
                    "Starting operation {OperationId} for activity {ActivityId}.",
                    job.OperationId,
                    job.InboundActivityId);

                try
                {
                    await messageService.RunAsync(job, stoppingToken);
                    queue.MarkCompleted(job);
                    logger.LogInformation("Completed operation {OperationId}.", job.OperationId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation(
                        "Operation {OperationId} stopped because the application is shutting down.",
                        job.OperationId);
                    break;
                }
                catch (Exception exception)
                {
                    queue.MarkFailed(job);
                    logger.LogError(
                        exception,
                        "Operation {OperationId} failed.",
                        job.OperationId);

                    try
                    {
                        await messageService.ReportFailureAsync(
                            job,
                            exception.Message,
                            stoppingToken);
                    }
                    catch (Exception updateException)
                    {
                        logger.LogError(
                            updateException,
                            "Could not publish the failure state for operation {OperationId}.",
                            job.OperationId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Progress worker stopped.");
        }
    }
}
