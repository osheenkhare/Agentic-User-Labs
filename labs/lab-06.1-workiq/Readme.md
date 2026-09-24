# Lab 6.1 - Read the Signed-In User's Profile with WorkIQ

Estimated time: 60 min

## Overview

This standalone lab copies the browser sign-in and native Teams streaming pattern from [Lab 06](../lab-06-delegated-graph/Readme.md), but retrieves the requesting human's profile through hosted **WorkIQ MCP** instead of directly calling Microsoft Graph. Lab 06 remains unchanged and can still be run independently.

1. The human messages the agentic user (AU) in Teams.
2. The application returns a sign-in card bound to the sender's tenant and object ID.
3. Browser sign-in authenticates that **same human**, using authorization code flow with PKCE.
4. On the next Teams message, the application acquires a delegated WorkIQ token for that human and calls `fetch` with `/me?$select=id,displayName,mail,userPrincipalName`.
5. After validating the profile's ID, `TeamsStreamingWriter` streams the display name and email back as the AU.

WorkIQ is the only profile implementation here: there is no provider selector, direct Graph fallback, `ask` synthesis, or WorkIQ-based Teams send operation. The Teams SDK still handles the agent, cards, callbacks, and response streaming. This is not Teams SSO, an OBO exchange, or application-only authentication. WorkIQ does not authenticate as the AU.

## Prerequisites

You need the .NET 10 SDK, an agent blueprint and agentic user, a tenant-enabled Work IQ API and authorized human user, a customer-owned confidential Web app registration, and an HTTPS dev tunnel. See [Lab 01](../lab-01-create-digital-worker/Readme.md) for agent provisioning and the [dev tunnel guide](../../dependencies/dev-tunnel/Readme.md) for tunnel setup. No earlier lab's source code or project is required to build this lab.

**Local build/regression results are not live WorkIQ proof.** Consent and callback configuration alone also do not establish that a particular app/user can successfully fetch a profile. The final acceptance step requires an actual human sign-in, same-user WorkIQ lookup, and Teams response.

## Step 1: Understand the standalone sample

Open `labs\lab-06.1-workiq\code` in your checkout or isolated worktree.

| File | Responsibility |
| --- | --- |
| [WorkIqLab.csproj](code/WorkIqLab.csproj) | Standalone .NET web app; Microsoft.Identity.Web, Teams SDK, and official ModelContextProtocol.Core client |
| [Program.cs](code/Program.cs) | Validates configuration, registers WorkIQ, and renders native Teams streams and sign-in cards |
| [WorkIqSettings.cs](code/WorkIqSettings.cs) | Requires a concrete WorkIQ tenant GUID |
| [BrowserSignIn.cs](code/BrowserSignIn.cs) | Human authorization-code sign-in with PKCE and `/signin-workiq` callback |
| [SignInStore.cs](code/SignInStore.cs) | One-time links, authenticated sender matching, and per-user identity cache |
| [WorkIqService.cs](code/WorkIqService.cs) | Account-specific token acquisition, MCP fetch, and strict response/identity validation |
| [IProfileService.cs](code/IProfileService.cs) | Profile service contract, result model, and validation error |
| [WorkIqAgentOrchestrator.cs](code/WorkIqAgentOrchestrator.cs) | Sign-in, lookup, failure handling, and word-by-word profile events |
| [IAgentOrchestrator.cs](code/IAgentOrchestrator.cs) | Streaming event contracts |
| [appsettings.json](code/appsettings.json) | Placeholder blueprint, WorkIQ, public-origin, and listener settings |
| [Validation program](validation/Program.cs) | Offline regression checks, including real MCP SDK exchanges through an in-memory HTTP handler |

### Identity and data isolation

The callback consumes a one-time ticket and checks the OIDC-validated `tid` and `oid` claims against the incoming Teams sender before storing the principal. Microsoft.Identity.Web obtains the WorkIQ-scoped token for that principal using its account-isolated MSAL cache. There is no global "current user" token.

Every lookup owns a fresh MCP transport/session and bearer header. Redirects and HTTP cookies are disabled for the WorkIQ client. The only application tool call is:

```json
{
  "name": "fetch",
  "arguments": {
    "entityUrls": ["/me?$select=id,displayName,mail,userPrincipalName"]
  }
}
```

The [WorkIQ tool reference](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq/mcp/tool-reference#fetch) documents a `results` array with `statusCode` and `data`. The parser accepts that envelope from structured content or a single JSON text block, requires one HTTP 200 result, and checks `data.id` against the authenticated `oid`. Errors, missing IDs, ambiguous envelopes, mismatches, and non-JSON answers are rejected, not turned into profiles.

Tenant binding comes from the validated tenant-specific OIDC identity and matching account-scoped token acquisition, not the UPN suffix or an invented tenant field in `/me`. MCP operations honor cancellation and have a 60-second deadline. Sign-in links expire after 10 minutes; principals are cached for eight hours. Restarting clears both sign-in state and the in-memory token cache. This is a single-process lab, not a durable multi-instance service.

## Step 2: Configure delegated WorkIQ access

Do not change tenant permissions or an existing running lab without coordination. If the required permission and callback have already been configured, retain them; this lab's packaging does not require a new app registration.

Microsoft's [Work IQ MCP Foundry quickstart](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq/mcp/quickstart/foundry) documents a customer-owned app registration, client secret, Web callback, and tenant-specific OAuth endpoints for the hosted MCP service. It demonstrates the confidential-client model in Foundry; this sample implements interactive browser authorization in ASP.NET using Microsoft.Identity.Web. Foundry is not a runtime dependency.

| Authentication setting | Value |
| --- | --- |
| Hosted MCP endpoint | `https://workiq.svc.cloud.microsoft/mcp` |
| API display name / resource app ID | Work IQ / `fdcc1f02-fc51-4226-8753-f668596af7f7` |
| Delegated permission | `WorkIQAgent.Ask` |
| Delegated permission ID | `0b1715fd-f4bf-4c63-b16d-5be31f9847c2` |
| Scope used by this lab | `api://workiq.svc.cloud.microsoft/WorkIQAgent.Ask` |
| Browser authority | `https://login.microsoftonline.com/<WorkIQ:TenantId>/v2.0` |
| Web callback | `<PublicBaseUrl>/signin-workiq` |

1. Confirm hosted Work IQ API enablement and the requesting user's access with your administrator. Existing CLI enablement alone does not establish this custom app's access. See [Enable Work IQ APIs](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq/enable-work-iq); do not infer entitlement from a local SKU list.
2. Create or choose a customer-owned, single-tenant **confidential Web app** in the sender's tenant. You may reuse Lab 06's human app registration and its valid secret. Keep the original `User.Read` permission and `/signin-oidc` callback for Lab 06 rollback. A separate WorkIQ registration is optional, not required.
3. Under **API permissions**, add **Work IQ > Delegated permissions > WorkIQAgent.Ask**, then obtain **admin consent**. If Work IQ is absent, have the administrator follow the official enablement guide; the sample does not provision it.
4. Under **Authentication > Web**, add the chosen public origin plus `/signin-workiq`. Do not use a SPA platform or enable implicit grant/public client flows.

The [permission reference](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq/permissions) requires admin consent. Despite the `Ask` name, the permission covers supported **read/write** access in the user's context; this lab invokes only read-only `fetch`. Do not add Graph permissions or enable mutation policies just for this lookup. Do not use the blueprint ID, AU credentials, or Microsoft's public CLI client ID (`ba081686-5d24-4bc6-a0d6-d034ecffed87`) as the human Web app.

### Scope discovery versus client authorization

Unauthenticated discovery on September 24, 2026 returned HTTP 401 with `resource_metadata` pointing to `https://workiq.svc.cloud.microsoft/.well-known/oauth-protected-resource/mcp`. It advertised the organizations authority and GUID-prefixed scope `fdcc1f02-fc51-4226-8753-f668596af7f7/WorkIQAgent.Ask`. The code instead uses the Application-ID-URI scope published in Microsoft's permission reference and MCP quickstart. Scope alias equivalence has not been token-tested here, and a Graph access token is not assumed to work for WorkIQ.

The MCP resource URL is not a JWT audience assertion. WorkIQ validates its access token; this application validates the browser ID token through OIDC rather than decoding and trusting an access token. No extra public client-ID allowlisting requirement was found in the cited registration instructions, but this is not proof of a particular app/tenant's authorization. Never reuse a connected chat/CLI MCP session or paste bearer tokens into configuration or chat.

## Step 3: Choose the public endpoint

For a **sequential switch from Lab 06**, keep the existing tunnel, blueprint/AU, public origin, and `/api/messages` notification URL. After review and setup, the user manually stops only the old app and runs Lab 6.1 on **3978**. Never run both apps on that port, and do not edit the primary checkout. No new blueprint or tunnel is needed.

For a fresh setup, start a dev tunnel for the chosen local port and configure the blueprint's notification URL to `<PublicBaseUrl>/api/messages`. If the baseline must remain running on 3978, a parallel run needs a separate port/tunnel (for example 3980) and coordinated Teams routing: a separate test blueprint/AU or an explicitly approved notification URL change. A second tunnel alone does not redirect existing Teams traffic.

Use only an HTTPS origin for `PublicBaseUrl`, without a path, query, or fragment. The human Web app redirect is `<PublicBaseUrl>/signin-workiq`, not `/api/messages`.

## Step 4: Build and run offline regression checks

From the **repository/worktree root**, run:

```powershell
dotnet build .\labs\lab-06.1-workiq\code\WorkIqLab.csproj
dotnet run --project .\labs\lab-06.1-workiq\validation\WorkIqLab.Validation.csproj
```

The validation executable adds no test-framework packages and references only this lab's project. It uses synthetic identities and an in-memory HTTP handler; it never starts a server or contacts Entra, WorkIQ, or Teams. Checks cover WorkIQ OIDC configuration, ticket isolation, profile mismatches, malformed/error envelopes, stream/sign-in events, and the real MCP SDK wire exchange with concurrent per-user headers. They are not proof of live service authorization.

## Step 5: Configure and launch when ready

The configuration sections are **AzureAd** for the blueprint and **WorkIQ** for the requesting human's Web app. There is no `Graph` configuration or `Profile:Provider` setting. The project/namespace is `WorkIqLab`; its development `UserSecretsId` is `agentic-user-labs-workiq`, separate from Lab 06.

Use a **new dedicated PowerShell terminal** at the repository/worktree root. Enter secrets only at masked local prompts, not in chat, command arguments, or committed files. The example uses Production environment to avoid automatically loading any development user-secrets store; that does not make the single-process lab production-ready.

```powershell
$env:DOTNET_ENVIRONMENT = 'Production'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:WorkIQ__Instance = 'https://login.microsoftonline.com/'
$env:WorkIQ__TenantId = Read-Host 'Requesting human tenant ID'
$env:WorkIQ__ClientId = Read-Host 'Your own admin-consented WorkIQ Web app client ID'
$env:WorkIQ__CallbackPath = '/signin-workiq'
$env:AzureAd__TenantId = $env:WorkIQ__TenantId
$env:AzureAd__ClientId = Read-Host 'Existing blueprint client ID (or coordinated test blueprint)'
$env:AzureAd__ClientCredentials__0__SourceType = 'ClientSecret'
$env:PublicBaseUrl = Read-Host 'HTTPS tunnel origin (keep existing origin for sequential switch)'
$port = Read-Host '3978 only after manually stopping old app; 3980 for coordinated parallel setup'
if ($port -notin @('3978', '3980')) { throw 'Choose the coordinated lab port.' }
try {
    $secret = Read-Host 'Human Web app secret VALUE' -AsSecureString
    $env:WorkIQ__ClientSecret = [System.Net.NetworkCredential]::new('', $secret).Password
    $secret.Dispose()
    $secret = Read-Host 'Blueprint secret VALUE' -AsSecureString
    $env:AzureAd__ClientCredentials__0__ClientSecret = [System.Net.NetworkCredential]::new('', $secret).Password
    $secret.Dispose()
    dotnet run --no-build --no-launch-profile --project .\labs\lab-06.1-workiq\code\WorkIqLab.csproj -- --urls "http://localhost:$port"
}
finally {
    Remove-Item Env:WorkIQ__ClientSecret -ErrorAction SilentlyContinue
    Remove-Item Env:AzureAd__ClientCredentials__0__ClientSecret -ErrorAction SilentlyContinue
}
```

Process environment is local lab secret handling, not a production secret store. Close the dedicated terminal when finished. Do not write either secret into `appsettings.json`, persist it using `setx`, or copy another process's secret environment. Keep identity/HTTP/MCP debug logging disabled with real credentials; sample code does not log tokens, raw profile bodies, or raw remote error text.

## Step 6: Perform the live acceptance test

Live WorkIQ identity validation and the Teams round trip remain **unconfirmed** until performed with the configured human and AU.

1. Send a message to the AU and open **Sign in to WorkIQ**.
2. Authenticate as the **same human account that sent the Teams message**, not the AU.
3. Confirm the browser says to return to Teams. The callback does not send a proactive reply.
4. Send another Teams message. Confirm **Fetching your profile from WorkIQ** appears. Success then requires a real WorkIQ `/me` response whose ID matches that human and the profile streaming back to Teams.
5. Confirm a different browser user or tenant is rejected, a second authorized human cannot receive the first human's profile, and later messages reuse only the correct human's cached authentication.

## Troubleshooting and rollback

- **Redirect mismatch:** Register the exact public `/signin-workiq` URI on the human Web app, not the blueprint. Authorization and code redemption use the same public redirect.
- **Invalid secret:** Use the human Web app secret for `WorkIQ:ClientSecret`; use the blueprint secret for `AzureAd:ClientCredentials:0:ClientSecret`.
- **Expired/reused sign-in link:** Request a fresh Teams card and complete sign-in within 10 minutes without restarting the app.
- **Tenant/account mismatch:** Use the same organizational account and tenant as the Teams sender; the rejection is intentional.
- **HTTP 401:** The cached principal is removed and a new sign-in card is required.
- **HTTP 403, throttling, policy denial, tool error, invalid JSON, or profile mismatch:** The app displays an explicit failure rather than a direct Graph substitute or invented data. Check service/consent diagnostics; do not infer a specific permission or entitlement cause from an undetailed tool error.
- **Port busy:** Do not stop unrelated processes. Coordinate the manual switch or use a separately routed test port.
- **Email fallback:** A null `mail` uses `userPrincipalName`; the profile ID must still match.
- **Rollback:** Stop Lab 6.1 and restart the unchanged Lab 06 baseline with its original configuration. Request a new sign-in card. If a notification URL was changed for a parallel test, coordinate its restoration separately. There is no provider toggle in Lab 6.1.
