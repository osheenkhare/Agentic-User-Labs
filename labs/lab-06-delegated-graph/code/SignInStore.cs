using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Web;

namespace DelegatedGraph;

internal sealed class SignInStore(IMemoryCache cache)
{
    private readonly object _gate = new();

    internal string CreateLink(string tenantId, string userId)
    {
        string ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        cache.Set($"link:{ticket}", new Sender(tenantId, userId), TimeSpan.FromMinutes(10));
        return ticket;
    }

    internal bool HasLink(string ticket) => cache.TryGetValue($"link:{ticket}", out Sender? _);

    internal string? Complete(string ticket, ClaimsPrincipal principal)
    {
        lock (_gate)
        {
            if (!cache.TryGetValue($"link:{ticket}", out Sender? sender) || sender is null)
            {
                return "This sign-in link expired or was already used. Request a new card in Teams.";
            }

            cache.Remove($"link:{ticket}");
            if (principal.Identity?.IsAuthenticated != true)
            {
                return "The browser account was not authenticated. Request a new card in Teams.";
            }

            string? tenantId = principal.GetTenantId();
            string? userId = principal.GetObjectId();
            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            {
                return "The signed-in account is missing its tenant or object ID claim. Check the Graph app sign-in configuration.";
            }

            if (!string.Equals(tenantId, sender.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return "The signed-in account belongs to a different tenant. Request a new card and use your Teams tenant account.";
            }

            if (!string.Equals(userId, sender.UserId, StringComparison.OrdinalIgnoreCase))
            {
                return "The signed-in account is not the Teams message sender. Request a new card and select the same account you use in Teams.";
            }

            cache.Set(UserKey(sender.TenantId, sender.UserId), principal, TimeSpan.FromHours(8));
            return null;
        }
    }

    internal ClaimsPrincipal? GetUser(string tenantId, string userId) =>
        cache.Get<ClaimsPrincipal>(UserKey(tenantId, userId));

    internal void RemoveUser(string tenantId, string userId) =>
        cache.Remove(UserKey(tenantId, userId));

    private static string UserKey(string tenantId, string userId) =>
        $"user:{tenantId.ToLowerInvariant()}:{userId.ToLowerInvariant()}";

    private sealed record Sender(string TenantId, string UserId);
}