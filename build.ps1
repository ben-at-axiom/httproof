#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$OutputDir = "$PSScriptRoot\publish"
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'HTTProof\HTTProof.csproj'
$outPath = Join-Path $OutputDir $Runtime

if (Test-Path $outPath) {
    Remove-Item $outPath -Recurse -Force
}

Write-Host "Publishing $Runtime single-file exe -> $outPath"

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    -o $outPath `
    -p:PublishSingleFileBundle=true `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit $LASTEXITCODE"
}

$exe = Get-ChildItem $outPath -Filter 'HTTProof.exe' | Select-Object -First 1
if (-not $exe) {
    throw "HTTProof.exe not found in $outPath"
}

$sizeMb = [math]::Round($exe.Length / 1MB, 2)
Write-Host "Done: $($exe.FullName) ($sizeMb MB)"
