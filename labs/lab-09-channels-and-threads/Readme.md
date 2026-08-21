# Lab 09 - Participate in Channels and Threads

⏰ Estimated time: 45 min

## Overview

In this standalone lab, you will run an API-based agent in Microsoft Teams channels. The agent handles messages in both root posts and existing threads and sends its response back to the same thread.

You will need an agent blueprint, its Microsoft Entra ID credentials, the .NET 10 SDK, Microsoft dev tunnel, and a Microsoft Teams team where you can test channel conversations.

## Step 1: Get the lab code sample

Clone this repository, then open `labs/lab-09-channels-and-threads/code`.

The folder contains:

- `Program.cs`, which receives messages and creates threaded replies.
- `ChannelAgentOrchestrator.cs`, which creates a response from the channel activity.
- `appsettings.json`, which contains the agent blueprint credentials and local server URL.

The important difference from a normal send is `ReplyAsync`:

```csharp
teams.OnMessage(async (context, cancellationToken) =>
{
    string response = agent.GetResponse(context.Activity);
    await context.ReplyAsync(response, cancellationToken);
});
```

For a channel message, `ReplyAsync` preserves the inbound conversation and message reference so the response appears in the current thread. A new send can create a separate root activity, which is not appropriate when answering an existing discussion.

`ChannelAgentOrchestrator.cs` also reads `ConversationType` so the response shows which conversation scope Teams supplied.

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

From `labs/lab-09-channels-and-threads/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

The application listens on `http://localhost:3978`.

## Step 4: Set up the dev tunnel

In a second terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

Your notification endpoint is `https://domain.devtunnels.ms/api/messages`.

## Step 5: Configure the callback URL

Open the [Microsoft 365 Developer Portal](https://dev.teams.microsoft.com/tools/agent-blueprint), select the matching blueprint, and open **Configuration > Notification Configuration**.

1. Set **Agent Type** to **API Based**.
2. Set **Notification Url** to the dev tunnel `/api/messages` endpoint.
3. Save the configuration.

## Step 6: Test a channel root post

1. Open a test team and channel in Microsoft Teams.
2. Start a new post and mention the agentic user.
3. Include a short message with the mention and send it.
4. Confirm the agent's response appears under that post instead of as a separate root post.

## Step 7: Test an existing thread

1. Open an existing channel post.
2. Reply in its thread and mention the agentic user.
3. Confirm the agent responds within the same thread.
4. Open a one-to-one chat with the agent and confirm the same handler also works there.

## Extend the lab

Use the team, channel, and conversation identifiers from the incoming activity to load thread-specific context. Store that context by conversation and thread ID so information from one channel discussion cannot leak into another.

## Troubleshooting

- **The agent does not receive a channel message**: Mention the agent explicitly and confirm it is available to the team and channel.
- **The response starts a new post**: Confirm the handler uses `context.ReplyAsync`, not a proactive send operation.
- **The reply goes to the wrong discussion**: Do not reuse conversation or activity IDs from a previous request; reply through the current handler context.
- **No response arrives**: Confirm the callback URL is current and both the application and dev tunnel are running.