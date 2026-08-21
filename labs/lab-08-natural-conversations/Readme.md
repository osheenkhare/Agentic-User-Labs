# Lab 08 - Participate Naturally in Conversations

⏰ Estimated time: 40 min

## Overview

In this standalone lab, you will make an API-based agent respond selectively instead of replying to every message. The sample recognizes help requests, status requests, and questions. It also handles reactions added to or removed from messages.

You will need an agent blueprint, its Microsoft Entra ID credentials, the .NET 10 SDK, and Microsoft dev tunnel.

## Step 1: Get the lab code sample

Clone this repository, then open `labs/lab-08-natural-conversations/code`.

The folder contains:

- `Program.cs`, which registers message and reaction handlers.
- `ConversationAgentOrchestrator.cs`, which decides when and how to respond.
- `appsettings.json`, which contains the agent blueprint credentials and local server URL.

The orchestrator returns `null` for messages that are not relevant. This prevents the agent from interrupting every conversation:

```csharp
string? response = agent.GetResponse(context.Activity.Text);
if (response is not null)
{
    await context.ReplyAsync(response, cancellationToken);
}
```

Reaction activities are routed separately with `OnMessageReactionAdded` and `OnMessageReactionRemoved`. The sample replies when a reaction is added and logs removal events.

## Step 2: Configure appsettings.json

Open `code/appsettings.json` and populate the `AzureAd` section with the agent blueprint client ID, tenant ID, and client secret. Do not commit a populated file containing a secret.

## Step 3: Build and run the agent locally

From `labs/lab-08-natural-conversations/code`, run:

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

Open the [Microsoft 365 Developer Portal](https://dev.teams.microsoft.com/tools/agent-blueprint), select the matching blueprint, and set **Configuration > Notification Configuration** as follows:

1. Set **Agent Type** to **API Based**.
2. Set **Notification Url** to the dev tunnel `/api/messages` endpoint.
3. Save the configuration.

## Step 6: Test natural participation

1. Send `help` and confirm the capability response appears.
2. Send `status` and confirm the availability response appears.
3. Send a question ending in `?` and confirm the agent acknowledges it.
4. Send an unrelated statement and confirm the agent stays silent.
5. Add a reaction to a message and confirm the agent acknowledges it.
6. Remove the reaction and confirm the event appears in the application log.

## Extend the lab

Replace the rule-based orchestrator with your own intent classifier or language model. Keep the handler's nullable response contract so the agent can continue deciding when silence is the appropriate behavior.

## Troubleshooting

- **The agent replies to unrelated messages**: Check the rules in `GetResponse` and return `null` when no rule matches.
- **Reaction events do not arrive**: Confirm the reaction targets a message visible to the agent and the callback URL is current.
- **No response arrives**: Confirm the local application and dev tunnel are both running.