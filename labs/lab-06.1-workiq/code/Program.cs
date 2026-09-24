using System.Text.Json;
using WorkIqLab;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Handlers;
using Microsoft.Teams.Apps.Schema;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WorkIqSettings settings = new(builder.Configuration);

if (!Uri.TryCreate(builder.Configuration["PublicBaseUrl"], UriKind.Absolute, out Uri? publicUrl) ||
    publicUrl.Scheme != Uri.UriSchemeHttps || publicUrl.AbsolutePath != "/" ||
    publicUrl.Query.Length > 0 || publicUrl.Fragment.Length > 0 || publicUrl.UserInfo.Length > 0)
{
    throw new InvalidOperationException("PublicBaseUrl must be the HTTPS dev tunnel origin, without a path or query.");
}

builder.Services.AddTeamsBotApplication();
builder.Services.AddSingleton(settings);
builder.Services.AddBrowserSignIn(builder.Configuration, publicUrl);
builder.Services.AddHttpClient<IProfileService, WorkIqService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    });

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapBrowserSignIn();
TeamsBotApplication teams = app.UseTeamsBotApplication();
SignInStore signIns = app.Services.GetRequiredService<SignInStore>();

teams.OnMessage(async (context, cancellationToken) =>
{
    using IServiceScope scope = app.Services.CreateScope();
    IProfileService profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
    IAgentOrchestrator agent = new WorkIqAgentOrchestrator(
        profileService, signIns, settings, publicUrl,
        scope.ServiceProvider.GetRequiredService<ILogger<WorkIqAgentOrchestrator>>());
    TeamsStreamingWriter stream = TeamsStreamingWriter.CreateFromContext(context);
    bool streamingStarted = false;

    await foreach (IAgentEvent update in agent.GetUpdatesAsync(context.Activity, cancellationToken))
    {
        if (update.SignInUrl is not null)
        {
            JsonElement card = JsonSerializer.SerializeToElement(new
            {
                type = "AdaptiveCard",
                version = "1.5",
                body = new[] { new { type = "TextBlock", text = update.Text, weight = "Bolder", wrap = true } },
                actions = new[] { new { type = "Action.OpenUrl", title = "Sign in", url = update.SignInUrl } }
            });
            TeamsAttachment attachment = TeamsAttachment.CreateBuilder().WithAdaptiveCard(card).Build();
            MessageActivity response = new MessageActivity { Text = update.Text }.AddAttachment(attachment);
            await stream.AppendResponseAsync(update.Text, cancellationToken);
            await stream.FinalizeResponseAsync(response, cancellationToken);
            return;
        }
        else if (update.IsInformative)
        {
            if (streamingStarted)
            {
                continue;
            }

            await stream.SendInformativeUpdateAsync(update.Text, cancellationToken);
        }
        else if (update.Text.Length > 0)
        {
            streamingStarted = true;
            await stream.AppendResponseAsync(update.Text, cancellationToken);
        }
    }

    await stream.FinalizeResponseAsync(cancellationToken: cancellationToken);
});

await app.RunAsync();
