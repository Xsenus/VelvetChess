[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[^\r\n`]{2,120}$')] [string]$DeveloperName,
    [Parameter(Mandatory)] [ValidatePattern('^[^\s@]+@[^\s@]+\.[^\s@]+$')] [string]$SupportEmail,
    [Parameter(Mandatory)] [uri]$WebsiteUrl,
    [Parameter(Mandatory)] [uri]$PrivacyPolicyUrl,
    [datetime]$EffectiveDate = (Get-Date)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8 = [Text.UTF8Encoding]::new($false)

function Assert-PublicHttps([uri]$Uri, [string]$Name) {
    if (-not $Uri.IsAbsoluteUri -or $Uri.Scheme -ne 'https' -or $Uri.UserInfo -or -not $Uri.DnsSafeHost -or $Uri.IsLoopback) {
        throw "$Name must be an absolute public HTTPS URL without embedded credentials."
    }
}

function Replace-Line([string]$Text, [string]$Pattern, [string]$Replacement) {
    $regex = [regex]::new($Pattern, [Text.RegularExpressions.RegexOptions]::Multiline)
    if ($regex.Matches($Text).Count -ne 1) { throw "Expected exactly one line matching: $Pattern" }
    return $regex.Replace($Text, { param($match) $Replacement }, 1)
}

function Escape-CSharp([string]$Value) {
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

Assert-PublicHttps $WebsiteUrl 'WebsiteUrl'
Assert-PublicHttps $PrivacyPolicyUrl 'PrivacyPolicyUrl'
$website = $WebsiteUrl.AbsoluteUri.TrimEnd('/')
$privacyUrl = $PrivacyPolicyUrl.AbsoluteUri
$dateText = $EffectiveDate.ToString('d MMMM yyyy', [Globalization.CultureInfo]::GetCultureInfo('ru-RU'))

Push-Location $repo
try {
    $listingPath = 'store\rustore\listing-ru.md'
    $listing = Get-Content -LiteralPath $listingPath -Raw
    if ($listing -notmatch '(?m)^- Разработчик:') {
        $listing = $listing.Replace('## Контакты (заполнить владельцу)', "## Контакты`n`n- Разработчик: ``TODO``")
    }
    $listing = Replace-Line $listing '^- Разработчик:.*$' "- Разработчик: ``$DeveloperName``"
    $listing = Replace-Line $listing '^- Email поддержки:.*$' "- Email поддержки: ``$SupportEmail``"
    $listing = Replace-Line $listing '^- Сайт:.*$' "- Сайт: ``$website``"
    $listing = Replace-Line $listing '^- Страница политики конфиденциальности:.*$' "- Страница политики конфиденциальности: ``$privacyUrl``"
    [IO.File]::WriteAllText((Join-Path $repo $listingPath), $listing, $utf8)

    $policyPath = 'store\rustore\privacy-policy.md'
    $policy = Get-Content -LiteralPath $policyPath -Raw
    $policy = Replace-Line $policy '^Дата вступления в силу:.*$' "Дата вступления в силу: $dateText года."
    if ($policy -notmatch '(?m)^Разработчик:') {
        $policy = $policy.Replace('По вопросам конфиденциальности:', "Разработчик: ``TODO``.`nСайт: ``https://TODO``.`n`nПо вопросам конфиденциальности:")
    }
    $policy = Replace-Line $policy '^Разработчик:.*$' "Разработчик: $DeveloperName."
    $policy = Replace-Line $policy '^Сайт:.*$' "Сайт: $website."
    $policy = Replace-Line $policy '^По вопросам конфиденциальности:.*$' "По вопросам конфиденциальности: $SupportEmail."
    [IO.File]::WriteAllText((Join-Path $repo $policyPath), $policy, $utf8)

    $template = Get-Content 'store\rustore\privacy-site\index.template.html' -Raw
    $html = $template.Replace('{{DEVELOPER_NAME}}', [Net.WebUtility]::HtmlEncode($DeveloperName))
    $html = $html.Replace('{{SUPPORT_EMAIL}}', [Net.WebUtility]::HtmlEncode($SupportEmail))
    $html = $html.Replace('{{WEBSITE_URL}}', [Net.WebUtility]::HtmlEncode($website))
    $html = $html.Replace('{{PRIVACY_POLICY_URL}}', [Net.WebUtility]::HtmlEncode($privacyUrl))
    $html = $html.Replace('{{EFFECTIVE_DATE}}', [Net.WebUtility]::HtmlEncode($dateText))
    if ($html -match '\{\{[^}]+\}\}') { throw 'Not all privacy-site template tokens were replaced.' }
    [IO.File]::WriteAllText((Join-Path $repo 'store\rustore\privacy-site\index.html'), $html, $utf8)

    $ownerInfoPath = 'src\VelvetChess.App\ReleaseOwnerInfo.cs'
    $ownerInfo = Get-Content -LiteralPath $ownerInfoPath -Raw
    $ownerInfo = Replace-Line $ownerInfo '^\s*public const string DeveloperName =.*$' ('    public const string DeveloperName = "{0}";' -f (Escape-CSharp $DeveloperName))
    $ownerInfo = Replace-Line $ownerInfo '^\s*public const string SupportEmail =.*$' ('    public const string SupportEmail = "{0}";' -f (Escape-CSharp $SupportEmail))
    $ownerInfo = Replace-Line $ownerInfo '^\s*public const string WebsiteUrl =.*$' ('    public const string WebsiteUrl = "{0}";' -f (Escape-CSharp $website))
    $ownerInfo = Replace-Line $ownerInfo '^\s*public const string PrivacyPolicyUrl =.*$' ('    public const string PrivacyPolicyUrl = "{0}";' -f (Escape-CSharp $privacyUrl))
    [IO.File]::WriteAllText((Join-Path $repo $ownerInfoPath), $ownerInfo, $utf8)

    & (Join-Path $PSScriptRoot 'Test-RuStoreReadiness.ps1') -SkipTests
    if (-not $?) { throw 'Owner data was written, but RuStore preflight failed.' }
    Write-Host 'Owner data and privacy site updated successfully.' -ForegroundColor Green
} finally {
    Pop-Location
}
