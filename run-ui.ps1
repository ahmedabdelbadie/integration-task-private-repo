# 3) Angular app  ->  http://localhost:4200
# Start after the API. Installs npm packages on the first run.
$Host.UI.RawUI.WindowTitle = 'Angular :4200'
Set-Location (Join-Path $PSScriptRoot 'src\github-monitor-ui')
if (-not (Test-Path 'node_modules')) {
    npm install
}
npx ng serve --port 4200 --open
