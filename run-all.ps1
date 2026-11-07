# Starts the WCF service, the ASP.NET Core API and the Angular app, each in its own window.
# Usage:  .\run-all.ps1            (first run: .\run-all.ps1 -Install to npm install the UI)
param([switch]$Install)

$root = $PSScriptRoot
$ui = Join-Path $root 'src\github-monitor-ui'

if ($Install -or -not (Test-Path (Join-Path $ui 'node_modules'))) {
    Push-Location $ui
    npm install
    Pop-Location
}

Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$root'; `$Host.UI.RawUI.WindowTitle = 'WCF Service :5100'; dotnet run --project src/GitHubMonitor.WcfService"
Start-Sleep -Seconds 3
Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$root'; `$Host.UI.RawUI.WindowTitle = 'API :5200'; dotnet run --project src/GitHubMonitor.Api --launch-profile http"
Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$ui'; `$Host.UI.RawUI.WindowTitle = 'Angular :4200'; npx ng serve --port 4200 --open"

Write-Host ''
Write-Host 'Starting...' -ForegroundColor Cyan
Write-Host '  WCF service : http://localhost:5100/GitHubCommitService.svc  (WSDL: ?wsdl, health: /health)'
Write-Host '  API         : http://localhost:5200/swagger'
Write-Host '  Angular UI  : http://localhost:4200'
