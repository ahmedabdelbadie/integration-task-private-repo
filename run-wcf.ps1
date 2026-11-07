# 1) WCF service + Quartz scheduler  ->  http://localhost:5100/GitHubCommitService.svc  (?wsdl, /health)
# Start this first. Needs the GitHub token in user-secrets (see RUN_STEPS.md).
$Host.UI.RawUI.WindowTitle = 'WCF Service :5100'
Set-Location $PSScriptRoot
dotnet run --project src/GitHubMonitor.WcfService
