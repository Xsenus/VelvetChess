[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$KeyStore,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string]$KeyAlias,
    [Parameter(Mandatory)] [string]$StorePasswordFile,
    [string]$KeyPasswordFile,
    [string]$OutputDirectory,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repoPrefix = $repo.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

function Resolve-ExternalFile([string]$Path, [string]$Description) {
    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw "$Description is not a file: $resolved" }
    if ($resolved.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must be stored outside the repository: $resolved"
    }
    return $resolved
}

$resolvedKeyStore = Resolve-ExternalFile $KeyStore 'Keystore'
$resolvedStorePassword = Resolve-ExternalFile $StorePasswordFile 'Store password file'
if (-not $KeyPasswordFile) { $KeyPasswordFile = $StorePasswordFile }
$resolvedKeyPassword = Resolve-ExternalFile $KeyPasswordFile 'Key password file'

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts\release\android' }
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

Push-Location $repo
try {
    $preflightArgs = @{}
    if ($SkipTests) { $preflightArgs['SkipTests'] = $true }
    & (Join-Path $PSScriptRoot 'Test-RuStoreReadiness.ps1') @preflightArgs
    if (-not $?) { throw 'RuStore preflight must pass before a signed release is created.' }

    # A signed store package must never be selected from an earlier incremental build.
    & dotnet clean 'src\VelvetChess.App\VelvetChess.App.csproj' -f net9.0-android -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Android release clean failed.' }

    $publishArgs = @(
        'publish', 'src\VelvetChess.App\VelvetChess.App.csproj',
        '-f', 'net9.0-android', '-c', 'Release',
        '-p:AndroidPackageFormats=apk%3Baab',
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$resolvedKeyStore",
        "-p:AndroidSigningKeyAlias=$KeyAlias",
        "-p:AndroidSigningKeyPass=file:$resolvedKeyPassword",
        "-p:AndroidSigningStorePass=file:$resolvedStorePassword"
    )
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    [xml]$project = Get-Content 'src\VelvetChess.App\VelvetChess.App.csproj' -Raw
    $version = [string]($project.Project.PropertyGroup.ApplicationDisplayVersion | Select-Object -First 1)
    $buildRoot = Join-Path $repo 'src\VelvetChess.App\bin\Release\net9.0-android'
    $signedAab = Get-ChildItem $buildRoot -Recurse -File -Filter '*-Signed.aab' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    $signedApk = Get-ChildItem $buildRoot -Recurse -File -Filter '*-Signed.apk' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (-not $signedAab -or -not $signedApk) { throw 'Signed APK/AAB outputs were not found.' }

    $aabOutput = Join-Path $resolvedOutput "VelvetChess-$version-RuStore-signed.aab"
    $apkOutput = Join-Path $resolvedOutput "VelvetChess-$version-RuStore-signed.apk"
    Copy-Item -LiteralPath $signedAab.FullName -Destination $aabOutput -Force
    Copy-Item -LiteralPath $signedApk.FullName -Destination $apkOutput -Force

    foreach ($package in @($aabOutput, $apkOutput)) {
        & (Join-Path $PSScriptRoot 'Test-RuStoreReadiness.ps1') -PackagePath $package -SkipTests
        if (-not $?) { throw "Package verification failed: $package" }
        $hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
        [IO.File]::WriteAllText("$package.sha256", "$hash  $([IO.Path]::GetFileName($package))`n")
        Write-Host "Created: $package" -ForegroundColor Green
        Write-Host "SHA-256: $hash"
    }

    $sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $null }
    $apksigner = if ($sdk) {
        Get-ChildItem (Join-Path $sdk 'build-tools') -Recurse -File -Filter 'apksigner.bat' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    } else { $null }
    if (-not $apksigner) { throw 'apksigner was not found; certificate fingerprint cannot be recorded.' }
    $certificateReport = (& $apksigner verify --print-certs $apkOutput 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $certificateReport -notmatch 'certificate SHA-256 digest:\s*([0-9a-f]+)') {
        throw 'Release certificate SHA-256 could not be extracted.'
    }
    $certificateHash = $Matches[1].ToUpperInvariant()
    $certificateFile = Join-Path $resolvedOutput "VelvetChess-$version-signing-certificate.sha256"
    [IO.File]::WriteAllText($certificateFile, "$certificateHash`n")
    Write-Host "Signing certificate SHA-256: $certificateHash" -ForegroundColor Green
} finally {
    Pop-Location
}
