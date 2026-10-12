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
