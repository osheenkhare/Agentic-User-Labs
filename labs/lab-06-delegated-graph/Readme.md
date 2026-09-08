# Lab 06 - Call Microsoft Graph as the Signed-In User

Estimated time: 60 min

## Overview

In this standalone lab, you will extend the streaming agent pattern from Lab 05 with delegated Microsoft Graph access. When a user sends a message, the application:

1. Reads the sender's Microsoft Entra object ID and tenant from the incoming activity.
2. Returns a sign-in card if the sender has not authenticated.
3. Opens browser sign-in and verifies that the signed-in account matches the Teams sender.
4. On the next message, acquires a delegated Graph token from the token cache and calls `/me`.
5. Streams the user's display name and email address back to Teams word by word.

You do not need to complete the earlier labs first. You will need an agent blueprint and agentic user, a separate Microsoft Entra app registration for Graph access, the .NET 10 SDK, and Microsoft dev tunnel. See [Lab 01](../lab-01-create-digital-worker/Readme.md) if you need to create the agent identity.

> This sample uses browser-based OAuth 2.0 authorization code flow with PKCE and delegated `User.Read`. It does not use Teams SSO, application permissions, or an OBO token exchange. The client secret authenticates the backend during code redemption; Graph access still requires user sign-in and consent.

## Step 1: Get the lab code sample

Clone this repository to your local development environment, then open the `labs/lab-06-delegated-graph/code` folder.

The folder contains a complete .NET application:

- [Program.cs](code/Program.cs) configures the Teams application and renders streaming events and sign-in cards.
- [IAgentOrchestrator.cs](code/IAgentOrchestrator.cs) defines the streaming event and orchestrator contracts, following Lab 05 with an additional optional `SignInUrl`.
- [GraphAgentOrchestrator.cs](code/GraphAgentOrchestrator.cs) decides whether to request sign-in or fetch the profile.
- [GraphService.cs](code/GraphService.cs) acquires a delegated token through Microsoft.Identity.Web and calls Graph `/me`.
- [BrowserSignIn.cs](code/BrowserSignIn.cs) configures browser authentication and handles the callback.
- [SignInStore.cs](code/SignInStore.cs) stores short-lived sign-in links and authenticated user identities in memory.
- [appsettings.json](code/appsettings.json) contains the agent blueprint, Graph app, public URL, and local server settings.

### How the sample works

The orchestrator follows the same `GetUpdatesAsync` pattern as Lab 05. If sign-in is required, it emits an event containing a browser link:

```csharp
string ticket = signIns.CreateLink(tenantId, userId);
string signInUrl = new Uri(publicUrl, $"/auth/signin?ticket={ticket}").AbsoluteUri;
yield return new AgentEvent("Sign in to Microsoft Graph", SignInUrl: signInUrl);
```

The message handler renders that event as an Adaptive Card with a **Sign in** button. It starts the response stream before finalizing with the card.

After browser sign-in, the callback checks the authenticated tenant and object ID against the Teams sender. The user then sends another Teams message to trigger the Graph call:

```csharp
string accessToken = await tokens.GetAccessTokenForUserAsync(
    Scopes, authenticationScheme: BrowserSignIn.Scheme, user: user);
```

The service sends the token in the authorization header of this request:

```http
GET https://graph.microsoft.com/v1.0/me?$select=displayName,mail,userPrincipalName
```

This is a single-process POC. Sign-in links expire after 10 minutes and are consumed at callback validation. Authenticated identities are retained for up to eight hours; token lifetime and renewal are managed separately by Microsoft.Identity.Web/MSAL. Restarting the app clears its in-memory sign-in state and token cache. The deliberate word-by-word delay makes streaming visible; remove it from production code.

## Step 2: Create the Graph app registration

Keep the agent blueprint credentials separate from the credentials used for browser sign-in and Graph access.

1. Open [Microsoft Entra app registrations](https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade/quickStartType~/null/sourceType/Microsoft_AAD_IAM).
2. Select **New registration**, enter a name, select **Accounts in this organizational directory only**, and select **Register**. Use the same tenant as the Teams sender and agent blueprint.
3. On **Overview**, copy the **Application (client) ID** and **Directory (tenant) ID**.
4. Open **Certificates & secrets**, select **New client secret**, and create a secret.
5. Store the secret **Value** securely. Do not use the secret ID.
6. Open **API permissions** and select **Add a permission > Microsoft Graph > Delegated permissions**.
7. Add `User.Read`, or retain it if already present. No Graph application permission is required.
8. Ensure user consent is permitted by your tenant policy, or have an administrator select **Grant admin consent** for the tenant.

You will add the browser redirect URI in Step 5, after obtaining the tunnel URL.

## Step 3: Configure the application

Open [appsettings.json](code/appsettings.json) and configure the two identity sections:

| Setting | Value |
| --- | --- |
| `AzureAd:ClientId` | Agent blueprint application ID |
| `AzureAd:TenantId` | Agent blueprint tenant ID |
| `AzureAd:ClientCredentials:0:SourceType` | `ClientSecret` |
| `AzureAd:ClientCredentials:0:ClientSecret` | Agent blueprint secret value |
| `Graph:Instance` | `https://login.microsoftonline.com/` |
| `Graph:TenantId` | Tenant ID from Step 2 |
| `Graph:ClientId` | Graph app registration application ID from Step 2 |
| `Graph:ClientSecret` | Graph app registration secret value from Step 2 |
| `Graph:CallbackPath` | `/signin-oidc` |
| `Urls` | `http://localhost:3978` |

Set `PublicBaseUrl` in Step 4 before starting the application. The app requires a public HTTPS origin, without a path or query.

Do not commit populated client secrets. For local development, use .NET user secrets with the keys `AzureAd:ClientCredentials:0:ClientSecret` and `Graph:ClientSecret`. The project already defines a `UserSecretsId`. Rotate any credentials previously shared or committed, and use a secure secret store for production deployments.

## Step 4: Set up the dev tunnel

Make sure [Microsoft dev tunnel](../../dependencies/dev-tunnel/Readme.md) is installed and authenticated. In a separate terminal, run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

Keep the tunnel running while you configure and test the agent. Set `PublicBaseUrl` to the returned HTTPS origin. For example, if the tunnel origin is `https://domain.devtunnels.ms`, use that origin without `/api/messages` or `/signin-oidc`.

The same tunnel serves three different purposes:

| Purpose | URL using the example origin |
| --- | --- |
| Public application origin | `https://domain.devtunnels.ms` |
| Browser sign-in redirect URI | `https://domain.devtunnels.ms/signin-oidc` |
| Agent notification endpoint | `https://domain.devtunnels.ms/api/messages` |

Replace the example origin with your actual tunnel host everywhere. `Urls` remains `http://localhost:3978`; it controls the local listener, not the browser-facing URL.

## Step 5: Add the tunnel redirect URI to the Graph app registration

This step is required for browser sign-in. Configure it on the **Graph app registration**, not the agent blueprint.

1. In Entra **App registrations**, select the app whose application ID matches `Graph:ClientId`.
2. Open **Authentication > Add a platform > Web**. If a Web platform already exists, add the URI there.
3. Add your exact tunnel origin followed by `/signin-oidc` as a **Redirect URI**. Using the example origin:

   ```text
   https://domain.devtunnels.ms/signin-oidc
   ```

4. Select **Configure** or **Save**.

Use the **Web** platform, not **Single-page application**. Do not enable implicit grant checkboxes or public client flows for this sample.

The registered URI must match `PublicBaseUrl` plus `Graph:CallbackPath`. The code uses this same URI for both the browser authorization request and authorization code redemption. If the tunnel host changes, update `PublicBaseUrl` and the registered redirect URI, then restart the application.

## Step 6: Configure the agent notification URL

Open the [Microsoft 365 Developer Portal](https://dev.teams.microsoft.com/tools/agent-blueprint) and select the blueprint whose application ID matches `AzureAd:ClientId`.

Under **Configuration > Notification Configuration**:

1. Set **Agent Type** to **API Based**.
2. Set **Notification Url** to your tunnel origin followed by `/api/messages`.
3. Save the configuration.

This notification URL is separate from the Graph app's `/signin-oidc` redirect URI. Update it as well if your tunnel host changes.

## Step 7: Build and run the agent locally

From `labs/lab-06-delegated-graph/code`, run:

```powershell
dotnet build
dotnet run --no-build
```

The application listens on `http://localhost:3978`. Keep this terminal and the tunnel terminal running.

## Step 8: Test the Graph call

1. Open [Microsoft Teams](https://teams.microsoft.com), find the agentic user, and open a one-to-one chat.
2. Send any message and confirm that a **Sign in** card appears.
3. Select **Sign in** to open the browser.
4. Sign in with the **same human account that sent the Teams message**, not the agentic user's account. Grant consent if prompted.
5. Confirm that the browser displays **Signed in. Return to Teams and send another message. You can close this tab.**
6. Return to Teams and send another message. The callback does not automatically send a proactive reply.
7. Confirm that **Fetching your profile from Microsoft Graph** appears, followed by your display name and email streamed word by word.
8. Send another message and confirm that no additional sign-in is needed while the cached authentication remains usable.

## Troubleshooting

- **PublicBaseUrl configuration error**: Use only the actual HTTPS tunnel origin, without a path or query, and restart the app after changes.
- **No reply URL configured / redirect URI mismatch**: Add the exact tunnel `/signin-oidc` URI under the Graph app's **Authentication > Web** platform, as described in Step 5.
- **AADSTS500112 with localhost and tunnel URLs**: Both authorization and code redemption must use the same public callback URI. Use the current sample code, restart the app, and request a fresh sign-in card. Adding localhost as another redirect URI does not resolve a mismatch between the two requests.
- **Invalid client secret**: Verify the Graph app ID and secret value. Do not use the blueprint secret or the secret ID for `Graph:ClientSecret`.
- **Consent required / Graph access denied**: Verify delegated `User.Read` and your tenant's consent policy. Obtain administrator consent if required.
- **Expired or already used link**: Request a new card and complete sign-in within 10 minutes. Do not restart the app between requesting the card and completing sign-in.
- **Account or tenant mismatch**: Select the same account and tenant used to send the Teams message. The sample deliberately rejects a different browser account.
- **Missing tenant or object ID claims**: Confirm you are using an organizational account in the configured tenant. The callback requires an authenticated tenant and user object ID.
- **Sign-in required after restart**: Expected for this POC; sign-in state and tokens are stored in memory.
- **Missing streamId from end stream activity**: Use the current message handler, which appends the sign-in text before finalizing the stream with the card.
- **Build output locked**: Stop the running application before rebuilding, then restart it and request a new sign-in card.
- **Email fallback**: If `mail` is empty, the sample displays `userPrincipalName` instead.

For more information, see the [authorization code flow documentation](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow), [Microsoft.Identity.Web documentation](https://learn.microsoft.com/en-us/entra/msal/dotnet/microsoft-identity-web/), and [Get user API documentation](https://learn.microsoft.com/en-us/graph/api/user-get).