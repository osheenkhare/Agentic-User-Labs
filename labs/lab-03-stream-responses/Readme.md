# Lab 03 - Stream Agent Responses

⏰ Estimated time: 40 min

## Overview

In this standalone lab, you will run an API-based agent that streams its response as a sequence of updates. The agent first sends an informative status message and then appends the final response one word at a time.

You do not need to complete Lab 02 before starting this lab. You will need an agent blueprint, its Microsoft Entra ID credentials, the .NET 10 SDK, and Microsoft dev tunnel.

## Step 1: Get the Lab code sample

Clone this repository to your local development environment, then open the `labs/lab-03-stream-responses/code` folder.

The folder contains a complete .NET application for this lab:

- `Program.cs` configures the Teams application and handles incoming messages.
- `IAgentOrchestrator.cs` defines the streaming event and orchestrator contracts.
- `StreamResponseAgentOrchestrator.cs` creates the informative and response updates.
- `appsettings.json` contains the agent blueprint credentials and local server URL.

### Code explanation

`Program.cs`

```csharp
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
```

This handler creates a `TeamsStreamingWriter` for each incoming message. It consumes the asynchronous event stream from the orchestrator, sends informative events as temporary status updates, and appends all other events to the visible response. After all updates have been received, `FinalizeResponseAsync` completes the response.

`IAgentOrchestrator.cs`

```csharp
internal sealed record AgentEvent(
    string Text,
    bool IsInformative = false) : IAgentEvent;

internal interface IAgentOrchestrator
{
    IAsyncEnumerable<IAgentEvent> GetUpdatesAsync(
        Microsoft.Teams.Apps.Schema.MessageActivity activity,
        CancellationToken cancellationToken);
}
```

The orchestrator returns `IAsyncEnumerable<IAgentEvent>`, allowing each update to be processed as soon as it is available instead of waiting for the full response. `IsInformative` distinguishes a temporary progress update from content that belongs in the final response.

`StreamResponseAgentOrchestrator.cs`

```csharp
yield return new AgentEvent("Echoing your message", IsInformative: true);

foreach (string word in response.Split(' '))
{
    await Task.Delay(TimeSpan.FromSeconds(0.05), cancellationToken);
    yield return new AgentEvent($"{word} ");
}
```

The orchestrator first emits an informative update. It then splits the response into words and yields each word as a separate response update. The delay makes streaming easy to observe during the lab and should be removed from production code.

## Step 2: Configure appsettings.json

Open `code/appsettings.json`. Leave the other values unchanged and replace the placeholders in `AzureAd`:

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

- `ClientId` is the application ID of your agent blueprint.
- `TenantId` is the tenant where the agent blueprint is configured.
- `ClientSecret` is the client secret created for the agent blueprint.

Do not commit a populated `appsettings.json` containing a client secret.

## Step 3: Build and run the agent locally

From `labs/lab-03-stream-responses/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

The application should start listening on:

```text
http://localhost:3978
```

Keep this terminal running. The local server is now ready to receive requests, but Microsoft 365 cannot reach it until you expose it through a development tunnel.

## Step 4: Set up the dev tunnel

Make sure you have [Microsoft dev tunnel](../../dependencies/dev-tunnel/Readme.md) installed and authenticated.

In a second terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

The command returns a public URL similar to:

```text
https://domain.devtunnels.ms
```

Your agent notification endpoint is:

```text
https://domain.devtunnels.ms/api/messages
```

Replace `domain.devtunnels.ms` with the host shown by your dev tunnel and keep this terminal running.

> Note: A development tunnel can add latency. This is not representative of production performance.

## Step 5: Configure the callback URL

Open the Microsoft 365 Developer Portal at:

```text
https://dev.teams.microsoft.com/tools/agent-blueprint
```

Select the agent blueprint whose application ID matches the `ClientId` in `appsettings.json`. Under **Configuration > Notification Configuration**:

- Set **Agent Type** to **API Based**.
- Set **Notification Url** to `https://domain.devtunnels.ms/api/messages`, using your dev tunnel host.
- Save the configuration.

You can also open a blueprint directly at `https://dev.teams.microsoft.com/tools/agent-blueprint/<id>`, replacing `<id>` with its application ID.

## Step 6: Test the agent

### Teams

1. Open [Microsoft Teams](https://teams.microsoft.com) in your browser.
2. Search for the agentic user by name or email address.
3. Open a chat and send a message.
4. Confirm that **Echoing your message** appears as an informative update, followed by a response that builds incrementally.

