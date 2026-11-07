# Run Steps — GitHub Repository Monitor

Every command below is run in **PowerShell**, from the project folder:

```
C:\@Work\ASAP
```

---

## Step 0 — Open PowerShell in the project folder

Open a **new** PowerShell window (so `dotnet`, `node`, `npm` and `git` are on PATH), then run:

```powershell
cd C:\@Work\ASAP
```

If PowerShell blocks the `.ps1` scripts ("running scripts is disabled on this system"), allow them for the current user once:

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

---

## Step 1 — Set the GitHub token (once, and again whenever you regenerate it)

**Path:** `C:\@Work\ASAP`

```powershell
cd C:\@Work\ASAP
dotnet user-secrets set "GitHub:Token" "PASTE_YOUR_NEW_TOKEN_HERE" --project src/GitHubMonitor.WcfService
```

You can also run it from any folder by giving the full project path:

```powershell
dotnet user-secrets set "GitHub:Token" "PASTE_YOUR_NEW_TOKEN_HERE" --project "C:\@Work\ASAP\src\GitHubMonitor.WcfService"
```

To check that it's saved (this prints the token, so don't share the output):

```powershell
dotnet user-secrets list --project "C:\@Work\ASAP\src\GitHubMonitor.WcfService"
```

**Notes:**
- The token is stored outside the repository, in
  `%APPDATA%\Microsoft\UserSecrets\2f128527-aff1-4f8c-b6ee-a6a800008ece\secrets.json`.
- The token needs **Repository access:** only `integration-task-private-repo`, and **Contents: Read-only**.
- If the WCF service is already running, restart it (Step 2) after changing the token.

---

## Step 2 — Start each part separately (each in its own PowerShell window)

Start them **in this order** and leave each window open.

### 2.1 WCF service + Quartz scheduler

**Path:** `C:\@Work\ASAP`

```powershell
cd C:\@Work\ASAP
.\run-wcf.ps1
```

- Runs on http://localhost:5100
- Check that it's working:
  - http://localhost:5100/health shows `cachedCommits: 10` and a `lastSyncTime`.
  - http://localhost:5100/GitHubCommitService.svc?wsdl shows the WSDL.
- The log should say `Commit sync succeeded: 10 commits cached`, and repeat this every minute.

### 2.2 ASP.NET Core API

**Path:** `C:\@Work\ASAP` (in a second window)

```powershell
cd C:\@Work\ASAP
.\run-api.ps1
```

- Runs on http://localhost:5200, and Swagger opens at http://localhost:5200/swagger.
- Check that it's working:
  - http://localhost:5200/api/github/live
  - http://localhost:5200/api/github/scheduled

### 2.3 Angular app

**Path:** `C:\@Work\ASAP` (in a third window)

```powershell
cd C:\@Work\ASAP
.\run-ui.ps1
```

- The first run does `npm install`, which takes a minute.
- Runs on http://localhost:4200 and opens the browser automatically.
- You should see "GitHub Repository Monitor" with the **Live Commits** and **Scheduled Commits** grids.
- The Kendo "Invalid license" watermark and trial banner are expected, because no license key is activated.

### Stop

Press `Ctrl + C` in each window, or just close the windows.

---

## Alternative — start all three at once

**Path:** `C:\@Work\ASAP`

```powershell
cd C:\@Work\ASAP
.\run-all.ps1
```

This opens three windows that run `run-wcf.ps1`, `run-api.ps1` and `run-ui.ps1`.

---

## Manual commands (without the scripts)

| Part | Path | Command |
|---|---|---|
| WCF + Quartz | `C:\@Work\ASAP` | `dotnet run --project src/GitHubMonitor.WcfService` |
| API | `C:\@Work\ASAP` | `dotnet run --project src/GitHubMonitor.Api --launch-profile http` |
| Angular | `C:\@Work\ASAP\src\github-monitor-ui` | `npm install` (first time only), then `npx ng serve --port 4200 --open` |

---

## Run the tests (optional)

| Tests | Path | Command |
|---|---|---|
| .NET (20 tests) | `C:\@Work\ASAP` | `dotnet test` |
| Angular (4 tests) | `C:\@Work\ASAP\src\github-monitor-ui` | `npx ng test --watch=false` |

Stop the running WCF service and API before `dotnet test`. Otherwise the build fails because their files are locked.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Dialog says "GitHub authentication failed" (HTTP 401) | The token is wrong, expired or missing. Redo **Step 1**, then restart the WCF service. |
| Dialog says "Repository not found" (HTTP 404) | The token doesn't include `integration-task-private-repo`, or it's missing **Contents: Read-only**. |
| Dialog says "Service unavailable" (HTTP 503) | The WCF service isn't running. Start **2.1**. |
| Dialog says "API unreachable" | The API isn't running. Start **2.2**. |
| `Address already in use` / port in use | An older instance is still running. Close its window, or run `Get-Process GitHubMonitor.* \| Stop-Process`. |
| `dotnet`, `npm` or `git` not recognized | Open a **new** PowerShell window, so it picks up the updated PATH. |
