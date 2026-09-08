using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace DelegatedGraph;

internal static class BrowserSignIn
{
    internal const string Scheme = "GraphSignIn";
    private const string CookieScheme = "GraphCookies";

    internal static void AddBrowserSignIn(this IServiceCollection services, IConfiguration configuration, Uri publicUrl)
    {
        string redirectUri = new Uri(publicUrl, configuration["Graph:CallbackPath"] ?? "/signin-oidc").AbsoluteUri;
        services.AddMemoryCache();
        services.AddSingleton<SignInStore>();
        services.AddAuthentication()
            .AddMicrosoftIdentityWebApp(configuration.GetSection("Graph"),
                openIdConnectScheme: Scheme, cookieScheme: CookieScheme)
            .EnableTokenAcquisitionToCallDownstreamApi(GraphService.Scopes)
            .AddInMemoryTokenCaches();

        services.Configure<ConfidentialClientApplicationOptions>(Scheme,
            options => options.RedirectUri = redirectUri);

        services.Configure<OpenIdConnectOptions>(Scheme, (OpenIdConnectOptions options) =>
        {
            options.MapInboundClaims = false;
            options.UsePkce = true;
            options.SaveTokens = false;
            options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(10);
            var previousRedirect = options.Events.OnRedirectToIdentityProvider;
            options.Events.OnRedirectToIdentityProvider = async context =>
            {
                await previousRedirect(context);
                context.ProtocolMessage.RedirectUri = redirectUri;
                context.ProtocolMessage.Prompt = "select_account";
            };

            var previousTicketReceived = options.Events.OnTicketReceived;
            options.Events.OnTicketReceived = async context =>
            {
                await previousTicketReceived(context);
                SignInStore store = context.HttpContext.RequestServices.GetRequiredService<SignInStore>();
                string? ticket = null;
                context.Properties?.Items.TryGetValue("signin-ticket", out ticket);
                string? error = ticket is null || context.Principal is null
                    ? "The sign-in callback is missing its ticket or account. Request a new card in Teams."
                    : store.Complete(ticket, context.Principal);
                context.HandleResponse();
                context.Response.StatusCode = error is null ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                await context.Response.WriteAsync(error ??
                    "Signed in. Return to Teams and send another message. You can close this tab.");
            };

            options.Events.OnRemoteFailure = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsync("Sign-in failed or was cancelled. Return to Teams and request a new sign-in card.");
            };
        });
    }

    internal static void MapBrowserSignIn(this WebApplication app)
    {
        app.MapGet("/auth/signin", async (string ticket, HttpContext context, SignInStore store) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (!store.HasLink(ticket))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("This link expired or was used. Request a new sign-in card in Teams.");
                return;
            }

            AuthenticationProperties properties = new();
            properties.Items["signin-ticket"] = ticket;
            await context.ChallengeAsync(Scheme, properties);
        }).AllowAnonymous();
    }
}