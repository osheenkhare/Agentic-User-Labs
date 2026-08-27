# Lab 00 - Set Up Your Environment

⏰ Estimated time: 15-40 min

## Overview

In this lab, you will prepare the Microsoft 365 tenant and local development environment used by the remaining labs. The sample applications target .NET 10 and use Microsoft dev tunnels to expose a local service on port `3978` to Microsoft 365.

By the end of this lab, you will have:

- A Microsoft 365 test tenant in which you are an administrator
- An available Microsoft 365 license for the agentic user
- The .NET 10 SDK installed locally
- Microsoft dev tunnel installed and authenticated

## 1. Prepare a Microsoft 365 test tenant

Use a dedicated Microsoft 365 test tenant. Do not use a production tenant because the labs create identities, grant permissions, assign licenses, and expose a development callback endpoint.

Your test account must be an administrator in the tenant. A **Global Administrator** account is recommended because the labs require you to:

- Create agent blueprints, agent identities, and agentic users
- Grant admin consent for Microsoft Graph permissions
- Create credentials and OAuth permission grants
- Assign a Microsoft 365 license
- Configure an agent blueprint in the Teams Developer Portal

Confirm that you can sign in to the following portals with the same test-tenant account:

- [Microsoft Entra admin center](https://entra.microsoft.com/)
- [Microsoft 365 admin center](https://admin.microsoft.com/)
- [Microsoft Teams Developer Portal](https://dev.teams.microsoft.com/)
- [Microsoft Graph Explorer](https://developer.microsoft.com/graph/graph-explorer)

> Keep tenant IDs, application IDs, and client secrets secure. Never commit client secrets or populated configuration files to source control.

## 2. Confirm that a Microsoft 365 license is available

The tenant must contain at least one unassigned Microsoft 365 license that can be assigned to the agentic user created in Lab 01. A **Microsoft 365 E3** license is used in that lab, but another suitable Microsoft 365 license available in your tenant can also be used.

1. Open the [Microsoft 365 admin center](https://admin.microsoft.com/).
2. Go to **Billing > Your products** to confirm that the tenant has a Microsoft 365 subscription.
3. Go to **Billing > Licenses** and confirm that at least one license is available to assign.

Do not assign the reserved license yet. You will assign it to the agentic user in Lab 01.

## 3. Install the .NET 10 SDK

The applications in this repository target `net10.0`, so install the **.NET 10 SDK**, not only the .NET runtime.

Use the official [.NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0), or install it on Windows with WinGet:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Open a new terminal after installation, then verify the SDK:

```powershell
dotnet --list-sdks
```

The output must include a version beginning with `10.`. You can also run:

```powershell
dotnet --version
```

If this command reports an older SDK, confirm that a .NET 10 SDK appears in `dotnet --list-sdks` before continuing. The project selects the compatible SDK when it builds.

## 4. Install and configure Microsoft dev tunnel

The local agent applications listen on port `3978`. Microsoft 365 must be able to reach that local endpoint, so the later labs use Microsoft dev tunnel to provide a temporary public HTTPS URL.

Follow the repository's [Microsoft dev tunnel setup guide](../../dependencies/dev-tunnel/Readme.md). Complete the installation and sign-in steps with the same account used for your Microsoft 365 test tenant.

Verify the installation:

```powershell
devtunnel --version
```

You do not need to start a tunnel yet. When a later lab asks you to expose an agent, you will run:

```powershell
devtunnel host -p 3978 --allow-anonymous
```

Keep that terminal open while testing. Dev tunnels are for development only, and the generated URL can change when the tunnel is restarted.

## Environment checklist

Before continuing to Lab 01, confirm that:

- [ ] You are using a non-production Microsoft 365 test tenant.
- [ ] Your test account is an administrator and can grant admin consent.
- [ ] The tenant has an available Microsoft 365 E3 or other suitable Microsoft 365 license.
- [ ] `dotnet --list-sdks` lists a .NET 10 SDK.
- [ ] `devtunnel --version` completes successfully.
- [ ] Dev tunnel is authenticated with your Microsoft 365 test account.

You are now ready to continue to [Lab 01 - Create a Digital Worker](../lab-01-create-digital-worker/Readme.md).
