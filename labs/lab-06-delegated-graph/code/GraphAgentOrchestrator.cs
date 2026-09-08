using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Teams.Apps.Schema;

namespace DelegatedGraph;

internal sealed class GraphAgentOrchestrator(
    GraphService graphService,
    SignInStore signIns,
    string tenantId,
    Uri publicUrl) : IAgentOrchestrator
{
    public async IAsyncEnumerable<IAgentEvent> GetUpdatesAsync(
        MessageActivity activity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
                "Fetching your profile from Microsoft Graph",
                IsInformative: true);

            try
            {
                profile = await graphService.GetMeAsync(user, cancellationToken);
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
            catch (HttpRequestException)
            {
                error = "Microsoft Graph could not return your profile. Check delegated User.Read consent and try again later.";
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
            yield return new AgentEvent("Sign in to Microsoft Graph", SignInUrl: signInUrl);
            yield break;
        }

        string displayName = profile.DisplayName ?? profile.UserPrincipalName ?? "there";
        string email = profile.Mail ?? profile.UserPrincipalName ?? "not available";
        string message = $"Hi `{displayName}`, Microsoft Graph returned your email as `{email}`.";

        foreach (string word in message.Split(' '))
        {
            await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
            yield return new AgentEvent($"{word} ");
        }
    }
}