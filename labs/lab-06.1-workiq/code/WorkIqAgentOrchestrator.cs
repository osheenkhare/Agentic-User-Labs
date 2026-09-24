using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Teams.Apps.Schema;
using ModelContextProtocol;

namespace WorkIqLab;

internal sealed class WorkIqAgentOrchestrator(
    IProfileService profileService,
    SignInStore signIns,
    WorkIqSettings settings,
    Uri publicUrl,
    ILogger<WorkIqAgentOrchestrator> logger) : IAgentOrchestrator
{
    public async IAsyncEnumerable<IAgentEvent> GetUpdatesAsync(
        MessageActivity activity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string tenantId = settings.TenantId;
        string? userId = activity.From?.AadObjectId;
        if (string.IsNullOrWhiteSpace(userId) ||
            !string.Equals(activity.ChannelData?.Tenant?.Id, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            yield return new AgentEvent(
                "Please message this agent from the configured tenant using your Microsoft 365 account.");
            yield break;
        }

        var user = signIns.GetUser(tenantId, userId);
        UserProfile? profile = null;
        string? error = null;
        if (user is not null)
        {
            yield return new AgentEvent(
                "Fetching your profile from WorkIQ",
                IsInformative: true);

            try
            {
                profile = await profileService.GetMeAsync(user, cancellationToken);
            }
            catch (MicrosoftIdentityWebChallengeUserException)
            {
                signIns.RemoveUser(tenantId, userId);
            }
            catch (MsalUiRequiredException)
            {
                signIns.RemoveUser(tenantId, userId);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                signIns.RemoveUser(tenantId, userId);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning("WorkIQ profile lookup failed with HTTP status {Status}.", exception.StatusCode);
                error = "WorkIQ could not return your profile. Check service access and delegated consent, then try again later.";
            }
            catch (ProfileLookupException)
            {
                signIns.RemoveUser(tenantId, userId);
                logger.LogWarning("WorkIQ profile lookup failed tool-result or identity validation.");
                error = "WorkIQ returned an invalid, failed, or mismatched profile. No profile was displayed. Request a new sign-in card.";
            }
            catch (Exception exception) when (exception is McpException or JsonException)
            {
                logger.LogWarning("WorkIQ profile lookup failed with {ErrorType}.", exception.GetType().Name);
                error = "WorkIQ returned an MCP error or unreadable response. No profile was displayed.";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                error = "WorkIQ profile lookup timed out. Try again later.";
            }
        }

        if (error is not null)
        {
            yield return new AgentEvent(error);
            yield break;
        }

        if (profile is null)
        {
            string ticket = signIns.CreateLink(tenantId, userId);
            string signInUrl = new Uri(publicUrl, $"/auth/signin?ticket={ticket}").AbsoluteUri;
            yield return new AgentEvent("Sign in to WorkIQ", SignInUrl: signInUrl);
            yield break;
        }

        string displayName = profile.DisplayName ?? profile.UserPrincipalName ?? "there";
        string email = profile.Mail ?? profile.UserPrincipalName ?? "not available";
        string message = $"Hi `{displayName}`, WorkIQ returned your email as `{email}`.";

        foreach (string word in message.Split(' '))
        {
            await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
            yield return new AgentEvent($"{word} ");
        }
    }
}