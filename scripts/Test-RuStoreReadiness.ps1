[CmdletBinding()]
param(
    [string]$PackagePath,
    [switch]$AllowOwnerPlaceholders,
    [switch]$AllowDebugCertificate,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = [Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message) {
    $script:failures.Add($Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Add-Pass([string]$Message) {
    Write-Host "[ OK ] $Message" -ForegroundColor Green
}

function Get-PngDimensions([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) {
        throw "Not a valid PNG file: $Path"
    }
    $width = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $height = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    return @($width, $height)
}

function Find-AndroidTool([string]$Name) {
    $sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $null }
    if (-not $sdk -or -not (Test-Path -LiteralPath $sdk -PathType Container)) { return $null }
    $buildTools = Get-ChildItem -LiteralPath (Join-Path $sdk 'build-tools') -Directory -ErrorAction SilentlyContinue |
        Sort-Object { try { [version]$_.Name } catch { [version]'0.0' } } -Descending |
        Select-Object -First 1
    if (-not $buildTools) { return $null }
    $candidate = Join-Path $buildTools.FullName $Name
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    return $null
}

function Find-JavaExecutable {
    if ($env:JAVA_HOME) {
        $candidate = Join-Path $env:JAVA_HOME 'bin\java.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $command = Get-Command java -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    return $null
}

function Find-BundleTool {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCommand) { return $null }
    $dotnetRoot = Split-Path -Parent $dotnetCommand.Source
    $packs = Join-Path $dotnetRoot 'packs'
    if (-not (Test-Path -LiteralPath $packs -PathType Container)) { return $null }
    return Get-ChildItem $packs -Recurse -File -Filter 'bundletool.jar' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'Microsoft\.Android\.Sdk' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

function Test-ManifestPermissions([string]$ManifestText, [string]$ApplicationId) {
    $permissions = [regex]::Matches($ManifestText, 'uses-permission(?:[^>]|\n)*?(?:android:)?name=[''"]([^''"]+)[''"]') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
    $unexpected = @($permissions | Where-Object { $_ -ne "$ApplicationId.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION" })
    if ($unexpected.Count) {
        Add-Failure "Unexpected Android permission(s): $($unexpected -join ', ')."
    } else {
        Add-Pass 'No sensitive Android permissions are declared.'
    }
}

Push-Location $repo
try {
    [xml]$project = Get-Content 'src\VelvetChess.App\VelvetChess.App.csproj' -Raw
    $appId = [string]($project.Project.PropertyGroup.ApplicationId | Select-Object -First 1)
    $versionName = [string]($project.Project.PropertyGroup.ApplicationDisplayVersion | Select-Object -First 1)
    $versionCode = [string]($project.Project.PropertyGroup.ApplicationVersion | Select-Object -First 1)
    $numericVersionCode = 0
    $validVersionCode = [int]::TryParse($versionCode, [ref]$numericVersionCode) -and $numericVersionCode -gt 0
    if ($appId -eq 'ru.velvetchess.game' -and $versionName -match '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$' -and $validVersionCode) {
        Add-Pass "Project identity: $appId $versionName ($versionCode)"
    } else {
        Add-Failure "Unexpected project identity: '$appId' '$versionName' ('$versionCode')."
    }

    $ownerContentFiles = @('store\rustore\listing-ru.md', 'store\rustore\privacy-policy.md', 'store\rustore\privacy-site\index.html')
    $placeholderHits = @(Select-String -Path $ownerContentFiles -Pattern 'TODO|example\.com' -CaseSensitive:$false)
    $placeholderHits += @(Select-String -Path 'src\VelvetChess.App\ReleaseOwnerInfo.cs' -Pattern '^\s*public const string \w+\s*=\s*"[^"]*(?:TODO|example\.com)' -CaseSensitive:$false)
    if ($placeholderHits) {
        if ($AllowOwnerPlaceholders) {
            Write-Host '[WARN] Owner contacts still contain placeholders.' -ForegroundColor Yellow
        } else {
            Add-Failure 'Replace owner contact placeholders in the RuStore listing and privacy policy.'
        }
    } else {
        $listingText = Get-Content 'store\rustore\listing-ru.md' -Raw
        $developerMatch = [regex]::Match($listingText, '(?m)^- Разработчик: `([^`]+)`\r?$')
        $emailMatch = [regex]::Match($listingText, '(?m)^- Email поддержки: `([^`]+)`\r?$')
        $siteMatch = [regex]::Match($listingText, '(?m)^- Сайт: `(https://[^`]+)`\r?$')
        $privacyMatch = [regex]::Match($listingText, '(?m)^- Страница политики конфиденциальности: `(https://[^`]+)`\r?$')
        if (-not $developerMatch.Success -or -not $emailMatch.Success -or $emailMatch.Groups[1].Value -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$' -or -not $siteMatch.Success -or -not $privacyMatch.Success) {
            Add-Failure 'Owner email, website or privacy-policy URL is malformed.'
        } else {
            $privacyHtml = Get-Content 'store\rustore\privacy-site\index.html' -Raw
            $ownerInfo = Get-Content 'src\VelvetChess.App\ReleaseOwnerInfo.cs' -Raw
            $encodedDeveloper = [Net.WebUtility]::HtmlEncode($developerMatch.Groups[1].Value)
            $encodedEmail = [Net.WebUtility]::HtmlEncode($emailMatch.Groups[1].Value)
            $encodedPrivacy = [Net.WebUtility]::HtmlEncode($privacyMatch.Groups[1].Value)
            if ($privacyHtml -notmatch [regex]::Escape($encodedDeveloper) -or $privacyHtml -notmatch [regex]::Escape($encodedEmail) -or $privacyHtml -notmatch [regex]::Escape($encodedPrivacy) -or
                $ownerInfo -notmatch [regex]::Escape($developerMatch.Groups[1].Value) -or $ownerInfo -notmatch [regex]::Escape($emailMatch.Groups[1].Value) -or $ownerInfo -notmatch [regex]::Escape($privacyMatch.Groups[1].Value)) {
                Add-Failure 'Privacy-site or in-app contacts do not match the RuStore listing.'
            } else { Add-Pass 'Owner contacts and privacy site are filled in and synchronized.' }
        }
    }

    $iconPath = 'store\rustore\graphics\app_icon_512.png'
    try {
        $iconSize = Get-PngDimensions $iconPath
        $iconLength = (Get-Item -LiteralPath $iconPath).Length
        if ($iconSize[0] -eq 512 -and $iconSize[1] -eq 512 -and $iconLength -le 3MB) {
            Add-Pass "Store icon: 512x512, $iconLength bytes."
        } else {
            Add-Failure "Store icon must be 512x512 PNG and at most 3 MiB."
        }
    } catch { Add-Failure $_.Exception.Message }

    $screenshots = @(Get-ChildItem 'store\rustore\screenshots' -File -Filter '*.png' | Sort-Object Name)
    if ($screenshots.Count -lt 1 -or $screenshots.Count -gt 10) {
        Add-Failure "RuStore requires 1-10 screenshots; found $($screenshots.Count)."
    } else {
        $screenshotsValid = $true
        foreach ($shot in $screenshots) {
            try {
                $size = Get-PngDimensions $shot.FullName
                if ($size[0] * 16 -ne $size[1] * 9 -or $size[0] -gt 2160 -or $size[1] -gt 3840 -or $shot.Length -gt 3MB) {
                    $screenshotsValid = $false
                    Add-Failure "$($shot.Name) must be 9:16, no larger than 2160x3840 and at most 3 MiB."
                }
            } catch { $screenshotsValid = $false; Add-Failure $_.Exception.Message }
        }
        if ($screenshotsValid) { Add-Pass "$($screenshots.Count) screenshots satisfy size and aspect-ratio checks." }
    }

    if (-not $SkipTests) {
        & dotnet test 'tests\VelvetChess.Core.Tests\VelvetChess.Core.Tests.csproj' -c Release
        if ($LASTEXITCODE -eq 0) { Add-Pass 'Core test suite passed.' } else { Add-Failure 'Core test suite failed.' }
    }

    if ($PackagePath) {
        $resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
        $extension = [IO.Path]::GetExtension($resolvedPackage).ToLowerInvariant()
        if ($extension -eq '.apk') {
            $aapt = Find-AndroidTool 'aapt.exe'
            $apksigner = Find-AndroidTool 'apksigner.bat'
            if (-not $aapt -or -not $apksigner) {
                Add-Failure 'Android build-tools were not found through ANDROID_HOME/ANDROID_SDK_ROOT.'
            } else {
                $badging = (& $aapt dump badging $resolvedPackage) -join "`n"
                if ($LASTEXITCODE -ne 0 -or $badging -notmatch "package: name='$([regex]::Escape($appId))'" -or $badging -notmatch "versionName='$([regex]::Escape($versionName))'" -or $badging -notmatch "versionCode='$([regex]::Escape($versionCode))'") {
                    Add-Failure 'APK package ID or version does not match the project.'
                } else { Add-Pass 'APK identity matches the project.' }
                if ($badging -match "sdkVersion:'21'" -and $badging -match "targetSdkVersion:'35'" -and $badging -match "compileSdkVersion='35'") {
                    Add-Pass 'APK SDK levels: min 21, target/compile 35.'
                } else { Add-Failure 'APK must use minSdk 21 and target/compileSdk 35.' }
                Test-ManifestPermissions $badging $appId
                $signatureReport = (& $apksigner verify --verbose --print-certs $resolvedPackage 2>&1) -join "`n"
                if ($LASTEXITCODE -eq 0) { Add-Pass 'APK signature is valid.' } else { Add-Failure 'APK signature validation failed.' }
                if (-not $AllowDebugCertificate -and $signatureReport -match 'CN=Android Debug') {
                    Add-Failure 'APK uses the Android Debug certificate and cannot be submitted to the store.'
                } elseif ($signatureReport -match 'certificate SHA-256 digest:\s*([0-9a-f]+)') {
                    Add-Pass "APK certificate SHA-256: $($Matches[1].ToUpperInvariant())."
                }
            }
        } elseif ($extension -eq '.aab') {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $archive = [IO.Compression.ZipFile]::OpenRead($resolvedPackage)
            try {
                $signatureEntries = @($archive.Entries | Where-Object { $_.FullName -match '^META-INF/.+\.(RSA|DSA|EC)$' })
                if ($signatureEntries.Count -eq 0) { Add-Failure 'AAB has no signing certificate entry.' } else { Add-Pass 'AAB contains a signing certificate.' }
            } finally { $archive.Dispose() }
            $java = Find-JavaExecutable
            $bundletool = Find-BundleTool
            if (-not $java -or -not $bundletool) {
                Add-Failure 'Java or .NET Android bundletool was not found; AAB identity cannot be verified.'
            } else {
                $manifest = (& $java -jar $bundletool dump manifest "--bundle=$resolvedPackage") -join "`n"
                $packagePattern = 'package=[''"]{0}[''"]' -f [regex]::Escape($appId)
                $versionPattern = 'versionName=[''"]{0}[''"]' -f [regex]::Escape($versionName)
                $versionCodePattern = 'versionCode=[''"]{0}[''"]' -f [regex]::Escape($versionCode)
                if ($LASTEXITCODE -ne 0 -or $manifest -notmatch $packagePattern -or $manifest -notmatch $versionPattern -or $manifest -notmatch $versionCodePattern) {
                    Add-Failure 'AAB package ID or version does not match the project.'
                } else { Add-Pass 'AAB identity matches the project.' }
                if ($manifest -match 'compileSdkVersion=[''"]35[''"]' -and $manifest -match 'minSdkVersion=[''"]21[''"]' -and $manifest -match 'targetSdkVersion=[''"]35[''"]') {
                    Add-Pass 'AAB SDK levels: min 21, target/compile 35.'
                } else { Add-Failure 'AAB must use minSdk 21 and target/compileSdk 35.' }
                Test-ManifestPermissions $manifest $appId
            }
            $jarsigner = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME 'bin\jarsigner.exe' } else { $null }
            if (-not $jarsigner -or -not (Test-Path -LiteralPath $jarsigner)) {
                $command = Get-Command jarsigner -ErrorAction SilentlyContinue
                $jarsigner = if ($command) { $command.Source } else { $null }
            }
            if (-not $jarsigner) { Add-Failure 'jarsigner was not found through JAVA_HOME or PATH.' }
            else {
                $signatureReport = (& $jarsigner -verify -verbose -certs $resolvedPackage 2>&1) -join "`n"
                if ($LASTEXITCODE -eq 0) { Add-Pass 'AAB signature is readable and valid.' } else { Add-Failure 'AAB signature validation failed.' }
                if (-not $AllowDebugCertificate -and $signatureReport -match 'CN=Android Debug') {
                    Add-Failure 'AAB uses the Android Debug certificate and cannot be submitted to the store.'
                } elseif ($AllowDebugCertificate -and $signatureReport -match 'CN=Android Debug') {
                    Write-Host '[WARN] AAB uses the Android Debug certificate (QA mode).' -ForegroundColor Yellow
                }
            }
        } else {
            Add-Failure 'PackagePath must point to an .apk or .aab file.'
        }
    }
} catch {
    Add-Failure $_.Exception.Message
} finally {
    Pop-Location
}

if ($failures.Count -gt 0) {
    Write-Host "`nRuStore preflight failed with $($failures.Count) issue(s)." -ForegroundColor Red
    exit 1
}

Write-Host "`nRuStore preflight passed." -ForegroundColor Green
