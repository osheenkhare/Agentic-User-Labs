# Lab 04 - Create Rich Responses

⏰ Estimated time: 45 min

## Overview

In this standalone lab, you will run an API-based agent that returns two kinds of rich responses:

- A hardcoded Adaptive Card when the user sends `card`.
- Hardcoded Markdown streamed in chunks when the user sends `markdown`.

For any other message, the agent replies with `Say card or markdown.` Chain-of-thought content is not part of this lab.

You do not need to complete the earlier labs first. You will need an agent blueprint, its Microsoft Entra ID credentials, the .NET 10 SDK, and Microsoft dev tunnel.

## Step 1: Get the lab code sample

Clone this repository to your local development environment, then open the `labs/lab-04-rich-responses/code` folder.

The folder contains a complete .NET application:

- `Program.cs` configures the Teams application and writes streaming or final rich responses.
- `IAgentOrchestrator.cs` defines events for informative updates, text, and Adaptive Cards.
- `RichResponseAgentOrchestrator.cs` selects a response from the incoming command.
- `appsettings.json` contains the agent blueprint credentials and local server URL.

### Adaptive Card response

The orchestrator creates an attachment with the Teams SDK's Adaptive Card helper:

```csharp
return TeamsAttachment.CreateBuilder()
    .WithAdaptiveCard(card)
    .Build();
```

`Program.cs` adds that attachment to the final streaming activity. Setting `Text` to an empty string creates an attachment-only response:

```csharp
finalResponse = new MessageActivity { Text = string.Empty }
    .AddAttachment(card.Attachment);
```

### Streamed Markdown response

The Markdown sample is split into chunks and emitted as text events. The message handler appends each event to the same streaming response:

```csharp
case TextAgentEvent text:
    await stream.AppendResponseAsync(text.Text, cancellationToken);
    break;
```

After all events have been handled, `FinalizeResponseAsync` completes either the accumulated Markdown response or the final Adaptive Card activity.

## Step 2: Configure appsettings.json

Open `code/appsettings.json` and replace the placeholders in `AzureAd`:

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

Do not commit a populated `appsettings.json` containing a client secret.

## Step 3: Build and run the agent locally

From `labs/lab-04-rich-responses/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

The application listens on `http://localhost:3978`.

## Step 4: Set up the dev tunnel

Make sure [Microsoft dev tunnel](../../dependencies/dev-tunnel/Readme.md) is installed and authenticated. In a second terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

Use the returned host to form the notification endpoint:

```text
https://domain.devtunnels.ms/api/messages
```

Keep both the application and tunnel terminals running.

## Step 5: Configure the callback URL

Open the Microsoft 365 Developer Portal at `https://dev.teams.microsoft.com/tools/agent-blueprint` and select the blueprint whose application ID matches `ClientId`.

Under **Configuration > Notification Configuration**:

1. Set **Agent Type** to **API Based**.
2. Set **Notification Url** to your dev tunnel `/api/messages` endpoint.
3. Save the configuration.

## Step 6: Test the rich responses

1. Open [Microsoft Teams](https://teams.microsoft.com).
2. Find the agentic user and open a chat.
3. Send any message and confirm the agent asks you to say `card` or `markdown`.
4. Send `markdown` and confirm the formatted response appears incrementally.
5. Send `card` and confirm the structured Adaptive Card appears.