# Lab 05 - Call Microsoft Graph

⏰ Estimated time: 50 min

## Overview

In this standalone lab, you will extend the streaming agent pattern from Lab 03 with an app-only Microsoft Graph call. When a user sends a message, the application:

1. Reads the sender's Microsoft Entra object ID from the incoming activity.
2. Sends an informative update while it calls Microsoft Graph.
3. Retrieves the sender's display name and email address.
4. Streams the result back to Microsoft Teams word by word.

You do not need to complete the earlier labs first. You will need an agent blueprint, a separate Microsoft Entra app registration for Graph access, the .NET 10 SDK, and Microsoft dev tunnel.

> This sample uses the OAuth 2.0 client credentials flow and the Microsoft Graph `.default` scope. It uses application permissions and does not act on behalf of the user.

## Step 1: Get the lab code sample

Clone this repository to your local development environment, then open the `labs/lab-05-Graph-Api/code` folder.

The folder contains a complete .NET application:

- `Program.cs` configures the Teams application and writes the streaming response.
- `IAgentOrchestrator.cs` defines the streaming event and orchestrator contracts.
- `GraphAgentOrchestrator.cs` coordinates the Graph lookup and response updates.
- `GraphService.cs` authenticates with Microsoft Entra ID and calls Microsoft Graph.
- `appsettings.json` contains the agent blueprint, Graph app, and local server settings.

### How the sample works

`Program.cs` creates a streaming writer and routes informative and response events:

```csharp
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
```

`GraphAgentOrchestrator.cs` obtains the sender ID from the message activity and asks the Graph service for that user:

```csharp
string userId = activity.From?.AadObjectId ?? string.Empty;

UserProfile profile = await _graphService.GetUserProfileAsync(
    userId,
    cancellationToken);
```

`GraphService.cs` uses `ClientSecretCredential` and the Graph `.default` scope to create an app-only client. It requests only the profile properties used by the response:

```csharp
var user = await _graphClient.Users[userId].GetAsync(
    request => request.QueryParameters.Select =
        ["displayName", "mail", "userPrincipalName"],
    cancellationToken);
```

The deliberate delay in `GraphAgentOrchestrator.cs` makes streaming visible during the lab. Remove it from production code.

## Step 2: Create the Graph app registration

This sample keeps the agent blueprint credentials separate from the credentials used to call Microsoft Graph.

1. Open [Microsoft Entra app registrations](https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade/quickStartType~/null/sourceType/Microsoft_AAD_IAM).
2. Select **New registration**, enter a name, leave the remaining values unchanged, and select **Register**.
3. On **Overview**, copy the **Application (client) ID** and **Directory (tenant) ID**.
4. Open **Certificates & secrets**, select **New client secret**, and create a secret.
5. Copy the secret **Value** immediately. Do not use the secret ID.
6. Open **API permissions** and select **Add a permission > Microsoft Graph > Application permissions**.
7. Add `User.Read.All`.
8. Select **Grant admin consent** for the tenant.

`User.Read.All` is a tenant-wide application permission. Use a dedicated development tenant and grant only the permissions required by your agent's business logic.

## Step 3: Configure appsettings.json

Open `code/appsettings.json` and replace the placeholders in both credential sections.

Configure `AzureAd` with the agent blueprint identity used to receive messages:

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

Configure `Graph` with the app registration created in Step 2:

```json
"Graph": {
  "ClientId": "<graph-client-id>",
  "TenantId": "<tenant-id>",
  "ClientSecret": "<graph-client-secret>"
}
```

Do not commit a populated `appsettings.json` containing client secrets. Use a secure secret store for production deployments.

## Step 4: Build and run the agent locally

From `labs/lab-05-Graph-Api/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

The application listens on `http://localhost:3978`. Keep this terminal running.

## Step 5: Set up the dev tunnel

Make sure [Microsoft dev tunnel](../../dependencies/dev-tunnel/Readme.md) is installed and authenticated. In a second terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

Use the returned host to form the notification endpoint:

```text
https://domain.devtunnels.ms/api/messages
```

Keep the tunnel running while you test the agent.

## Step 6: Configure the callback URL

Open the [Microsoft 365 Developer Portal](https://dev.teams.microsoft.com/tools/agent-blueprint) and select the blueprint whose application ID matches `AzureAd:ClientId`.

Under **Configuration > Notification Configuration**:

1. Set **Agent Type** to **API Based**.
2. Set **Notification Url** to your dev tunnel `/api/messages` endpoint.
3. Save the configuration.

## Step 7: Test the Graph call

1. Open [Microsoft Teams](https://teams.microsoft.com).
2. Find the agentic user and open a chat.
3. Send any message.
4. Confirm that **Fetching your profile from Microsoft Graph** appears as an informative update.
5. Confirm that the final response streams your display name and email address.

## Troubleshooting

- **Missing configuration value**: Populate every placeholder in the `AzureAd` and `Graph` sections.
- **401 Unauthorized**: Verify the Graph tenant ID, client ID, and client secret. Confirm that `ClientSecret` contains the secret value, not its ID.
- **403 Forbidden**: Verify that the Graph app has the `User.Read.All` application permission and that an administrator granted tenant-wide consent.
- **Sender object ID missing**: Test from a Microsoft 365 user chat in the same tenant and confirm the incoming activity contains `From.AadObjectId`.
- **User not found**: Confirm that the sender exists in the tenant configured under `Graph:TenantId`.
- **Email fallback**: If the user's `mail` property is empty, the sample displays `userPrincipalName` instead.

For more information, see the [Microsoft Graph .NET SDK documentation](https://learn.microsoft.com/en-us/graph/sdks/sdk-installation), [client credentials flow documentation](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow), and [Get user API documentation](https://learn.microsoft.com/en-us/graph/api/user-get).
