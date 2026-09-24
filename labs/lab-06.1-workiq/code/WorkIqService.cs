using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Identity.Web;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace WorkIqLab;

internal sealed class WorkIqService(
    ITokenAcquisition tokens,
    HttpClient httpClient,
    WorkIqSettings settings) : IProfileService
{
    internal static readonly string[] Scopes = ["api://workiq.svc.cloud.microsoft/WorkIQAgent.Ask"];
    internal const string ProfilePath = "/me?$select=id,displayName,mail,userPrincipalName";

    public async Task<UserProfile> GetMeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        ValidateIdentity(user, settings.TenantId);
        string accessToken = await tokens.GetAccessTokenForUserAsync(
            Scopes, authenticationScheme: BrowserSignIn.Scheme, user: user);
        return await FetchProfileAsync(httpClient, accessToken, user, settings.TenantId, cancellationToken);
    }

    internal static async Task<UserProfile> FetchProfileAsync(
        HttpClient httpClient, string accessToken, ClaimsPrincipal user,
        string tenantId, CancellationToken cancellationToken)
    {
        ValidateIdentity(user, tenantId);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        // Each lookup owns its MCP session and bearer header; never share either across users.
        await using HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://workiq.svc.cloud.microsoft/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {accessToken}"
            }
        }, httpClient);
        await using McpClient client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        CallToolResult result = await client.CallToolAsync(
            "fetch", new Dictionary<string, object?> { ["entityUrls"] = new[] { ProfilePath } },
            cancellationToken: timeout.Token);
        return ReadProfile(result, user, tenantId);
    }

    internal static UserProfile ReadProfile(CallToolResult result, ClaimsPrincipal user, string tenantId)
    {
        ValidateIdentity(user, tenantId);
        if (result.IsError is true)
        {
            throw new ProfileLookupException("WorkIQ reported a fetch tool error.");
        }

        if (result.StructuredContent is JsonElement structured)
        {
            return ReadEnvelope(structured, user);
        }

        // MCP also permits structured JSON serialized as a single text content block.
        if (result.Content.Count != 1 || result.Content[0] is not TextContentBlock text)
        {
            throw new ProfileLookupException("WorkIQ did not return a single JSON profile envelope.");
        }
        using JsonDocument document = JsonDocument.Parse(text.Text);
        return ReadEnvelope(document.RootElement, user);
    }

    private static UserProfile ReadEnvelope(JsonElement envelope, ClaimsPrincipal user)
    {
        if (envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("results", out JsonElement results) ||
            results.ValueKind != JsonValueKind.Array || results.GetArrayLength() != 1)
        {
            throw new ProfileLookupException("WorkIQ returned an unexpected fetch envelope.");
        }
        JsonElement result = results[0];
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("statusCode", out JsonElement status) ||
            status.ValueKind != JsonValueKind.Number || !status.TryGetInt32(out int statusCode))
        {
            throw new ProfileLookupException("WorkIQ omitted the fetch status code.");
        }
        if (statusCode != 200)
        {
            throw new HttpRequestException("WorkIQ profile fetch failed.", null, (HttpStatusCode)statusCode);
        }
        if (!result.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new ProfileLookupException("WorkIQ returned no profile data.");
        }
        UserProfile profile = data.Deserialize<UserProfile>(JsonSerializerOptions.Web)
            ?? throw new ProfileLookupException("WorkIQ returned an empty profile.");
        if (!Guid.TryParse(profile.Id, out Guid profileId) ||
            !Guid.TryParse(user.GetObjectId(), out Guid signedInId) || profileId != signedInId)
        {
            throw new ProfileLookupException("WorkIQ profile does not match the authenticated Teams sender.");
        }
        return profile;
    }

    private static void ValidateIdentity(ClaimsPrincipal user, string tenantId)
    {
        // Tenant comes from the validated OIDC identity and account-scoped token cache, not mail/UPN.
        if (user.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(user.GetObjectId(), out _) ||
            !Guid.TryParse(user.GetTenantId(), out Guid signedInTenant) ||
            !Guid.TryParse(tenantId, out Guid expectedTenant) || signedInTenant != expectedTenant)
        {
            throw new ProfileLookupException("WorkIQ requires the authenticated sender in the configured tenant.");
        }
    }
}
