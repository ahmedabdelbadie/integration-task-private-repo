# 2) ASP.NET Core API  ->  http://localhost:5200/swagger
# Start after the WCF service.
$Host.UI.RawUI.WindowTitle = 'API :5200'
Set-Location $PSScriptRoot
dotnet run --project src/GitHubMonitor.Api --launch-profile http
