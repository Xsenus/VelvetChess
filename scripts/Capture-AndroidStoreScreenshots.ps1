[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackagePath,
    [string]$DeviceId,
    [string]$OutputDirectory,
    [switch]$KeepAppData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $null }
if (-not $sdk) { throw 'ANDROID_HOME or ANDROID_SDK_ROOT is required.' }
$adb = Join-Path $sdk 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb -PathType Leaf)) { throw "adb was not found: $adb" }
$package = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'store\rustore\screenshots' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null

function Invoke-Adb([string[]]$Arguments) {
    $prefix = if ($DeviceId) { @('-s', $DeviceId) } else { @() }
    $result = @(& $adb @prefix @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "adb $($Arguments -join ' ') failed:`n$($result -join "`n")" }
    return $result
}

$devices = @(& $adb devices | Select-String '^\S+\s+device$' | ForEach-Object { ($_ -split '\s+')[0] })
if (-not $DeviceId) {
    if ($devices.Count -ne 1) { throw "Expected exactly one ready Android device, found $($devices.Count). Use -DeviceId." }
    $DeviceId = $devices[0]
} elseif ($DeviceId -notin $devices) { throw "Android device is not ready: $DeviceId" }

$sizeOutput = (Invoke-Adb @('shell', 'wm', 'size')) -join "`n"
if ($sizeOutput -notmatch 'Physical size:\s*1080x1920') {
    throw "The RuStore capture device must have a physical 1080x1920 display. Actual output: $sizeOutput"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('velvetchess-capture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$uiPath = Join-Path $tempRoot 'window.xml'

function Get-Ui {
    Invoke-Adb @('shell', 'uiautomator', 'dump', '/sdcard/velvetchess-window.xml') | Out-Null
    Invoke-Adb @('pull', '/sdcard/velvetchess-window.xml', $uiPath) | Out-Null
    return [xml](Get-Content -LiteralPath $uiPath -Raw)
}

function Find-Text([string]$Text, [int]$TimeoutSeconds = 15) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $xml = Get-Ui
        $node = $xml.SelectNodes('//node') | Where-Object { $_.text -eq $Text } | Select-Object -First 1
        if ($node) { return $node }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "UI text was not found within $TimeoutSeconds seconds: $Text"
}

function Tap-Text([string]$Text) {
    $node = Find-Text $Text
    if ($node.bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') { throw "Invalid UI bounds for '$Text': $($node.bounds)" }
    $x = [int](([int]$Matches[1] + [int]$Matches[3]) / 2)
    $y = [int](([int]$Matches[2] + [int]$Matches[4]) / 2)
    Invoke-Adb @('shell', 'input', 'tap', "$x", "$y") | Out-Null
}

function Capture([string]$Name) {
    $destination = Join-Path $output $Name
    Invoke-Adb @('shell', 'screencap', '-p', "/sdcard/$Name") | Out-Null
    Invoke-Adb @('pull', "/sdcard/$Name", $destination) | Out-Null
    if ((Get-Item -LiteralPath $destination).Length -gt 3MB) { throw "Screenshot exceeds 3 MiB: $Name" }
    Write-Host "Captured: $destination" -ForegroundColor Green
}

try {
    if (-not $KeepAppData) {
        $prefix = if ($DeviceId) { @('-s', $DeviceId) } else { @() }
        & $adb @prefix uninstall ru.velvetchess.game 2>&1 | Out-Null
    }
    Invoke-Adb @('install', '-r', $package) | Out-Host
    Invoke-Adb @('logcat', '-c') | Out-Null
    Invoke-Adb @('shell', 'monkey', '-p', 'ru.velvetchess.game', '-c', 'android.intent.category.LAUNCHER', '1') | Out-Null

    Find-Text 'Тактические задачи' | Out-Null
    Capture '01_home.png'

    Tap-Text 'Играть против компьютера'
    Find-Text 'Новая партия' | Out-Null
    Invoke-Adb @('shell', 'input', 'tap', '602', '1266') | Out-Null
    Start-Sleep -Milliseconds 350
    Capture '02_local_game.png'
    Invoke-Adb @('shell', 'input', 'tap', '602', '1017') | Out-Null
    Start-Sleep -Seconds 2
    Find-Text 'Ваш ход' | Out-Null
    Invoke-Adb @('shell', 'input', 'keyevent', '4') | Out-Null

    Find-Text 'Тактические задачи' | Out-Null
    Invoke-Adb @('shell', 'input', 'swipe', '540', '1750', '540', '850', '450') | Out-Null
    Tap-Text 'Тактические задачи'
    Find-Text 'Нерешённые' | Out-Null
    Capture '03_puzzles.png'

    Tap-Text 'Вилка · 1'
    Find-Text 'Показать решение' | Out-Null
    Capture '04_puzzle_play.png'
    Tap-Text 'Показать решение'
    Tap-Text 'Показать'
    Start-Sleep -Seconds 4
    Capture '06_puzzle_solution.png'

    Invoke-Adb @('shell', 'input', 'keyevent', '4') | Out-Null
    Find-Text 'Нерешённые' | Out-Null
    Invoke-Adb @('shell', 'input', 'keyevent', '4') | Out-Null
    Find-Text 'Тактические задачи' | Out-Null
    Invoke-Adb @('shell', 'input', 'swipe', '540', '1750', '540', '750', '450') | Out-Null
    Find-Text 'Настройки и о приложении' | Out-Null
    Tap-Text 'Настройки и о приложении'
    Find-Text 'Оформление' | Out-Null
    Capture '05_settings_appearance.png'
    Invoke-Adb @('shell', 'input', 'swipe', '540', '1750', '540', '800', '450') | Out-Null
    Find-Text 'Показывать возможные ходы' | Out-Null
    Capture '08_settings_board_behavior.png'

    Invoke-Adb @('shell', 'input', 'keyevent', '4') | Out-Null
    Find-Text 'Профиль и рейтинг' | Out-Null
    Tap-Text 'Профиль и рейтинг'
    Find-Text 'Гостевой профиль' | Out-Null
    Capture '07_profile.png'
    Invoke-Adb @('shell', 'input', 'swipe', '540', '1750', '540', '650', '500') | Out-Null
    Find-Text 'Я   Войти с Яндекс ID' | Out-Null
    Capture '09_profile_auth.png'
    Tap-Text 'Я   Войти с Яндекс ID'
    Find-Text 'Сервер аккаунтов ещё не указан в сборке приложения.' | Out-Null
    Tap-Text 'Понятно'

    $fatal = (Invoke-Adb @('logcat', '-d', '-t', '2000', 'AndroidRuntime:E', '*:S')) -join "`n"
    if ($fatal -match 'FATAL EXCEPTION') { throw "AndroidRuntime crash found after navigation:`n$fatal" }
    Write-Host 'Android screenshot smoke test passed without runtime crashes.' -ForegroundColor Green
} finally {
    $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTemp.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolvedTemp -Leaf) -like 'velvetchess-capture-*') {
        [IO.Directory]::Delete($resolvedTemp, $true)
    }
}
