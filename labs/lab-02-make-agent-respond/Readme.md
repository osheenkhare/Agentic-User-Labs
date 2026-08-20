# Lab 02 - Make the Agent Respond

⏰ Estimated time: 40 min

## Overview

In this lab, you will scaffold a basic agent code and configure it to respond to messages sent by users in WXPTO [WXPTO - Word, Excel, PowerPoint, Teams, Outlook]. You will also set up a development tunnel and callback URL to enable communication between the agent and the Microsoft 365 environment.

## Step 1: Get the Lab code sample

Clone this repository to your local development environment, then open the `labs/lab-02-make-agent-respond/code` folder.

The folder contains a complete .NET application for this lab:

- `Program.cs` configures the Teams application and handles incoming messages.
- `IAgentOrchestrator.cs` defines the agent orchestrator contract.
- `HelloWorldAgentOrchestrator.cs` creates the response.
- `appsettings.json` contains the agent blueprint credentials and local server URL.

### Code explanation

`Program.cs`

```csharp
teams.OnMessage(async (context, cancellationToken) =>
{
    string response = agent.GetResponse(context.Activity);
    await context.SendActivityAsync(response, cancellationToken: cancellationToken);
});
```

This code snippet sets up an event handler for incoming messages. When a message is received, it calls the `GetResponse` method of the agent to generate a response based on the activity context. The response is then sent back to the user.

`HelloWorldAgentOrchestrator.cs`

```csharp
public string GetResponse(Microsoft.Teams.Apps.Schema.MessageActivity activity)
{
  string fromId = activity.From?.AadObjectId ?? "unknown";
  string agenticUserId = activity.Recipient?.AgenticUserId ?? "unknown";
  string text = activity.Text ?? string.Empty;
  return $"User `{fromId}` sent a message to agentic user `{agenticUserId}` with the following content: `{text}`";
}
```

This method constructs a response string that includes the sender's ID, the agentic user's ID, and the content of the message. It uses null-coalescing operators to handle cases where certain properties may be null.
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

From `labs/lab-02-make-agent-respond/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

This should start the server locally and you should see the following message in the console:

```text
Build succeeded in 1.3s
info: Microsoft.Teams.Apps.TeamsBotApplication[1]
      Started BotApplication listener for AppID:<id> with Teams.Core version 1.0.8
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:3978
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

This means that the server is running and ready to accept requests locally on `http://localhost:3978`. For it to connect to Microsoft 365, you will need to set up a development tunnel and configure a callback URL pointing to your local server.

## Step 4: Set up the dev tunnel

Make sure you have the [Microsoft dev tunnel](../../dependencies/dev-tunnel/Readme.md) installed and configured.

In a second terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

This should give you a public URL that you can use as the callback URL for your agent.

```text
Connect via browser: https://domain.devtunnels.ms
```

The endpoint to be updated in the developer portal is `https://domain.devtunnels.ms/api/messages` (replace the host with your own dev tunnel host).

> Note: Since dev tunnel can be slow, the agent may take a few seconds to respond to messages. This latency is caused by the dev tunnel and is not indicative of the agent's performance in production.

## Step 5: Configure the callback URL

In the Microsoft 365 Developer Portal, configure the endpoint.

Open the following URL in your browser: `https://dev.teams.microsoft.com/tools/agent-blueprint/<id>`
`<id>` is the agent blueprint app ID created while setting up the blueprint. (This is the same `ClientId` you configured in `appsettings.json`)

Alternatively, you can go to `https://dev.teams.microsoft.com/tools/agent-blueprint`, which should list all your blueprints. Select the required one.

In **Blueprint > Configuration > Notification Configuration**:

- Set **Agent Type** to **API Based**.
- Set **Notification Url** to the dev-tunnel endpoint `https://domain.devtunnels.ms/api/messages`.
- Save the configuration.

## Step 6: Test the agent

### Teams

Open [Microsoft Teams](https://teams.microsoft.com) in your browser.

Search for the agentic user by name or email address in the search bar.

![Teams Search](../../diagrams/labs/lab-02-Teams-1.png)

Send a message to the agentic user and confirm that the agent returns a response.

![Teams Chat](../../diagrams/labs/lab-02-Teams-2.png)
### Word, Excel, or PowerPoint

Open Word, Excel, or PowerPoint and open any document. While adding comments, you can mention the agentic user by name or email address.

![WXP](../../diagrams/labs/lab-02-Word-1.png)

Once mentioned, the agentic user should return a response in the comment thread.

![WXP Response](../../diagrams/labs/lab-02-Word-2.png)



