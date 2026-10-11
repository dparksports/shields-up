# ControlCore release build: self-contained win-x64 folder + zip + SHA256SUMS.
# Mirrors AutoCommand's distribution model (loose files, no installer).
#
# Usage:  .\publish.ps1 [-OutDir .\publish]
param(
    [string]$OutDir = ".\publish"
)

$ErrorActionPreference = "Stop"
Push-Location (Split-Path -Parent $PSCommandPath)

$version = (Select-Xml -Path ".\src\ControlCore\ControlCore.csproj" -XPath "//Version").Node.InnerText
$publishDir = Join-Path $OutDir "v$version"

Write-Host "== ControlCore v$version - publishing self-contained win-x64 ==" -ForegroundColor Cyan

dotnet publish ".\src\ControlCore\ControlCore.csproj" -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# Zip + checksum
$zip = Join-Path $OutDir "ControlCore-v$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zip

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$zipName = Split-Path -Leaf $zip
"$hash  $zipName" | Out-File (Join-Path $OutDir "SHA256SUMS.txt") -Encoding ascii

Write-Host ""
Write-Host "Done:" -ForegroundColor Green
Write-Host "  folder : $publishDir"
Write-Host "  zip    : $zip"
Write-Host "  sha256 : $hash"
Pop-Location
