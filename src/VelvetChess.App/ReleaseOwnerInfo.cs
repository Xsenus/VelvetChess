namespace VelvetChess.App;

internal static class ReleaseOwnerInfo
{
    public const string DeveloperName = "TODO";
    public const string SupportEmail = "TODO@example.com";
    public const string WebsiteUrl = "https://TODO";
    public const string PrivacyPolicyUrl = "https://TODO/privacy/";

    public static bool IsConfigured =>
        !DeveloperName.Contains("TODO", StringComparison.OrdinalIgnoreCase) &&
        !SupportEmail.Contains("TODO", StringComparison.OrdinalIgnoreCase) &&
        Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var website) && website.Scheme == Uri.UriSchemeHttps &&
        Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out var privacy) && privacy.Scheme == Uri.UriSchemeHttps;
}
