namespace VelvetChess.App;

internal static class ReleaseOwnerInfo
{
    public const string DeveloperName = "Телятников Илья Александрович";
    public const string SupportEmail = "ilel@list.ru";
    public const string WebsiteUrl = "https://github.com/Xsenus/VelvetChess";
    public const string PrivacyPolicyUrl = "https://xsenus.github.io/VelvetChess/privacy/";

    public static bool IsConfigured =>
        !DeveloperName.Contains("TODO", StringComparison.OrdinalIgnoreCase) &&
        !SupportEmail.Contains("TODO", StringComparison.OrdinalIgnoreCase) &&
        Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var website) && website.Scheme == Uri.UriSchemeHttps &&
        Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out var privacy) && privacy.Scheme == Uri.UriSchemeHttps;
}
