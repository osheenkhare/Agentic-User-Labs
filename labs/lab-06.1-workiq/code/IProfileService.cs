using System.Security.Claims;

namespace WorkIqLab;

internal interface IProfileService
{
    Task<UserProfile> GetMeAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}

internal sealed record UserProfile(string? DisplayName, string? Mail, string? UserPrincipalName, string? Id = null);

internal sealed class ProfileLookupException(string message) : Exception(message);
