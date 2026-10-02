param(
    [string]$GameDir = "F:\SteamLibrary\steamapps\common\Stranded Deep",
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot
$SourceFiles = @(
    (Join-Path $ProjectDir "BetterMeat.cs"),
    (Join-Path $ProjectDir "ModSettingsClient.cs")
)
$BuildDir = Join-Path $ProjectDir "build"
$OutputDll = Join-Path $BuildDir "BetterMeat.dll"

$ManagedDir = Join-Path $GameDir "Stranded_Deep_Data\Managed"
$BepInExDir = Join-Path $GameDir "BepInEx"

$Refs = @(
    (Join-Path $BepInExDir "core\BepInEx.dll"),
    (Join-Path $BepInExDir "core\0Harmony.dll"),
    (Join-Path $ManagedDir "Assembly-CSharp.dll"),
    (Join-Path $ManagedDir "bolt.dll"),
    (Join-Path $ManagedDir "bolt.user.dll")
)

$UnityRefs = Get-ChildItem -Path $ManagedDir -Filter "UnityEngine*.dll" |
    Sort-Object Name |
    ForEach-Object { $_.FullName }

$Refs += $UnityRefs

foreach ($file in @($SourceFiles) + $Refs) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "Required file not found: $file"
    }
}

$Csc = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $Csc) {
    throw "csc.exe was not found."
}

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

$Args = @(
    "/nologo",
    "/target:library",
    "/optimize+",
    "/platform:anycpu",
    "/out:$OutputDll"
)

foreach ($r in $Refs) {
    $Args += "/reference:$r"
}

foreach ($SourceFile in $SourceFiles) {
    $Args += $SourceFile
}

Write-Host "Game: $GameDir"
Write-Host "Compiler: $Csc"
Write-Host "Unity references: $($UnityRefs.Count)"
Write-Host "Building Better Meat v0.3.1..."

& $Csc $Args

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $OutputDll"

if ($Deploy) {
    $PluginDir = Join-Path $BepInExDir "plugins\BetterMeat"
    New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null

    Copy-Item `
        -LiteralPath $OutputDll `
        -Destination (Join-Path $PluginDir "BetterMeat.dll") `
        -Force

    Write-Host "Deployed to: $PluginDir"
}

Write-Host ""
Write-Host "Better Meat v0.3.1:"
Write-Host "  - bottom-anchored HUD"
Write-Host "  - one soft-edge background"
Write-Host "  - strict crosshair-to-station targeting (no subtree search)"
Write-Host "  - meat mechanics unchanged"
Write-Host "  - Mod Settings integration (soft dependency)"
Write-Host "  - 6 user-facing settings in Options -> MODS"

