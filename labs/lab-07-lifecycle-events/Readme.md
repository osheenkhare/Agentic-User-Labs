# Lab 07 - Handle Lifecycle Events

⏰ Estimated time: 40 min

## Overview

In this standalone lab, you will handle important lifecycle changes for an API-based agent. The sample welcomes users when the agent is installed, responds when conversation membership changes, and logs when the agent is uninstalled.

You do not need to complete the earlier labs first. You will need an agent blueprint, its Microsoft Entra ID credentials, the .NET 10 SDK, and Microsoft dev tunnel.

## Step 1: Get the lab code sample

Clone this repository, then open the `labs/lab-07-lifecycle-events/code` folder.

The folder contains:

- `Program.cs`, which registers installation and membership handlers.
- `LifecycleEventOrchestrator.cs`, which creates lifecycle responses.
- `appsettings.json`, which contains the agent blueprint credentials and local server URL.

The Teams SDK routes each activity to a dedicated handler:

```csharp
teams.OnInstall(async (context, cancellationToken) =>
{
    await context.SendActivityAsync(
        agent.GetInstalledResponse(),
        cancellationToken);
});
```

The sample also registers `OnUnInstall`, `OnMembersAdded`, and `OnMembersRemoved`. It excludes the agent's own identity from membership counts because installation and removal can also produce membership changes. Uninstall events are logged because the agent should not attempt to send into a conversation after it has been removed.

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

From `labs/lab-07-lifecycle-events/code`, run:

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

## Step 6: Test lifecycle events

1. Add or enable the agent in a test conversation and confirm the welcome message appears.
2. Add a member and confirm the agent reports the membership change.
3. Remove a member and confirm the agent reports the membership change.
4. Remove the agent and confirm an uninstall entry appears in the application log.

## Troubleshooting

- **No lifecycle event arrives**: Confirm the callback URL is current and the application and tunnel are both running.
- **Duplicate welcome messages**: Confirm the agent is not installed more than once in the test scope.
- **Uninstall produces no chat response**: This is expected. The sample records uninstall events in the server log.