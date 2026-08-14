using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VelvetChess.Core.Online;
using VelvetChess.Server.Accounts;
using VelvetChess.Server.Auth;
using VelvetChess.Server.Data;
using Xunit;

namespace VelvetChess.Server.Tests;

public sealed class AccountApiTests : IClassFixture<AccountApiFactory>
{
    private readonly HttpClient _client;
    private readonly AccountApiFactory _factory;
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-safe";
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    public AccountApiTests(AccountApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task HealthAndPublicProviderConfigurationAreAvailable()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        var config = await _client.GetFromJsonAsync<ProviderConfiguration[]>("/v1/auth/config", Json);
        Assert.Contains(config!, x => x.Provider == IdentityProvider.Yandex && x.Configured && x.ClientId == "test-client");
    }

    [Fact]
    public async Task ExchangeCreatesStableAccountAndAuthenticatedProfile()
    {
        var first = await ExchangeAsync("same-user");
        var second = await ExchangeAsync("same-user");
        Assert.Equal(first.Profile.Profile.PlayerId, second.Profile.Profile.PlayerId);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileSnapshot>(Json);
        Assert.Equal("Тестовый игрок same-user", profile!.Profile.DisplayName);
        Assert.False(profile.Profile.IsGuest);
        Assert.Equal(IdentityProvider.Yandex, profile.Profile.Provider);
        Assert.Equal(3, profile.Ratings.Count);
    }

    [Fact]
    public async Task RefreshTokenIsRotatedAndCannotBeReused()
    {
        var session = await ExchangeAsync("refresh-user");
        using var firstResponse = await _client.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest(session.RefreshToken));
        firstResponse.EnsureSuccessStatusCode();
        var rotated = (await firstResponse.Content.ReadFromJsonAsync<SessionResponse>(Json))!;
        Assert.NotEqual(session.RefreshToken, rotated.RefreshToken);
        using var reuseResponse = await _client.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeMakesRefreshTokenUnusable()
    {
        var session = await ExchangeAsync("revoke-user");
        using var revoke = await _client.PostAsJsonAsync("/v1/auth/revoke", new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var refresh = await _client.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task GuestClaimIsIdempotentAndCannotBeClaimedByAnotherAccount()
    {
        var owner = await ExchangeAsync("owner");
        var solvedIds = Enumerable.Range(1, 12).Select(x => $"solved-{x}").ToArray();
        var claim = new ClaimGuestProgressRequest("guest-123456", "claim-1", new(8, 4, 1, 3, 12, 20), solvedIds);
        var first = await PostAuthorizedAsync<ClaimGuestProgressRequest, ProfileSnapshot>("/v1/profile/claim-guest-progress", owner.AccessToken, claim);
        var repeated = await PostAuthorizedAsync<ClaimGuestProgressRequest, ProfileSnapshot>("/v1/profile/claim-guest-progress", owner.AccessToken, claim);
        Assert.Equal(8, first.Progress.Games);
        Assert.Equal(first.Progress, repeated.Progress);

        var other = await ExchangeAsync("other");
        using var conflict = await PostAuthorizedAsync("/v1/profile/claim-guest-progress", other.AccessToken, claim);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task LocalRatingIsNeverPublishedToLeaderboard()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/v1/leaderboards/local")).StatusCode);
        var response = await _client.GetFromJsonAsync<LeaderboardItem[]>("/v1/leaderboards/puzzles?limit=10", Json);
        Assert.NotNull(response);
        Assert.All(response!, row => Assert.Equal(1000, row.Rating));
    }

    [Fact]
    public async Task InvalidPkceRequestIsRejectedBeforeProviderCall()
    {
        using var response = await _client.PostAsJsonAsync("/v1/auth/yandex/exchange", new ExchangeRequest("code", "velvetchess://auth", "short", null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfiguredWebOriginReceivesCorsHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://web.velvetchess.test");
        using var response = await _client.SendAsync(request);
        Assert.Equal("https://web.velvetchess.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task DeleteAccountRemovesServerDataAndRefreshSession()
    {
        var session = await ExchangeAsync("delete-user");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var refresh = await _client.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        Assert.False(await db.Players.AnyAsync(x => x.Id == session.Profile.Profile.PlayerId));
    }

    [Fact]
    public async Task VerifiedPuzzleUpdatesServerRatingOnlyOnce()
    {
        var session = await ExchangeAsync("puzzle-user");
        var result = new PuzzleResultRequest(2, ["e2e4", "e7e5"]);
        var first = await PostAuthorizedAsync<PuzzleResultRequest, ProfileSnapshot>("/v1/puzzles/test-puzzle/result", session.AccessToken, result);
        var repeated = await PostAuthorizedAsync<PuzzleResultRequest, ProfileSnapshot>("/v1/puzzles/test-puzzle/result", session.AccessToken, result);
        var rating = first.Ratings.Single(x => x.Kind == RatingKind.Puzzles);
        Assert.True(rating.Value > 1000);
        Assert.Equal(1, rating.Games);
        Assert.Equal(first.Progress, repeated.Progress);
        Assert.Equal(1, repeated.Progress.SolvedPuzzles);

        using var invalid = await PostAuthorizedAsync("/v1/puzzles/test-puzzle/result", session.AccessToken, new PuzzleResultRequest(1, ["a2a3"]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task SecondProviderCanBeLinkedToTheSamePlayer()
    {
        var yandex = await ExchangeAsync("linked-yandex");
        var exchange = new ExchangeRequest("linked-vk", "velvetchess://auth", Verifier, "device-1");
        var linked = await PostAuthorizedAsync<ExchangeRequest, ProfileSnapshot>("/v1/profile/link/vk/exchange", yandex.AccessToken, exchange);
        Assert.Equal(yandex.Profile.Profile.PlayerId, linked.Profile.PlayerId);

        using var vkResponse = await _client.PostAsJsonAsync("/v1/auth/vk/exchange", exchange);
        vkResponse.EnsureSuccessStatusCode();
        var vkSession = (await vkResponse.Content.ReadFromJsonAsync<SessionResponse>(Json))!;
        Assert.Equal(yandex.Profile.Profile.PlayerId, vkSession.Profile.Profile.PlayerId);
    }

    private async Task<SessionResponse> ExchangeAsync(string code)
    {
        using var response = await _client.PostAsJsonAsync("/v1/auth/yandex/exchange", new ExchangeRequest(code, "velvetchess://auth", Verifier, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(Json))!;
    }

    private async Task<TResponse> PostAuthorizedAsync<TRequest, TResponse>(string uri, string token, TRequest body)
    {
        using var response = await PostAuthorizedAsync(uri, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TResponse>(Json))!;
    }

    private async Task<HttpResponseMessage> PostAuthorizedAsync<TRequest>(string uri, string token, TRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record ProviderConfiguration(IdentityProvider Provider, bool Configured, string? ClientId, string AuthorizationEndpoint);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class AccountApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Session:SigningKey"] = "tests-only-signing-key-that-is-longer-than-thirty-two-bytes",
            ["Cors:AllowedOrigins:0"] = "https://web.velvetchess.test"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AccountDbContext>>();
            services.RemoveAll<AccountDbContext>();
            services.RemoveAll<IExternalIdentityProvider>();
            services.RemoveAll<IPuzzleCatalog>();
            services.AddDbContext<AccountDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IExternalIdentityProvider, FakeIdentityProvider>();
            services.AddScoped<IExternalIdentityProvider, FakeVkIdentityProvider>();
            services.AddSingleton<IPuzzleCatalog>(new FakePuzzleCatalog());
            services.AddCors(options => options.AddPolicy("clients", policy => policy.WithOrigins("https://web.velvetchess.test").AllowAnyHeader().AllowAnyMethod()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public sealed class FakeIdentityProvider : IExternalIdentityProvider
{
    public IdentityProvider Provider => IdentityProvider.Yandex;
    public ProviderOptions PublicOptions { get; } = new() { ClientId = "test-client", ClientSecret = "test-secret", AuthorizationEndpoint = "https://example.test/authorize" };
    public Task<ExternalIdentity> ExchangeAsync(OAuthExchange exchange, CancellationToken cancellationToken) =>
        Task.FromResult(new ExternalIdentity(exchange.Code, $"Тестовый игрок {exchange.Code}"));
}

public sealed class FakeVkIdentityProvider : IExternalIdentityProvider
{
    public IdentityProvider Provider => IdentityProvider.Vk;
    public ProviderOptions PublicOptions { get; } = new() { ClientId = "test-vk-client", ClientSecret = "test-vk-secret", AuthorizationEndpoint = "https://example.test/vk-authorize" };
    public Task<ExternalIdentity> ExchangeAsync(OAuthExchange exchange, CancellationToken cancellationToken) =>
        Task.FromResult(new ExternalIdentity(exchange.Code, $"VK игрок {exchange.Code}"));
}

public sealed class FakePuzzleCatalog : IPuzzleCatalog
{
    public RatedPuzzle? Find(string id) => id == "test-puzzle" ? new(id, 1400, ["e2e4", "e7e5"]) : id.StartsWith("solved-", StringComparison.Ordinal) ? new(id, 1200, ["a2a3"]) : null;
}
