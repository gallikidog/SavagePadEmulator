[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.3'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'SavagePadEmu.csproj'
$publishDir = Join-Path $root "publish\$Runtime"
$artifactsDir = Join-Path $root 'artifacts'
$zipPath = Join-Path $artifactsDir "SavagePadEmulator-$Version-portable-$Runtime.zip"

Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null

dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishDir

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
Write-Host "Portable package generated: $zipPath" -ForegroundColor Green
