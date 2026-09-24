using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using WorkIqLab;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Teams.Apps.Schema;
using ModelContextProtocol.Protocol;

const string tenant = "11111111-1111-1111-1111-111111111111";
const string alice = "22222222-2222-2222-2222-222222222222";
const string bob = "33333333-3333-3333-3333-333333333333";
ClaimsPrincipal user = Identity(tenant, alice);
int checks = 0;

Check("structured profile", () =>
{
    UserProfile profile = WorkIqService.ReadProfile(Result(Envelope(alice)), user, tenant);
    Require(profile.Id == alice && profile.DisplayName == "Example" && profile.Mail is null &&
        profile.UserPrincipalName == "example@invalid.test");
});
Check("text JSON profile", () =>
{
    CallToolResult result = new() { Content = [new TextContentBlock { Text = Envelope(alice) }] };
    Require(WorkIqService.ReadProfile(result, user, tenant).Id == alice);
});
Reject<ProfileLookupException>("different profile user", Result(Envelope(bob)));
Reject<ProfileLookupException>("missing profile ID",
    Result("""{"results":[{"statusCode":200,"data":{"displayName":"Example"}}]}"""));
Reject<ProfileLookupException>("MCP tool error", new() { IsError = true, StructuredContent = Json(Envelope(alice)) });
Reject<ProfileLookupException>("empty result", Result("""{"results":[]}"""));
Reject<ProfileLookupException>("multiple results",
    Result("""{"results":[{"statusCode":200,"data":{}},{"statusCode":200,"data":{}}]}"""));
Reject<ProfileLookupException>("missing status", Result("""{"results":[{"data":{}}]}"""));
Reject<ProfileLookupException>("string status", Result("""{"results":[{"statusCode":"200","data":{}}]}"""));
Reject<ProfileLookupException>("null data", Result("""{"results":[{"statusCode":200,"data":null}]}"""));
Reject<ProfileLookupException>("null envelope", Result("null"));
Reject<ProfileLookupException>("empty content", new());
Reject<ProfileLookupException>("ambiguous text blocks", new()
{
    Content = [new TextContentBlock { Text = Envelope(alice) }, new TextContentBlock { Text = Envelope(bob) }]
});
Reject<JsonException>("non-JSON text", new() { Content = [new TextContentBlock { Text = "not a profile" }] });
Reject<JsonException>("wrong field type",
    Result($$$"""{"results":[{"statusCode":200,"data":{"id":"{{{alice}}}","mail":42}}]}"""));
foreach (int status in new[] { 401, 403, 429, 500 })
{
    Check($"in-band HTTP {status}", () =>
    {
        try
        {
            WorkIqService.ReadProfile(Result($$$"""{"results":[{"statusCode":{{{status}}},"data":{}}]}"""), user, tenant);
        }
        catch (HttpRequestException exception) when ((int?)exception.StatusCode == status)
        {
            return;
        }
        throw new Exception("Expected exact HTTP status.");
    });
}
Check("tenant binding", () => Throws<ProfileLookupException>(() =>
    WorkIqService.ReadProfile(Result(Envelope(alice)), Identity(bob, alice), tenant)));
Check("authentication required", () => Throws<ProfileLookupException>(() =>
    WorkIqService.ReadProfile(Result(Envelope(alice)), new ClaimsPrincipal(new ClaimsIdentity(user.Claims)), tenant)));
Check("missing object claim", () => Throws<ProfileLookupException>(() =>
    WorkIqService.ReadProfile(Result(Envelope(alice)),
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("tid", tenant)], "test")), tenant)));

using (MemoryCache cache = new(new MemoryCacheOptions()))
{
    SignInStore store = new(cache);
    Check("same-sender ticket and single use", () =>
    {
        string ticket = store.CreateLink(tenant, alice);
        Require(store.Complete(ticket, user) is null && store.GetUser(tenant, alice) == user);
        Require(store.Complete(ticket, user) is not null);
        store.RemoveUser(tenant, alice);
        Require(store.GetUser(tenant, alice) is null);
    });
    Check("reject different browser user", () =>
    {
        string ticket = store.CreateLink(tenant, alice);
        Require(store.Complete(ticket, Identity(tenant, bob)) is not null);
        Require(!store.HasLink(ticket) && store.GetUser(tenant, alice) is null);
    });
    Check("reject different browser tenant", () =>
    {
        string ticket = store.CreateLink(tenant, alice);
        Require(store.Complete(ticket, Identity(bob, alice)) is not null);
        Require(store.GetUser(tenant, alice) is null);
    });
    Check("users isolated", () =>
    {
        Require(store.Complete(store.CreateLink(tenant, alice), user) is null);
        Require(store.Complete(store.CreateLink(tenant, bob), Identity(tenant, bob)) is null);
        store.RemoveUser(tenant, alice);
        Require(store.GetUser(tenant, alice) is null && store.GetUser(tenant, bob) is not null);
    });
}

Check("WorkIQ auth configuration", () =>
{
    IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["WorkIQ:TenantId"] = tenant,
        ["WorkIQ:ClientId"] = bob,
        ["WorkIQ:Instance"] = "https://login.microsoftonline.com/",
        ["WorkIQ:CallbackPath"] = "/signin-workiq"
    }).Build();
    HostApplicationBuilder builder = new(new HostApplicationBuilderSettings { DisableDefaults = true });
    builder.Configuration.AddConfiguration(config);
    builder.Services.AddBrowserSignIn(builder.Configuration, new Uri("https://example.invalid"));
    using IHost container = builder.Build();
    OpenIdConnectOptions options = container.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
        .Get(BrowserSignIn.Scheme);
    Require(options.UsePkce && !options.SaveTokens && !options.MapInboundClaims);
    Require(options.Authority?.Contains(tenant, StringComparison.Ordinal) == true);
    Require(options.Scope.Contains(WorkIqService.Scopes.Single()));
    Require(!options.Scope.Contains("https://graph.microsoft.com/User.Read"));
    Require(options.CallbackPath == "/signin-workiq");
});
Check("WorkIQ tenant configuration", () =>
{
    IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["WorkIQ:TenantId"] = tenant }).Build();
    Require(new WorkIqSettings(config).TenantId == tenant);
});
Check("missing WorkIQ tenant rejected", () =>
    Throws<InvalidOperationException>(() => new WorkIqSettings(new ConfigurationBuilder().Build())));
Check("invalid WorkIQ tenant rejected", () =>
{
    IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["WorkIQ:TenantId"] = "not-a-tenant" }).Build();
    Throws<InvalidOperationException>(() => new WorkIqSettings(config));
});

{
    IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["WorkIQ:TenantId"] = tenant
    }).Build();
    WorkIqSettings settings = new(config);
    using MemoryCache cache = new(new MemoryCacheOptions());
    SignInStore store = new(cache);
    StubProfileService service = new();
    WorkIqAgentOrchestrator orchestrator = new(service, store, settings, new Uri("https://example.invalid"),
        NullLogger<WorkIqAgentOrchestrator>.Instance);
    MessageActivity activity = JsonSerializer.Deserialize<MessageActivity>($$$$"""
        {"type":"message","from":{"id":"test","aadObjectId":"{{{{alice}}}}"},"channelData":{"tenant":{"id":"{{{{tenant}}}}"}}}
        """, JsonSerializerOptions.Web)!;
    List<IAgentEvent> events = await Collect(orchestrator, activity);
    Check("WorkIQ unauthenticated card", () =>
        Require(events.Count == 1 && events[0].SignInUrl?.StartsWith("https://example.invalid/auth/signin?ticket=") == true &&
            events[0].Text == "Sign in to WorkIQ" && service.Calls == 0));
    Require(store.Complete(store.CreateLink(tenant, alice), user) is null);
    events = await Collect(orchestrator, activity);
    Check("WorkIQ profile streaming events", () =>
        Require(events[0].IsInformative && events[0].Text == "Fetching your profile from WorkIQ" &&
            string.Concat(events.Skip(1).Select(item => item.Text)) ==
                "Hi `Example`, WorkIQ returned your email as `example@invalid.test`. " &&
            service.Calls == 1 && service.User == user));
    service.Error = new HttpRequestException("synthetic", null, HttpStatusCode.Forbidden);
    events = await Collect(orchestrator, activity);
    Check("WorkIQ explicit failure without fallback", () =>
        Require(events.Count == 2 && events[1].Text.Contains("could not return", StringComparison.Ordinal) &&
            events[1].SignInUrl is null && service.Calls == 2));
    service.Error = new HttpRequestException("synthetic", null, HttpStatusCode.Unauthorized);
    events = await Collect(orchestrator, activity);
    Check("WorkIQ 401 requires new sign-in", () =>
        Require(events.Last().SignInUrl is not null && store.GetUser(tenant, alice) is null));
    Require(store.Complete(store.CreateLink(tenant, alice), user) is null);
    service.Error = new ProfileLookupException("synthetic identity mismatch");
    events = await Collect(orchestrator, activity);
    Check("WorkIQ mismatched profile never streamed", () =>
        Require(events.Count == 2 && events[1].Text.Contains("No profile was displayed", StringComparison.Ordinal) &&
            store.GetUser(tenant, alice) is null));
}

// This handler never opens a socket. Exercise the real MCP SDK wire protocol with synthetic identities.
using (ProfileMcpHandler handler = new())
using (HttpClient http = new(handler))
{
    Task<UserProfile> first = WorkIqService.FetchProfileAsync(http, alice, user, tenant, CancellationToken.None);
    Task<UserProfile> second = WorkIqService.FetchProfileAsync(http, bob, Identity(tenant, bob), tenant, CancellationToken.None);
    UserProfile[] profiles = await Task.WhenAll(first, second);
    Check("real MCP transport, isolated concurrent users", () =>
    {
        Require(profiles[0].Id == alice && profiles[1].Id == bob && handler.FetchCount == 2);
        Require(handler.Bearers.Contains(alice) && handler.Bearers.Contains(bob));
        Require(http.DefaultRequestHeaders.Authorization is null);
    });
}
Console.WriteLine($"Passed {checks} local checks. No live WorkIQ, sign-in, or Teams calls were made.");

void Check(string name, Action action)
{
    action();
    checks++;
    Console.WriteLine($"PASS {name}");
}
void Reject<T>(string name, CallToolResult result) where T : Exception =>
    Check(name, () => Throws<T>(() => WorkIqService.ReadProfile(result, user, tenant)));
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
static void Require(bool condition)
{
    if (!condition) throw new Exception("Assertion failed.");
}
static ClaimsPrincipal Identity(string tenantId, string objectId) =>
    new(new ClaimsIdentity([new Claim("tid", tenantId), new Claim("oid", objectId)], "test"));
static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);
static CallToolResult Result(string json) => new() { StructuredContent = Json(json) };
static string Envelope(string id) =>
    $$$"""{"results":[{"statusCode":200,"data":{"id":"{{{id}}}","displayName":"Example","mail":null,"userPrincipalName":"example@invalid.test"}}]}""";

static async Task<List<IAgentEvent>> Collect(IAgentOrchestrator orchestrator, MessageActivity activity)
{
    List<IAgentEvent> events = [];
    await foreach (IAgentEvent item in orchestrator.GetUpdatesAsync(activity, CancellationToken.None))
        events.Add(item);
    return events;
}

sealed class StubProfileService : IProfileService
{
    internal int Calls;
    internal Exception? Error;
    internal ClaimsPrincipal? User;

    public Task<UserProfile> GetMeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        Calls++;
        User = user;
        return Error is null
            ? Task.FromResult(new UserProfile("Example", null, "example@invalid.test"))
            : Task.FromException<UserProfile>(Error);
    }
}

sealed class ProfileMcpHandler : HttpMessageHandler
{
    internal ConcurrentBag<string> Bearers { get; } = [];
    internal int FetchCount;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsoluteUri != "https://workiq.svc.cloud.microsoft/mcp")
            throw new Exception("Unexpected endpoint or direct Graph fallback.");
        string token = request.Headers.Authorization?.Parameter ?? throw new Exception("Missing bearer.");
        Bearers.Add(token);
        if (request.Method != HttpMethod.Post)
            return new(HttpStatusCode.MethodNotAllowed);
        using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        JsonElement root = body.RootElement;
        string? method = root.GetProperty("method").GetString();
        if (!root.TryGetProperty("id", out JsonElement id))
            return new(HttpStatusCode.Accepted);
        if (method == "server/discover")
        {
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0", id, error = new { code = -32601, message = "Method not found" }
                }), System.Text.Encoding.UTF8, "application/json")
            };
        }
        object result;
        if (method == "initialize")
        {
            result = new
            {
                protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                capabilities = new { tools = new { } },
                serverInfo = new { name = "local-validation", version = "1.0" }
            };
        }
        else if (method == "tools/call")
        {
            JsonElement parameters = root.GetProperty("params");
            if (parameters.GetProperty("name").GetString() != "fetch" ||
                parameters.GetProperty("arguments").GetProperty("entityUrls").GetArrayLength() != 1 ||
                parameters.GetProperty("arguments").GetProperty("entityUrls")[0].GetString() != WorkIqService.ProfilePath)
                throw new Exception("Unexpected tool or profile path.");
            Interlocked.Increment(ref FetchCount);
            result = new
            {
                content = Array.Empty<object>(),
                structuredContent = new
                {
                    results = new[] { new { statusCode = 200, data = new { id = token, displayName = "Example", userPrincipalName = "example@invalid.test" } } }
                }
            };
        }
        else
        {
            throw new Exception($"Unexpected MCP method: {method}");
        }
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }),
                System.Text.Encoding.UTF8, "application/json")
        };
    }
}
