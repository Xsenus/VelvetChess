using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VelvetChess.Core.Online;

namespace VelvetChess.Server.Auth;

public sealed class OAuthOptions
{
    public ProviderOptions Yandex { get; set; } = new();
    public ProviderOptions Vk { get; set; } = new();
}

public sealed class ProviderOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AuthorizationEndpoint { get; set; } = "";
    public string TokenEndpoint { get; set; } = "";
    public string UserInfoEndpoint { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed record ExternalIdentity(string ProviderUserId, string DisplayName);
public sealed record OAuthExchange(string Code, string RedirectUri, string CodeVerifier, string? DeviceId);

public interface IExternalIdentityProvider
{
    IdentityProvider Provider { get; }
    ProviderOptions PublicOptions { get; }
    Task<ExternalIdentity> ExchangeAsync(OAuthExchange exchange, CancellationToken cancellationToken);
}

public abstract class OAuthProviderBase(IHttpClientFactory clients) : IExternalIdentityProvider
{
    protected HttpClient Client => clients.CreateClient("oauth");
    public abstract IdentityProvider Provider { get; }
    public abstract ProviderOptions PublicOptions { get; }
    public abstract Task<ExternalIdentity> ExchangeAsync(OAuthExchange exchange, CancellationToken cancellationToken);

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new OAuthExchangeException($"Провайдер отклонил обмен кода ({(int)response.StatusCode}).");
        try { return JsonDocument.Parse(body); }
        catch (JsonException) { throw new OAuthExchangeException("Провайдер вернул некорректный ответ."); }
    }
}

public sealed class YandexOAuthProvider(IHttpClientFactory clients, IOptions<OAuthOptions> options) : OAuthProviderBase(clients)
{
    private readonly ProviderOptions _options = options.Value.Yandex;
    public override IdentityProvider Provider => IdentityProvider.Yandex;
    public override ProviderOptions PublicOptions => _options;

    public override async Task<ExternalIdentity> ExchangeAsync(OAuthExchange exchange, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var tokenResponse = await Client.PostAsync(_options.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = exchange.Code, ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret, ["redirect_uri"] = exchange.RedirectUri, ["code_verifier"] = exchange.CodeVerifier
        }), cancellationToken);
        using var tokenJson = await ReadJsonAsync(tokenResponse, cancellationToken);
        var token = tokenJson.RootElement.GetProperty("access_token").GetString() ?? throw new OAuthExchangeException("Яндекс не вернул access token.");
        using var infoRequest = new HttpRequestMessage(HttpMethod.Get, _options.UserInfoEndpoint);
        infoRequest.Headers.Authorization = new AuthenticationHeaderValue("OAuth", token);
        using var infoResponse = await Client.SendAsync(infoRequest, cancellationToken);
        using var info = await ReadJsonAsync(infoResponse, cancellationToken);
        var id = info.RootElement.GetProperty("id").GetString() ?? throw new OAuthExchangeException("Яндекс не вернул идентификатор пользователя.");
        var name = ReadString(info.RootElement, "display_name") ?? ReadString(info.RootElement, "login") ?? "Игрок Яндекс";
        return new(id, name);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured) throw new OAuthNotConfiguredException("Яндекс OAuth не настроен на сервере.");
    }

    private static string? ReadString(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
}

public sealed class VkOAuthProvider(IHttpClientFactory clients, IOptions<OAuthOptions> options) : OAuthProviderBase(clients)
{
    private readonly ProviderOptions _options = options.Value.Vk;
    public override IdentityProvider Provider => IdentityProvider.Vk;
    public override ProviderOptions PublicOptions => _options;

    public override async Task<ExternalIdentity> ExchangeAsync(OAuthExchange exchange, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured) throw new OAuthNotConfiguredException("VK ID не настроен на сервере.");
        if (string.IsNullOrWhiteSpace(exchange.DeviceId)) throw new OAuthExchangeException("VK ID требует device_id из callback.");
        using var tokenResponse = await Client.PostAsync(_options.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = exchange.Code, ["code_verifier"] = exchange.CodeVerifier,
            ["client_id"] = _options.ClientId, ["client_secret"] = _options.ClientSecret, ["redirect_uri"] = exchange.RedirectUri,
            ["device_id"] = exchange.DeviceId
        }), cancellationToken);
        using var tokenJson = await ReadJsonAsync(tokenResponse, cancellationToken);
        var token = tokenJson.RootElement.GetProperty("access_token").GetString() ?? throw new OAuthExchangeException("VK ID не вернул access token.");
        using var infoResponse = await Client.PostAsync(_options.UserInfoEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["access_token"] = token, ["client_id"] = _options.ClientId
        }), cancellationToken);
        using var info = await ReadJsonAsync(infoResponse, cancellationToken);
        var user = info.RootElement.TryGetProperty("user", out var nested) ? nested : info.RootElement;
        var id = user.TryGetProperty("user_id", out var userId) ? userId.ToString() : null;
        if (string.IsNullOrWhiteSpace(id)) throw new OAuthExchangeException("VK ID не вернул идентификатор пользователя.");
        var first = user.TryGetProperty("first_name", out var firstName) ? firstName.GetString() : null;
        var last = user.TryGetProperty("last_name", out var lastName) ? lastName.GetString() : null;
        return new(id, string.Join(' ', new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } name ? name : "Игрок VK");
    }
}

public sealed class OAuthNotConfiguredException(string message) : Exception(message);
public sealed class OAuthExchangeException(string message) : Exception(message);
