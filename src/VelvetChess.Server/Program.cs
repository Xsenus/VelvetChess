using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using VelvetChess.Core.Online;
using VelvetChess.Server.Accounts;
using VelvetChess.Server.Auth;
using VelvetChess.Server.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("OAuth"));
builder.Services.AddHttpClient("oauth", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddDbContext<AccountDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("Accounts")));
builder.Services.AddSingleton<SessionKey>();
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddScoped<IExternalIdentityProvider, YandexOAuthProvider>();
builder.Services.AddScoped<IExternalIdentityProvider, VkOAuthProvider>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddSingleton<IPuzzleCatalog, JsonPuzzleCatalog>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<SessionKey>((options, key) =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Session:Issuer"] ?? "VelvetChess",
        ValidateAudience = true, ValidAudience = builder.Configuration["Session:Audience"] ?? "VelvetChess.Clients",
        ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key.SecurityKey,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("clients", policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options => options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
    })));

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseExceptionHandler();
app.UseRouting();
app.UseCors("clients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AccountDbContext>().Database.MigrateAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/auth/config", (IEnumerable<IExternalIdentityProvider> providers) => Results.Ok(providers.Select(x => new
{
    provider = x.Provider, configured = x.PublicOptions.IsConfigured, clientId = x.PublicOptions.IsConfigured ? x.PublicOptions.ClientId : null,
    authorizationEndpoint = x.PublicOptions.AuthorizationEndpoint
})));
app.MapPost("/v1/auth/{provider}/exchange", async (string provider, ExchangeRequest request, AccountService accounts, CancellationToken token) =>
{
    if (!Enum.TryParse<IdentityProvider>(provider, true, out var parsed) || parsed == IdentityProvider.Guest) return Results.BadRequest(new { error = "unsupported_provider" });
    try { return Results.Ok(await accounts.ExchangeAsync(parsed, request, token)); }
    catch (OAuthNotConfiguredException exception) { return Results.Problem(exception.Message, statusCode: 503); }
    catch (OAuthExchangeException exception) { return Results.Problem(exception.Message, statusCode: 401); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).RequireRateLimiting("auth");
app.MapPost("/v1/auth/refresh", async (RefreshRequest request, AccountService accounts, CancellationToken token) =>
{
    try { return Results.Ok(await accounts.RefreshAsync(request.RefreshToken, token)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Json(new { error = exception.Message }, statusCode: 401); }
}).RequireRateLimiting("auth");
app.MapPost("/v1/auth/revoke", async (RefreshRequest request, AccountService accounts, CancellationToken token) =>
{
    await accounts.RevokeAsync(request.RefreshToken, token);
    return Results.NoContent();
}).RequireRateLimiting("auth");
app.MapPost("/v1/profile/link/{provider}/exchange", async (string provider, ClaimsPrincipal user, ExchangeRequest request, AccountService accounts, CancellationToken token) =>
{
    if (!Enum.TryParse<IdentityProvider>(provider, true, out var parsed) || parsed == IdentityProvider.Guest) return Results.BadRequest(new { error = "unsupported_provider" });
    try { return Results.Ok(await accounts.LinkExternalAsync(RequirePlayerId(user), parsed, request, token)); }
    catch (OAuthNotConfiguredException exception) { return Results.Problem(exception.Message, statusCode: 503); }
    catch (OAuthExchangeException exception) { return Results.Problem(exception.Message, statusCode: 401); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
}).RequireAuthorization().RequireRateLimiting("auth");
app.MapGet("/v1/profile", async (ClaimsPrincipal user, AccountService accounts, CancellationToken token) =>
    Results.Ok(await accounts.GetProfileAsync(RequirePlayerId(user), IdentityProvider.Guest, token))).RequireAuthorization();
app.MapPost("/v1/profile/claim-guest-progress", async (ClaimsPrincipal user, ClaimGuestProgressRequest request, AccountService accounts, CancellationToken token) =>
{
    try { return Results.Ok(await accounts.ClaimGuestAsync(RequirePlayerId(user), request, token)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
}).RequireAuthorization();
app.MapDelete("/v1/profile", async (ClaimsPrincipal user, AccountService accounts, CancellationToken token) =>
{
    await accounts.DeleteAccountAsync(RequirePlayerId(user), token);
    return Results.NoContent();
}).RequireAuthorization();
app.MapPost("/v1/puzzles/{id}/result", async (string id, ClaimsPrincipal user, PuzzleResultRequest request, IPuzzleCatalog catalog, AccountService accounts, CancellationToken token) =>
{
    var puzzle = catalog.Find(id);
    if (puzzle is null) return Results.NotFound(new { error = "puzzle_not_found" });
    try { return Results.Ok(await accounts.RecordPuzzleResultAsync(RequirePlayerId(user), puzzle, request, token)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).RequireAuthorization();
app.MapGet("/v1/leaderboards/{kind}", async (string kind, int? limit, AccountService accounts, CancellationToken token) =>
{
    if (!Enum.TryParse<RatingKind>(kind, true, out var parsed) || parsed == RatingKind.Local) return Results.BadRequest(new { error = "unsupported_rating_pool" });
    return Results.Ok(await accounts.LeaderboardAsync(parsed, limit ?? 50, token));
});
app.Run();

static string RequirePlayerId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
public partial class Program;
