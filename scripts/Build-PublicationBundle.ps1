[CmdletBinding()]
param([Parameter(Mandatory)] [string]$OutputDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
$screenshots = Join-Path $output 'VelvetChess-Screenshots'
$assetZip = Join-Path $output 'VelvetChess-RuStore-assets-v1.zip'
$screenshotZip = Join-Path $output 'VelvetChess-Game-Screenshots.zip'
$publicationZip = Join-Path $output 'VelvetChess-1.0.0-RuStore-publication.zip'

New-Item -ItemType Directory -Path $screenshots -Force | Out-Null
Copy-Item -LiteralPath (Get-ChildItem (Join-Path $repo 'store\rustore\screenshots\*.png')).FullName -Destination $screenshots -Force
Compress-Archive -Path (Join-Path $screenshots '*') -DestinationPath $screenshotZip -Force
Compress-Archive -Path (Join-Path $repo 'store\rustore\*') -DestinationPath $assetZip -Force

$stage = Join-Path ([IO.Path]::GetTempPath()) ('velvetchess-publication-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path (Join-Path $stage 'release'), (Join-Path $stage 'store') | Out-Null
    Copy-Item -LiteralPath (Get-ChildItem (Join-Path $output 'RuStore-release\*')).FullName -Destination (Join-Path $stage 'release')
    Copy-Item -LiteralPath $assetZip -Destination (Join-Path $stage 'store')
    @(
        'Velvet Chess 1.0.0 RuStore publication bundle'
        'Contains signed APK/AAB, hashes, signing certificate fingerprint, and store assets.'
        'Does not contain the release keystore or passwords.'
    ) | Set-Content -LiteralPath (Join-Path $stage 'README.txt') -Encoding utf8
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $publicationZip -Force
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedStage.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolvedStage -Leaf) -like 'velvetchess-publication-*') {
        [IO.Directory]::Delete($resolvedStage, $true)
    }
}

$archives = @($screenshotZip, $assetZip, $publicationZip)
foreach ($file in $archives) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($file))" | Set-Content -LiteralPath ($file + '.sha256') -Encoding ascii
}
$archives | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($_))"
} | Set-Content -LiteralPath (Join-Path $output 'VelvetChess-ARTIFACTS-SHA256.txt') -Encoding ascii

Get-ChildItem $archives | Select-Object Name, Length, LastWriteTime
