# Run Steps — GitHub Repository Monitor

Run every command below in a terminal (PowerShell or cmd). Unless a step says otherwise, run it from the repository root:

```
C:\@Work\ASAP
```

Open a **new** terminal window, so `dotnet`, `node`, `npm` and `git` are on PATH.

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
- Instead of user-secrets, you can set the `GitHub__Token` environment variable before starting the WCF service.
- The token needs **Repository access:** only `integration-task-private-repo`, and **Contents: Read-only**.
- If the WCF service is already running, restart it (Step 2.1) after changing the token.

---

## Step 2 — Start each part (each in its own terminal window)

Start them **in this order** and leave each window open.

### 2.1 WCF service + Quartz scheduler

**Path:** `C:\@Work\ASAP`

```powershell
cd C:\@Work\ASAP
dotnet run --project src/GitHubMonitor.WcfService
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
dotnet run --project src/GitHubMonitor.Api --launch-profile http
```

- Runs on http://localhost:5200, and Swagger is at http://localhost:5200/swagger.
- Check that it's working:
  - http://localhost:5200/api/github/live
  - http://localhost:5200/api/github/scheduled

### 2.3 Angular app

**Path:** `C:\@Work\ASAP\src\github-monitor-ui` (in a third window)

```powershell
cd C:\@Work\ASAP\src\github-monitor-ui
npm install
npx ng serve --port 4200 --open
```

- `npm install` is only needed the first time, and takes a minute.
- Runs on http://localhost:4200 and opens the browser automatically.
- You should see "GitHub Repository Monitor" with the **Live Commits** and **Scheduled Commits** grids.
- The Kendo "Invalid license" watermark and trial banner are expected, because no license key is activated.

### Stop

Press `Ctrl + C` in each window, or just close the windows.

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
| `dotnet`, `npm` or `git` not recognized | Open a **new** terminal window, so it picks up the updated PATH. |
