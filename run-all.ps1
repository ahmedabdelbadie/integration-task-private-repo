# Starts the WCF service, the API and the Angular app, each in its own window.
# Each part can also be started on its own with run-wcf.ps1, run-api.ps1 and run-ui.ps1 (see RUN_STEPS.md).
$root = $PSScriptRoot

Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'run-wcf.ps1')
Start-Sleep -Seconds 3
Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'run-api.ps1')
Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'run-ui.ps1')

Write-Host ''
Write-Host 'Starting...' -ForegroundColor Cyan
Write-Host '  WCF service : http://localhost:5100/GitHubCommitService.svc  (WSDL: ?wsdl, health: /health)'
Write-Host '  API         : http://localhost:5200/swagger'
Write-Host '  Angular UI  : http://localhost:4200'
