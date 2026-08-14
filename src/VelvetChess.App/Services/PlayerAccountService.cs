using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VelvetChess.Core.Online;

namespace VelvetChess.App.Services;

public sealed class PlayerAccountService : IPlayerAccountService
{
    private const string GuestIdKey = "profile.guestId.v1";
    private const string SessionKey = "profile.session.v1";
    private const string CallbackUri = "velvetchess://auth";
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly PlayerProfile _guest;
    private PlayerProfile? _current;
    private SessionEnvelope? _session;

    public PlayerAccountService()
    {
        _guest = LoadGuest();
        _current = _guest;
    }

    public PlayerProfile Current => _current ?? _guest;
    public ProfileSnapshot? ServerSnapshot => _session?.Profile;
    public bool ExternalProvidersConfigured => TryGetApiBaseUri(out _);

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetApiBaseUri(out var api)) return;
        try
        {
            var serialized = await SecureStorage.Default.GetAsync(SessionKey);
            if (string.IsNullOrWhiteSpace(serialized)) return;
            _session = JsonSerializer.Deserialize<SessionEnvelope>(serialized, Json);
            if (_session is null) return;
            _current = _session.Profile.Profile;
            if (_session.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                try { _session = await RefreshAsync(api, _session.RefreshToken, cancellationToken); }
                catch (HttpRequestException) { return; }
            }
            _current = _session.Profile.Profile;
        }
        catch (Exception) { await ClearSessionAsync(); }
    }

    public async Task<PlayerProfile> SignInAsync(IdentityProvider provider, CancellationToken cancellationToken = default)
    {
        if (provider == IdentityProvider.Guest) return Current;
        if (!TryGetApiBaseUri(out var api)) throw new InvalidOperationException("Сервер аккаунтов ещё не указан в сборке приложения.");
        ProviderConfiguration[] configurations;
        try { configurations = await _http.GetFromJsonAsync<ProviderConfiguration[]>(new Uri(api, "v1/auth/config"), Json, cancellationToken) ?? throw new InvalidOperationException("Сервер не вернул конфигурацию авторизации."); }
        catch (HttpRequestException) { throw new InvalidOperationException("Сервер профилей недоступен. Проверьте интернет и попробуйте снова."); }
        var config = configurations.SingleOrDefault(x => x.Provider == provider);
        if (config is null || !config.Configured || string.IsNullOrWhiteSpace(config.ClientId)) throw new InvalidOperationException($"{ProviderName(provider)} ещё не настроен на сервере.");

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var authorization = BuildAuthorizationUri(config, provider, challenge, state);
        WebAuthenticatorResult callback;
        try { callback = await WebAuthenticator.Default.AuthenticateAsync(authorization, new Uri(CallbackUri)); }
        catch (TaskCanceledException) { throw new InvalidOperationException("Вход отменён."); }
        catch (Exception exception) { throw new InvalidOperationException($"Не удалось открыть вход: {exception.Message}"); }
        if (!callback.Properties.TryGetValue("state", out var returnedState) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(returnedState)))
            throw new InvalidOperationException("Проверка OAuth state не пройдена.");
        if (!callback.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Провайдер не вернул код авторизации.");
        callback.Properties.TryGetValue("device_id", out var deviceId);
        var linking = !Current.IsGuest && _session is not null;
        var endpoint = linking ? $"v1/profile/link/{provider.ToString().ToLowerInvariant()}/exchange" : $"v1/auth/{provider.ToString().ToLowerInvariant()}/exchange";
        HttpResponseMessage response;
        try
        {
            using var exchangeRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(api, endpoint)) { Content = JsonContent.Create(new ExchangeRequest(code, CallbackUri, verifier, deviceId), options: Json) };
            if (linking) exchangeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session!.AccessToken);
            response = await _http.SendAsync(exchangeRequest, cancellationToken);
        }
        catch (HttpRequestException) { throw new InvalidOperationException("Не удалось завершить вход: сервер профилей недоступен."); }
        using (response)
        {
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadServerErrorAsync(response, cancellationToken));
        if (linking) _session = _session! with { Profile = (await response.Content.ReadFromJsonAsync<ProfileSnapshot>(Json, cancellationToken))! };
        else _session = await response.Content.ReadFromJsonAsync<SessionEnvelope>(Json, cancellationToken) ?? throw new InvalidOperationException("Сервер не создал сессию.");
        }
        _current = _session.Profile.Profile;
        await SaveSessionAsync();
        return Current;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (_session is not null && TryGetApiBaseUri(out var api))
        {
            try { using var response = await _http.PostAsJsonAsync(new Uri(api, "v1/auth/revoke"), new RefreshRequest(_session.RefreshToken), Json, cancellationToken); }
            catch (Exception) { /* Local sign-out must also work offline. */ }
        }
        await ClearSessionAsync();
        _session = null;
        _current = _guest;
    }

    public async Task DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        if (Current.IsGuest || _session is null) return;
        if (!TryGetApiBaseUri(out var api)) throw new InvalidOperationException("Сервер аккаунтов не настроен.");
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(api, "v1/profile"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadServerErrorAsync(response, cancellationToken));
        await SignOutAsync(cancellationToken);
    }

    public async Task SyncProgressAsync(ProfileSnapshot localSnapshot, CancellationToken cancellationToken = default)
    {
        if (Current.IsGuest || _session is null) throw new InvalidOperationException("Сначала войдите в аккаунт.");
        if (!TryGetApiBaseUri(out var api)) throw new InvalidOperationException("Сервер аккаунтов не настроен.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api, "v1/profile/claim-guest-progress"))
        {
            Content = JsonContent.Create(new ClaimRequest(_guest.PlayerId, $"claim-{_guest.PlayerId}", localSnapshot.Progress, localSnapshot.SolvedPuzzleIds), options: Json)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _session = await RefreshAsync(api, _session.RefreshToken, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
            using var retry = new HttpRequestMessage(HttpMethod.Post, new Uri(api, "v1/profile/claim-guest-progress"))
            {
                Content = JsonContent.Create(new ClaimRequest(_guest.PlayerId, $"claim-{_guest.PlayerId}", localSnapshot.Progress, localSnapshot.SolvedPuzzleIds), options: Json)
            };
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
            using var retryResponse = await _http.SendAsync(retry, cancellationToken);
            if (!retryResponse.IsSuccessStatusCode) throw new InvalidOperationException(await ReadServerErrorAsync(retryResponse, cancellationToken));
            _session = _session with { Profile = (await retryResponse.Content.ReadFromJsonAsync<ProfileSnapshot>(Json, cancellationToken))! };
        }
        else
        {
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadServerErrorAsync(response, cancellationToken));
            _session = _session with { Profile = (await response.Content.ReadFromJsonAsync<ProfileSnapshot>(Json, cancellationToken))! };
        }
        _current = _session.Profile.Profile;
        await SaveSessionAsync();
    }

    public async Task RecordPuzzleResultAsync(string puzzleId, int attempts, IReadOnlyList<string> moves, CancellationToken cancellationToken = default)
    {
        if (Current.IsGuest || _session is null || !TryGetApiBaseUri(out var api)) return;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api, $"v1/puzzles/{Uri.EscapeDataString(puzzleId)}/result"))
        {
            Content = JsonContent.Create(new PuzzleResultRequest(Math.Max(1, attempts), moves), options: Json)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return;
        _session = _session with { Profile = (await response.Content.ReadFromJsonAsync<ProfileSnapshot>(Json, cancellationToken))! };
        await SaveSessionAsync();
    }

    private async Task<SessionEnvelope> RefreshAsync(Uri api, string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(new Uri(api, "v1/auth/refresh"), new RefreshRequest(refreshToken), Json, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Сессия истекла. Войдите снова.");
        var session = await response.Content.ReadFromJsonAsync<SessionEnvelope>(Json, cancellationToken) ?? throw new InvalidOperationException("Сервер не обновил сессию.");
        _session = session;
        await SaveSessionAsync();
        return session;
    }

    private static Uri BuildAuthorizationUri(ProviderConfiguration config, IdentityProvider provider, string challenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = config.ClientId!, ["redirect_uri"] = CallbackUri,
            ["state"] = state, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256"
        };
        if (provider == IdentityProvider.Yandex) parameters["scope"] = "login:info";
        var query = string.Join('&', parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return new Uri($"{config.AuthorizationEndpoint}?{query}");
    }

    private static bool TryGetApiBaseUri(out Uri uri)
    {
        var configured = typeof(PlayerAccountService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "AccountApiBaseUrl")?.Value;
        return Uri.TryCreate(configured, UriKind.Absolute, out uri!);
    }

    private async Task SaveSessionAsync()
    {
        if (_session is not null) await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(_session, Json));
    }

    private static async Task ClearSessionAsync()
    {
        try { SecureStorage.Default.Remove(SessionKey); await Task.CompletedTask; } catch (Exception) { }
    }

    private static async Task<string> ReadServerErrorAsync(HttpResponseMessage response, CancellationToken token)
    {
        var body = await response.Content.ReadAsStringAsync(token);
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("detail", out var detail)) return detail.GetString() ?? "Ошибка авторизации.";
            if (json.RootElement.TryGetProperty("error", out var error)) return error.GetString() ?? "Ошибка авторизации.";
        }
        catch (JsonException) { }
        return $"Сервер вернул ошибку {(int)response.StatusCode}.";
    }

    private static PlayerProfile LoadGuest()
    {
        var id = Preferences.Default.Get(GuestIdKey, "");
        if (string.IsNullOrWhiteSpace(id)) { id = $"guest-{Guid.NewGuid():N}"; Preferences.Default.Set(GuestIdKey, id); }
        return new(id, "Гость", IdentityProvider.Guest, true, DateTimeOffset.UtcNow);
    }

    private static string ProviderName(IdentityProvider provider) => provider == IdentityProvider.Yandex ? "Яндекс ID" : "VK ID";
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static JsonSerializerOptions CreateJsonOptions() { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); options.Converters.Add(new JsonStringEnumConverter()); return options; }

    private sealed record ProviderConfiguration(IdentityProvider Provider, bool Configured, string? ClientId, string AuthorizationEndpoint);
    private sealed record ExchangeRequest(string Code, string RedirectUri, string CodeVerifier, string? DeviceId);
    private sealed record RefreshRequest(string RefreshToken);
    private sealed record ClaimRequest(string GuestId, string IdempotencyKey, PlayerProgress Progress, IReadOnlyList<string>? SolvedPuzzleIds);
    private sealed record SessionEnvelope(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, ProfileSnapshot Profile);
    private sealed record PuzzleResultRequest(int Attempts, IReadOnlyList<string> Moves);
}
