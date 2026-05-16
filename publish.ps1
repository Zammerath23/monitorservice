#requires -Version 5.1
<#
.SYNOPSIS
  Build a self-contained, single-file executable of MonitorService for Windows x64
  and stage it alongside its config files in dist\.
#>

param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src\MonitorService\MonitorService.csproj'
$publishDir = Join-Path $PSScriptRoot ('artifacts\publish\' + $Runtime)
$distDir = Join-Path $PSScriptRoot 'dist'

Write-Host "==> Restoring..." -ForegroundColor Cyan
dotnet restore $project

Write-Host "==> Publishing single-file self-contained ($Runtime, $Configuration)..." -ForegroundColor Cyan
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -o $publishDir

if (Test-Path $distDir) { Remove-Item -Recurse -Force $distDir }
New-Item -ItemType Directory -Path $distDir | Out-Null

Copy-Item (Join-Path $publishDir 'MonitorService.exe') (Join-Path $distDir 'MonitorService.exe')
Copy-Item (Join-Path $publishDir 'appsettings.json')   (Join-Path $distDir 'appsettings.json')
Copy-Item (Join-Path $publishDir 'sources.json')       (Join-Path $distDir 'sources.json')

# Convenience scripts for the end user (live at the repo root):
foreach ($extra in @('README.md', 'AI-HELPER.md', 'instalar-autoarranque.bat', 'desinstalar-autoarranque.bat')) {
    if (Test-Path $extra) {
        Copy-Item $extra (Join-Path $distDir $extra)
    }
}

Write-Host ""
Write-Host "==> Done. Distributable contents:" -ForegroundColor Green
Get-ChildItem $distDir | Format-Table Name, Length -AutoSize

Write-Host ""
Write-Host "Next steps for the end user:" -ForegroundColor Yellow
Write-Host "  1. Edit appsettings.json -> Telegram.BotToken (get one from @BotFather)."
Write-Host "  2. Run 'MonitorService.exe --discover-chat' to grab the ChatId, paste it back into appsettings.json."
Write-Host "  3. Edit sources.json with the RSS feeds / web pages to watch."
Write-Host "  4. Double-click MonitorService.exe."
