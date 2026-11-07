# GitHub Repository Monitor

End-to-end integration task: **Quartz Job → WCF → GitHub private repository → ASP.NET Core → Angular (Kendo Grid)**.

| Part | Project | Tech |
|---|---|---|
| Shared WCF contract | `BE/src/GitHubMonitor.Contracts` | `System.ServiceModel` `[ServiceContract]` / `[DataContract]` |
| WCF service + Quartz scheduler | `BE/src/GitHubMonitor.WcfService` | .NET 9, **CoreWCF** (SOAP, `BasicHttpBinding`), **Quartz.NET** |
| REST API | `BE/src/GitHubMonitor.Api` | ASP.NET Core 9 Web API, WCF client (`ChannelFactory`) |
| Front-end | `FE/github-monitor-ui` | Angular 22, **Kendo UI for Angular** (Grid, Dialog, Buttons, Card) |
| Tests | `BE/tests/*` | xUnit + NSubstitute (.NET), Vitest (Angular) |

---

## Architecture

```
                         ┌──────────────────────── GitHubMonitor.WcfService (:5100) ────────────────────────┐
                         │                                                                                    │
  ┌─────────────┐  every │  ┌──────────────┐   GET /repos/{owner}/{repo}/commits?per_page=10                  │
  │ Quartz job  │──60s──►│  │ GitHubApi    │──────────────────────────────────────────────►  api.github.com  │
  │ SyncCommits │        │  │ Client       │◄──────────────────────────────────────────────  (private repo)  │
  └──────┬──────┘        │  └──────▲───────┘   Authorization: Bearer {token}                                  │
         │ save + LastSyncTime     │           User-Agent: IntegrationTaskApp                                 │
         ▼                         │ live call                                                                │
  ┌─────────────────────┐   ┌──────┴─────────────────────┐                                                    │
  │ Commit cache        │◄──│ GitHubCommitService (WCF)  │  SOAP endpoint  /GitHubCommitService.svc (+ ?wsdl) │
  │ memory + JSON file  │   │  GetLiveCommits()          │                                                    │
  └─────────────────────┘   │  GetScheduledCommits()     │                                                    │
                            └──────────────▲─────────────┘                                                    │
                         └─────────────────┼──────────────────────────────────────────────────────────────────┘
                                           │ SOAP (BasicHttpBinding), typed FaultException<GitHubFault>
                         ┌─────────────────┴────────── GitHubMonitor.Api (:5200) ─────────────┐
                         │ GitHubController  GET /api/github/live      → GetLiveCommits()       │
                         │                   GET /api/github/scheduled → GetScheduledCommits()  │
                         │ DTO mapping · { success, message, data } · global exception handler │
                         └─────────────────▲───────────────────────────────────────────────────┘
                                           │ JSON over HTTP (CORS: http://localhost:4200)
                         ┌─────────────────┴────────── github-monitor-ui (:4200) ─────────────┐
                         │ "GitHub Repository Monitor" page                                     │
                         │  A) Live Commits      – "Refresh Live Data" → Kendo Grid             │
                         │  B) Scheduled Commits – LastSyncTime, auto re-check → Kendo Grid      │
                         │  Errors → Kendo Dialog                                               │
                         └──────────────────────────────────────────────────────────────────────┘
```

### Key design decisions

- **CoreWCF on .NET 9** – the real WCF programming model (service/data/fault contracts, SOAP, WSDL) on the same
  runtime as the API; runs with `dotnet run`, no IIS or .NET Framework required.
- **Shared contract assembly** – `GitHubMonitor.Contracts` uses the standard `System.ServiceModel` attributes. CoreWCF
  hosts it and the API consumes it through `ChannelFactory<IGitHubCommitService>`, so there is no generated proxy to drift.
- **Quartz runs inside the WCF host** – the job and `GetScheduledCommits()` share one singleton cache, no IPC needed.
  The trigger fires on startup (`StartNow`) and then every 60 s; `[DisallowConcurrentExecution]` prevents overlapping runs.
- **Cache = in-memory + JSON file** (`BE/src/GitHubMonitor.WcfService/App_Data/commits-cache.json`). Writes are atomic
  (temp file + move), so the last sync survives a restart. A failed sync **keeps the last good commits** and records
  `LastSyncError` / `LastAttemptTime`, which the UI shows as a warning.
- **Typed errors end-to-end** – GitHub failures become `FaultException<GitHubFault>` with a code
  (`Unauthorized`, `Forbidden`, `RateLimited`, `NotFound`, `Timeout`, `UpstreamError`); the API maps them to HTTP status codes.

### API

Every response uses the unified envelope:

```json
{ "success": true, "message": "Fetched 10 live commits from GitHub.", "data": { ... } }
{ "success": false, "message": "GitHub rejected the access token (401). ...", "data": null }
```

`GET /api/github/live` and `GET /api/github/scheduled` return:

```json
{
  "source": "Scheduled",
  "count": 10,
  "lastSyncTime": "2026-11-07T17:40:18.59+00:00",
  "lastSyncError": null,
  "commits": [
    { "sha": "21b5c2ae…", "shortSha": "21b5c2a", "message": "Add Commit Number 11 to README",
      "author": "Ahmed Badea", "authorLogin": "ahmedabdelbadie", "date": "2026-11-07T17:12:04+00:00",
      "url": "https://github.com/…/commit/21b5c2ae…", "source": "Scheduled" }
  ]
}
```

| Failure | HTTP status | Message (example) |
|---|---|---|
| GitHub token invalid / expired / missing | **401** (passed through) | GitHub rejected the access token (401)… |
| Token lacks permission | 403 | The access token is not permitted to read … |
| Repo not found / token has no access to it | 404 | Repository … was not found (404)… |
| GitHub rate limit | 429 | GitHub API rate limit exceeded. Resets at … |
| GitHub did not answer within 15 s | 504 | GitHub did not respond within 15 seconds. |
| WCF did not answer within 30 s | 504 | The WCF commit service did not respond in time. |
| WCF service not running | 503 | The WCF commit service is unavailable… |
| Other WCF / GitHub failure | 502 | … |

Swagger UI: http://localhost:5200/swagger

---

## How authentication with GitHub is handled

- A **fine-grained Personal Access Token** scoped to *only* the target repository, with
  **Repository permissions → Contents: Read-only** (Metadata: Read-only is added automatically). No account permissions.
- The token is **never stored in the repository**. The WCF service reads `GitHub:Token` from configuration, supplied by:
  - **.NET user-secrets** during development (stored under `%APPDATA%\Microsoft\UserSecrets\…`, outside the repo), or
  - the **`GitHub__Token` environment variable** in any other environment.
- Only the WCF service talks to GitHub, so the token never reaches the API or the browser.
- Every request sends `Authorization: Bearer {token}`, `User-Agent: IntegrationTaskApp`,
  `Accept: application/vnd.github+json` and `X-GitHub-Api-Version: 2022-11-28`.
- `HttpClient` comes from `IHttpClientFactory` (typed client) with a 15 s timeout.
- If the token is missing, the service still starts. Calls then fail cleanly with an `Unauthorized` fault instead of
  calling GitHub anonymously. An invalid token from GitHub (401) is passed through to the UI as HTTP 401.

---

## How to run

### Prerequisites

- .NET SDK 9 (`dotnet --list-sdks`)
- Node.js 20+ and npm (`node -v`)
- A GitHub fine-grained PAT as described above
- *(Optional)* a Kendo UI license key – without it the grids work but show a watermark/banner (see below)

### 1. Configure the repository and token

The repository is configured in `BE/src/GitHubMonitor.WcfService/appsettings.json`:

```json
"GitHub": { "Owner": "ahmedabdelbadie", "Repository": "integration-task-private-repo", "CommitCount": 10, "TimeoutSeconds": 15 }
```

Store the token (once per machine):

```powershell
dotnet user-secrets set "GitHub:Token" "<your-token>" --project BE/src/GitHubMonitor.WcfService
```

### 2. Start everything

Start each part in its own terminal, from the repository root, **in this order**:

```powershell
# 1) WCF service + Quartz scheduler   → http://localhost:5100/GitHubCommitService.svc  (?wsdl, /health)
dotnet run --project BE/src/GitHubMonitor.WcfService

# 2) ASP.NET Core API                  → http://localhost:5200/swagger
dotnet run --project BE/src/GitHubMonitor.Api --launch-profile http

# 3) Angular app                       → http://localhost:4200
cd FE/github-monitor-ui
npm install
npx ng serve
```

### 3. Kendo UI license (removes the trial watermark)

1. Sign up for a free trial at https://www.telerik.com/kendo-angular-ui.
2. Download the license key from https://www.telerik.com/account/your-licenses/license-keys.
3. Save it as `FE/github-monitor-ui/kendo-ui-license.txt` (git-ignored) or set the `KENDO_UI_LICENSE` environment variable, then run:
   ```powershell
   cd FE/github-monitor-ui
   npx kendo-ui-license activate
   ```

### 4. Run the tests

```powershell
dotnet test BE                                # 20 tests: GitHub client, cache, Quartz job, WCF service, API
cd FE/github-monitor-ui; npx ng test --watch=false   # 4 tests: API service, error mapping, dialog
```

### Ports and configuration

| Setting | Where | Default |
|---|---|---|
| WCF URL | `GitHubMonitor.WcfService/Properties/launchSettings.json` | `http://localhost:5100` |
| Sync interval | `Scheduler:IntervalSeconds` (WCF) | `60` |
| Cache file | `Cache:FilePath` (WCF) | `App_Data/commits-cache.json` |
| WCF endpoint used by the API | `WcfService:EndpointAddress` (API) | `http://localhost:5100/GitHubCommitService.svc` |
| WCF call timeout | `WcfService:TimeoutSeconds` (API) | `30` |
| Allowed CORS origins | `Cors:AllowedOrigins` (API) | `http://localhost:4200` |
| API URL used by the UI | `FE/github-monitor-ui/src/environments/environment.ts` | `http://localhost:5200` |

---

## Front-end

- One page, **"GitHub Repository Monitor"**, with two Kendo Cards:
  - **Live Commits**: loads on page open, and again whenever you click **Refresh Live Data**.
  - **Scheduled Commits**: shows the Quartz cache with **LastSyncTime** (absolute and "x s ago"). It re-checks the API
    every 30 s and has a manual Refresh button. If the latest sync failed, a warning appears above the grid.
- Kendo Grid columns: **Commit Message, Author, Date, Commit SHA, Source (Live/Scheduled)**. The grid supports sorting
  (default: newest first), paging (5/10) and column resizing. The SHA links to the commit on GitHub, and hovering
  shows the full SHA or message.
- **Responsive**:
  - ≥ 1400 px: the two sections sit side by side.
  - Below that: the sections stack.
  - < 768 px: the five columns collapse into one stacked "Commit" column (Kendo column `media` queries).
- **Errors** appear in a Kendo Dialog. It shows the `message` from the API envelope, or "API unreachable" if the
  API is down. Repeats of the same error (for example from the auto-refresh) are not stacked.

---

## Assumptions and notes

- "Latest 10 commits" means the repository's default branch, newest first, as returned by `per_page=10`.
- The PDF's `company/integration-task-private-repo` is a placeholder. The demo uses
  `ahmedabdelbadie/integration-task-private-repo`, and owner and repo are configurable.
- **401 policy:** a GitHub 401 is passed through to the client as HTTP 401 with a clear message. The app has no
  user login, so the UI treats it as an upstream credential problem, not a session expiry.
- The service runs as a single instance, so in-memory + JSON cache is enough (no database or distributed cache).
- The WCF endpoint is plain HTTP on localhost for the demo. In production it would use HTTPS
  (`BasicHttpsBinding` / transport security) and the API would authenticate to it.
- The API itself has no end-user authentication, because the task doesn't require it. CORS is limited to the Angular origin.
- Kendo UI for Angular is a commercial library. Without a license key it still works but shows a watermark.
- Version notes: CoreWCF 1.9, `System.ServiceModel.*` client 8.1, and Quartz 3.x are used because Quartz 4.x and
  WCF client 10.x target .NET 10 only.
