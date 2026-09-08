using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Identity.Web;

namespace DelegatedGraph;

internal sealed class GraphService(ITokenAcquisition tokens, HttpClient httpClient)
{
    internal static readonly string[] Scopes = ["https://graph.microsoft.com/User.Read"];

    internal async Task<UserProfile> GetMeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        string accessToken = await tokens.GetAccessTokenForUserAsync(
            Scopes, authenticationScheme: BrowserSignIn.Scheme, user: user);

        using HttpRequestMessage request = new(HttpMethod.Get,
            "https://graph.microsoft.com/v1.0/me?$select=displayName,mail,userPrincipalName");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>(cancellationToken)
            ?? throw new InvalidOperationException("Microsoft Graph returned an empty profile.");
    }
}

internal sealed record UserProfile(string? DisplayName, string? Mail, string? UserPrincipalName);