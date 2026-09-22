# Lab 11 - Send Progressive Updates

⏰ Estimated time: 60 min

## Overview

In this standalone lab, you will build an API-based agent that acknowledges a
request immediately and continues the work in a background service. The agent
communicates progress by editing one Teams message or replacing one work-plan
Adaptive Card as each step completes.

This architecture allows work to continue beyond the incoming Teams request
window. It also brings progressive updates to group chats and channels, where
native Teams streaming is not supported.

Lab 3 introduces native streaming in supported conversations. You do not need
to complete Lab 3 before starting this lab.

You will need:

- An agent blueprint and its Microsoft Entra ID credentials.
- The .NET 10 SDK.
- Microsoft dev tunnel.
- A Microsoft Teams chat, group chat, or channel where the agent is available.

## What you will learn

By the end of the lab, you will be able to:

- Return promptly from an incoming Teams activity handler.
- Queue long-running work in a hosted background service.
- Update an existing Teams activity without retaining the turn context.
- Display an updating work-plan Adaptive Card.
- Keep channel updates in the original thread.
- Ignore duplicate delivery of the same incoming activity.
- Validate a job that runs for more than two minutes.

## Architecture

The sample separates request handling from long-running work:

```text
Teams message
     |
     v
OnMessage handler
  1. Reserve the inbound activity ID
  2. Send a placeholder or initial card
  3. Store the conversation and progress activity IDs
  4. Queue a ProgressJob
  5. Return
     |
     v
ProgressJobQueue
     |
     v
ProgressWorker
  1. Run the simulated long operation
  2. Update the stored activity ID
  3. Publish the final state
```

The worker uses `TeamsBotApplication.Api` rather than the incoming `Context`.
The turn context and its cancellation token are not retained after the handler
returns.

## Step 1: Get the lab code sample

Clone this repository, then open:

```text
labs/lab-11-progressive-updates/code
```

The project contains:

| File | Purpose |
| --- | --- |
| `Program.cs` | Receives commands, sends the initial activity, and queues the work. |
| `ProgressJob.cs` | Defines the queued operation and its status values. |
| `ProgressJobQueue.cs` | Provides a bounded in-memory channel and duplicate-delivery tracking. |
| `ProgressWorker.cs` | Runs queued jobs after the incoming handler has returned. |
| `ProgressiveMessageService.cs` | Edits the stored Teams activity and retries transient update failures. |
| `WorkPlanCard.cs` | Builds the collapsible work-plan Adaptive Card. |
| `DemoContent.cs` | Produces generated chunks and work-plan steps without requiring an AI provider. |
| `appsettings.example.json` | Contains placeholder identity settings and the demo duration. |

## Step 2: Configure the application

Copy the example configuration:

```powershell
Copy-Item appsettings.example.json appsettings.json
```

Open `appsettings.json` and replace the three Azure AD placeholders:

```json
"AzureAd": {
  "ClientId": "<agent-blueprint-id>",
  "TenantId": "<tenant-id>",
  "ClientCredentials": [
    {
      "SourceType": "ClientSecret",
      "ClientSecret": "<agent-blueprint-client-secret>"
    }
  ]
}
```

- `ClientId` is the application ID of the agent blueprint.
- `TenantId` is the tenant containing the blueprint.
- `ClientSecret` is the secret value created for the blueprint, not its secret
  ID.

Do not commit the populated `appsettings.json`. The repository ignores this
file.

The default demonstration duration is 12 seconds:

```json
"ProgressiveUpdates": {
  "DemoDurationSeconds": 12
}
```

Keep the short duration while developing. You will change it to 130 seconds
when validating work beyond the two-minute request window.

## Step 3: Review the incoming message handler

`Program.cs` registers the background services before building the application:

```csharp
builder.Services.AddTeamsBotApplication();
builder.Services.AddSingleton<ProgressJobQueue>();
builder.Services.AddSingleton<ProgressiveMessageService>();
builder.Services.AddHostedService<ProgressWorker>();
```

The message handler accepts `/edit-stream` and `/work-plan`. Before starting
work, it reserves the incoming activity ID:

```csharp
if (!queue.TryReserve(inboundActivityId))
{
    logger.LogInformation(
        "Ignoring duplicate delivery for activity {ActivityId}.",
        inboundActivityId);
    return;
}
```

This prevents a repeated delivery of the same Teams activity from creating a
second job in the current process.

The handler then sends a placeholder or initial card, captures its activity ID,
and queues the job:

```csharp
ProgressJob job = new(
    operationId,
    inboundActivityId,
    command.Value,
    conversationId,
    progressActivityId,
    TimeSpan.FromSeconds(demoDurationSeconds));

queue.TryEnqueue(job);
```

The long-running operation is not awaited by the message handler.

## Step 4: Review the background queue

`ProgressJobQueue` uses a bounded `Channel<ProgressJob>`:

```csharp
Channel.CreateBounded<ProgressJob>(
    new BoundedChannelOptions(25)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false,
    });
```

The queue accepts work from multiple request handlers. One `ProgressWorker`
processes the jobs sequentially, which keeps the learning sample predictable.
If the queue is full, the initial progress activity is replaced with an
actionable error.

The queue and duplicate-delivery state are stored in memory. They are lost if
the process restarts.

## Step 5: Review the background worker

`ProgressWorker` is a hosted `BackgroundService`. It reads jobs until the
application shuts down:

```csharp
await foreach (ProgressJob job in queue.ReadAllAsync(stoppingToken))
{
    queue.MarkRunning(job);
    await messageService.RunAsync(job, stoppingToken);
    queue.MarkCompleted(job);
}
```

The real implementation also catches job failures, logs the operation ID, and
attempts to replace the progress activity with a visible failure message. One
failed job does not terminate the worker.

The worker uses the application shutdown token. It does not use the
cancellation token from the incoming Teams request.

## Step 6: Review edit-based updates

For `/edit-stream`, the handler sends one placeholder and stores its activity
ID. `ProgressiveMessageService` later updates that activity through the
application-level Teams API:

```csharp
await teams.Api.Conversations.Activities.UpdateAsync(
    job.ConversationId,
    job.ProgressActivityId,
    activity,
    cancellationToken: cancellationToken);
```

The service:

- Accumulates the complete response.
- Limits intermediate updates to a bounded cadence.
- Awaits each update before sending the next one.
- Always publishes the complete final response.
- Retries HTTP 429 and transient server failures up to three times with
  bounded backoff.

This is message editing, not native Teams streaming. Teams may show an edited
indicator, and normal service throttling still applies.

## Step 7: Review the work-plan card

For `/work-plan`, the handler immediately sends a card whose steps are pending.
The worker changes one step to `InProgress`, waits for the simulated work, and
updates the same activity after each transition.

The card supports:

- Pending steps.
- An active progress ring.
- Completed steps.
- Failure and cancellation markers.
- A collapsible list that closes after all steps complete.

Every update renders the card from the complete current step collection. The
card is a user-visible status summary; it must not expose private model
reasoning or chain-of-thought.

## Step 8: Build and run the agent

From `labs/lab-11-progressive-updates/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

The application listens on:

```text
http://localhost:3978
```

Verify the health endpoint in another terminal:

```powershell
Invoke-RestMethod http://localhost:3978/health
```

Expected result:

```text
status sample
------ ------
ok     progressive-updates
```

Keep the application terminal running.

## Step 9: Set up the development tunnel

Make sure [Microsoft dev tunnel](../../dependencies/dev-tunnel/Readme.md) is
installed and authenticated.

In another terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

The command returns a public URL similar to:

```text
https://domain.devtunnels.ms
```

The notification endpoint is:

```text
https://domain.devtunnels.ms/api/messages
```

Keep the tunnel running.

## Step 10: Configure the callback URL

Open the [Microsoft 365 Developer Portal](https://dev.teams.microsoft.com/tools/agent-blueprint)
and select the agent blueprint whose application ID matches `ClientId`.

Under **Configuration > Notification Configuration**:

1. Set **Agent Type** to **API Based**.
2. Set **Notification Url** to the dev tunnel `/api/messages` URL.
3. Save the configuration.

## Step 11: Test the short demonstrations

Open a personal chat with the agent and send:

```text
/edit-stream
```

Confirm that:

1. A queued message appears immediately.
2. The same message is edited as content accumulates.
3. No new message is created for each chunk.
4. The final message contains the complete response.

Then send:

```text
/work-plan
```

Confirm that:

1. A work-plan card appears immediately.
2. One step at a time becomes active and then completes.
3. The same card is replaced throughout the operation.
4. The completed card shows `4/4 done` and starts collapsed.

## Step 12: Validate work beyond two minutes

Stop the application and change the duration in `appsettings.json`:

```json
"ProgressiveUpdates": {
  "DemoDurationSeconds": 130
}
```

Restart the application:

```powershell
dotnet run
```

Send `/edit-stream` and verify:

1. The placeholder appears immediately.
2. The callback request is not held open while the work runs.
3. The application remains responsive at `/health`.
4. Updates continue for more than two minutes.
5. The final accumulated response replaces the placeholder.

Repeat the test with `/work-plan`.

The operation must continue because `ProgressWorker` owns the work. A sample
that awaits the complete operation inside `OnMessage` does not demonstrate this
timeout-safe pattern.

## Step 13: Test group chats and channel threads

### Group chat

1. Add the agent to a group chat.
2. Mention it and send `/edit-stream`.
3. Confirm that the same progress message is edited in the group chat.

### Channel thread

1. Add the agent to a team.
2. Start a channel post and mention the agent with `/work-plan`.
3. Confirm that the initial card appears in the expected thread.
4. Confirm that every update replaces that same card.

The job stores the exact conversation ID from the incoming activity, so updates
remain associated with the activity created in that conversation or thread.

## How duplicate delivery is handled

The queue reserves each incoming activity ID before sending the initial
progress activity. A duplicate delivery with the same activity ID is logged and
ignored.

This protection is local to one running process. A production application
should persist operation IDs and terminal status in durable storage so
duplicate detection works across restarts and multiple application instances.

## Troubleshooting

- **The project will not start**: Confirm that `appsettings.json` exists and
  contains the blueprint application ID, tenant ID, and secret value.
- **The agent does not receive commands**: Confirm that the tunnel is running
  and the saved notification URL ends with `/api/messages`.
- **The placeholder appears but never changes**: Check the application log for
  the operation ID and any HTTP update failures.
- **An update receives HTTP 429**: The sample retries transient failures three
  times. Increase the update interval rather than sending updates more often.
- **The response creates many messages**: Confirm the worker calls
  `Activities.UpdateAsync` with the stored progress activity ID.
- **The card appears in the wrong channel discussion**: Do not replace the
  stored conversation ID with one from another request.
- **The operation stops when the application restarts**: The lab uses an
  in-memory queue. Use durable infrastructure for restart recovery.
- **A repeated message creates duplicate work**: Confirm that the inbound
  activity ID is reserved before the initial progress message is sent.
- **Later jobs remain queued**: The sample intentionally uses one worker.
  Finish the current job or reduce `DemoDurationSeconds`.

## Production considerations

The lab prioritizes a clear local learning experience. Before using this design
in production:

- Replace the in-memory channel with Azure Service Bus, Azure Queue Storage, or
  another durable queue.
- Store operation state and idempotency keys in durable storage.
- Support multiple workers with distributed locking or queue-level delivery
  guarantees.
- Respect service-provided throttling guidance and `Retry-After` values.
- Add authentication-safe telemetry using operation IDs rather than message
  content or credentials.
- Define cancellation, expiration, dead-letter, and restart-recovery behavior.
- Keep user-visible progress separate from private model reasoning.

## Extend the lab

- Replace `DemoContent` with output from a model or business workflow.
- Add `/cancel <operation-id>` support.
- Persist job state across application restarts.
- Process several jobs concurrently without mixing conversation state.
- Add telemetry for queue latency, execution duration, update count, and
  failures.
- Add a final rich response after the work-plan card completes.
