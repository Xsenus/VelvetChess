using VelvetChess.Core.Online;

namespace VelvetChess.App.Services;

public sealed class PlayerAccountService : IPlayerAccountService
{
    private const string GuestIdKey = "profile.guestId.v1";
    private PlayerProfile? _current;

    public PlayerProfile Current => _current ??= LoadGuest();
    public bool ExternalProvidersConfigured => false;

    public Task<PlayerProfile> SignInAsync(IdentityProvider provider, CancellationToken cancellationToken = default)
    {
        if (provider == IdentityProvider.Guest) return Task.FromResult(Current);
        throw new InvalidOperationException("Для входа необходимо зарегистрировать OAuth-приложение и подключить серверный обмен токенов.");
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        _current = LoadGuest();
        return Task.CompletedTask;
    }

    public Task SyncProgressAsync(ProfileSnapshot localSnapshot, CancellationToken cancellationToken = default)
    {
        if (Current.IsGuest) throw new InvalidOperationException("Гостевой профиль не синхронизируется с сервером.");
        throw new InvalidOperationException("Сервер рейтинга ещё не подключён.");
    }

    private static PlayerProfile LoadGuest()
    {
        var id = Preferences.Default.Get(GuestIdKey, "");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"guest-{Guid.NewGuid():N}";
            Preferences.Default.Set(GuestIdKey, id);
        }
        return new(id, "Гость", IdentityProvider.Guest, true, DateTimeOffset.UtcNow);
    }
}
