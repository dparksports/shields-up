# ShieldsUp release build: self-contained win-x64 folder + zip + SHA256SUMS.
# Mirrors AutoCommand's distribution model (loose files, no installer).
#
# Usage:  .\publish.ps1 [-OutDir .\publish]
param(
    [string]$OutDir = ".\publish"
)

$ErrorActionPreference = "Stop"
Push-Location (Split-Path -Parent $PSCommandPath)

$version = (Select-Xml -Path ".\src\ShieldsUp\ShieldsUp.csproj" -XPath "//Version").Node.InnerText
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
