# ShieldsUp release build: self-contained win-x64 folder + zip + SHA256SUMS.
# Mirrors AutoCommand's distribution model (loose files, no installer).
#
# Usage:  .\publish.ps1 [-OutDir .\publish] [-Version 2026.10.12]
param(
    [string]$OutDir = ".\publish",
    [string]$Version
)

$ErrorActionPreference = "Stop"
Push-Location (Split-Path -Parent $PSCommandPath)

# Version = build date (yyyy.M.d), stamped into both csproj files so every
# release carries the day it was built, AutoCommand-style. -Version overrides
# for same-day re-releases (the date would collide with the earlier build).
$version = if ($Version) { $Version } else { Get-Date -Format "yyyy.M.d" }
foreach ($proj in @(".\src\ShieldsUp\ShieldsUp.csproj", ".\src\ShieldsUp.Core\ShieldsUp.Core.csproj")) {
    (Get-Content $proj -Raw) -replace "<Version>[^<]+</Version>", "<Version>$version</Version>" |
        Set-Content $proj -NoNewline
}
$publishDir = Join-Path $OutDir "v$version"

Write-Host "== ShieldsUp v$version - publishing self-contained win-x64 ==" -ForegroundColor Cyan

dotnet publish ".\src\ShieldsUp\ShieldsUp.csproj" -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# ── Prune: Windows AI / Semantic Search machinery the app never calls ──
# WinAppSDK 2.5's self-contained layout deploys its AI/Foundry feature set
# (~50 MB: onnxruntime, DirectML, the Microsoft.Windows.AI.* projections,
# Semantic Search) with every app whether or not the app calls those APIs.
# This app calls none of them. Evidence the set is inert here: a text-mode
# scan of every remaining binary finds no reference to any of these files —
# the only mentions anywhere are (a) each other, (b) the Windows API surface
# projection monolith (Microsoft.Windows.SDK.NET.dll projects EVERY Windows
# API as strings; projecting is not loading), and (c) ShieldsUp.dll's
# embedded WinRT activation manifest, which REGISTERS every shipped file's
# classes and only touches one when a class is activated (nothing activates
# these).
# Deliberately NOT pruned (referenced as strings by core startup-path DLLs
# Microsoft.WinUI / Microsoft.Internal.FrameworkUdk — string != load, but
# the whole family is ~1.4 MB and not worth the bet): Microsoft.Windows
# .Workloads* + workloads*.json, Microsoft.Graphics.Imaging*.
# NOTE: the prune list is version-pinned to the WinAppSDK file layout —
# re-diff it on every SDK bump (the count warning below catches a shuffle).
$prunePatterns = @(
    'onnxruntime.dll', 'DirectML.dll', 'Microsoft.ML.OnnxRuntime.dll',
    'Microsoft.Asg.SemanticIndex.*',
    'Microsoft.Windows.AI.*', 'Microsoft.Windows.Internal.AI.*',
    'Microsoft.Windows.Internal.SemanticSearch.winmd',
    'Microsoft.Windows.Search.*', 'Microsoft.Windows.SemanticSearch*.winmd',
    'PerceptiveStreaming.dll', 'WinUIEdit.dll',
    'Microsoft.InteractiveExperiences.Projection.dll'
)
$pruned = @()
foreach ($pat in $prunePatterns) {
    $pruned += @(Get-ChildItem $publishDir -File -Filter $pat -ErrorAction SilentlyContinue)
}
if ($pruned.Count -lt 35) {
    Write-Warning "prune matched only $($pruned.Count) files (expected ~45) - WinAppSDK layout may have changed; review the prune list"
}
$saved = ($pruned | Measure-Object -Property Length -Sum).Sum
foreach ($f in $pruned) { Remove-Item $f.FullName -Force }
Write-Host ("pruned {0} unused AI/Search file(s), {1:N1} MB" -f $pruned.Count, ($saved / 1MB))

# Zip + checksum (System.IO.Compression — Compress-Archive is flaky on large trees)
$zip = Join-Path $OutDir "ShieldsUp-v$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publishDir, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$zipName = Split-Path -Leaf $zip
"$hash  $zipName" | Out-File (Join-Path $OutDir "SHA256SUMS.txt") -Encoding ascii

Write-Host ""
Write-Host "Done:" -ForegroundColor Green
Write-Host "  folder : $publishDir"
Write-Host "  zip    : $zip"
Write-Host "  sha256 : $hash"
Pop-Location
