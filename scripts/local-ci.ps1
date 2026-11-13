# local-ci.ps1 - local verification gate for Shields Up.
#
# Mirrors AutoCommand's scripts/local-ci.ps1: redirects TMP/TEMP into .citmp,
# scans NuGet dependencies for vulnerabilities, builds Release and Debug, and
# runs ShieldsUpSelfTest. Exits non-zero on any failure.
#
# NOTE: keep this file pure ASCII (Windows PowerShell 5.1 reads BOM-less scripts
# as ANSI).
#
# Usage:  powershell -File scripts\local-ci.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Die($msg)  { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

$citmp = Join-Path $repoRoot '.citmp'
New-Item -ItemType Directory -Force -Path $citmp | Out-Null
$env:TMP = $citmp; $env:TEMP = $citmp
Step "TMP/TEMP redirected to $citmp"

Step 'Dependency vulnerability scan'
$out = dotnet list ShieldsUp.slnx package --vulnerable --include-transitive
Write-Host $out
if ($out -match 'has the following vulnerable packages') { Die 'vulnerable packages found' }

Step 'Build (Release)'
dotnet build ShieldsUp.slnx -c Release --nologo
if ($LASTEXITCODE -ne 0) { Die 'Release build failed' }

Step 'Build (Debug)'
dotnet build ShieldsUp.slnx -c Debug --nologo
if ($LASTEXITCODE -ne 0) { Die 'Debug build failed' }

Step 'Self-test suite (strict)'
dotnet src\ShieldsUpSelfTest\bin\Release\net10.0-windows10.0.19041.0\win-x64\ShieldsUpSelfTest.dll
if ($LASTEXITCODE -ne 0) { Die 'ShieldsUpSelfTest failed' }

Step 'LOCAL CI: GREEN'
exit 0
