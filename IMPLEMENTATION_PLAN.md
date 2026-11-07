# Implementation Plan — GitHub Repository Monitor

Data flow: **Quartz Job → GitHub API → Cache → WCF → ASP.NET Core → Angular (Kendo Grid)**

## Environment (checked 2026-11-07)

| Tool | Status |
|---|---|
| .NET SDK 9.0.320 / 10.0.401 | Installed — target **net9.0** |
| .NET Framework 4.8 targeting pack + VS 18 | Installed (only needed if classic WCF is chosen) |
| Node.js / npm / Angular CLI | **Missing** — install Node 22 LTS, then `npm i -g @angular/cli` |
| git | **Missing** — needed to publish the deliverable |

## Solution layout

```
ASAP/
├─ GitHubMonitor.sln
├─ src/
│  ├─ GitHubMonitor.Contracts/        netstandard2.0 — WCF ServiceContract, DataContracts, FaultContract
│  ├─ GitHubMonitor.WcfService/       net9.0 — CoreWCF host + Quartz job + GitHub client + cache
│  ├─ GitHubMonitor.Api/              net9.0 — ASP.NET Core Web API (WCF client)
│  └─ github-monitor-ui/              Angular 19 + Kendo UI for Angular
├─ tests/
│  ├─ GitHubMonitor.WcfService.Tests/ xUnit — GitHub client error mapping, cache, job
│  └─ GitHubMonitor.Api.Tests/        xUnit — controller/response mapping
└─ README.md
```

## Key design decisions

1. **WCF via CoreWCF on .NET 9** (BasicHttpBinding, SOAP, `[ServiceContract]`/`[OperationContract]`, WSDL exposed).
   Real WCF programming model, runs with `dotnet run`, no IIS/VS required.
   Alternative: classic WCF on .NET Framework 4.8 (IIS Express / self-host) — more "traditional" but heavier to run.
2. **Quartz runs inside the WCF host process.** The job and `GetScheduledCommits()` share the same singleton
   cache, so no IPC is needed. Cache is in-memory **and** persisted to `cache/commits-cache.json` so data survives restarts.
3. **Shared Contracts library** referenced by both WCF service and API — avoids generated proxy drift
   (`dotnet-svcutil` not needed; API uses `ChannelFactory<IGitHubCommitService>`).
4. **GitHub token never in source control**: `dotnet user-secrets` locally, env var `GitHub__Token` otherwise.
   Repo owner/name configurable (the PDF's `company/integration-task-private-repo` is a placeholder).
5. **Typed faults end-to-end**: WCF throws `FaultException<GitHubFault>` with a code
   (`Unauthorized`, `Forbidden/RateLimited`, `NotFound`, `Timeout`, `Upstream`). API maps codes to HTTP status + unified envelope.

## Phase 1 — Contracts (½ h)

- `IGitHubCommitService`: `GetLiveCommits()`, `GetScheduledCommits()`
- `CommitDto` (DataContract): `Sha`, `Message`, `AuthorName`, `AuthorLogin`, `Date`, `HtmlUrl`, `Source` (`Live`/`Scheduled`)
- `CommitsResult`: `Commits`, `LastSyncTime?`, `Source`
- `GitHubFault`: `Code` (enum), `Message`

## Phase 2 — WCF service + GitHub client (½ day)

- `GitHubOptions` (BaseUrl, Owner, Repo, Token, PerPage=10, TimeoutSeconds) bound from config, validated on start.
- `IGitHubClient` / `GitHubClient` via `IHttpClientFactory` typed client:
  - `GET /repos/{owner}/{repo}/commits?per_page=10`
  - Headers: `Authorization: Bearer {token}`, `User-Agent: IntegrationTaskApp`, `Accept: application/vnd.github+json`, `X-GitHub-Api-Version`
  - Map 401 → Unauthorized, 403 + `X-RateLimit-Remaining: 0` → RateLimited, 404 → NotFound (also returned for private repos without access), `TaskCanceledException` → Timeout.
- `ICommitCache` / `JsonFileCommitCache` (singleton, thread-safe via lock / `SemaphoreSlim`): `Get()`, `Set(commits, lastSyncTime)`; loads file on startup.
- `GitHubCommitService : IGitHubCommitService`
  - `GetLiveCommits` → GitHub client, tag `Source=Live`
  - `GetScheduledCommits` → cache, tag `Source=Scheduled`, return `LastSyncTime` (empty list + null time before first sync)
- Expose endpoint `http://localhost:5100/GitHubCommitService.svc` + WSDL (`ServiceMetadataBehavior`).
- Serilog/console logging.

## Phase 3 — Quartz job (2 h)

- `Quartz.Extensions.Hosting`; `SyncCommitsJob` with `[DisallowConcurrentExecution]`.
- Trigger: `WithSimpleSchedule(x => x.WithIntervalInSeconds(60).RepeatForever())` + `StartNow()` so data exists immediately.
- Steps: call GitHub → take 10 → `cache.Set(commits, DateTimeOffset.UtcNow)` → log.
- On failure: log, **keep last good cache** (don't wipe), record `LastError` / `LastAttemptTime` for diagnostics.

## Phase 4 — ASP.NET Core API (½ day)

- `GitHubController`: `GET /api/github/live`, `GET /api/github/scheduled`.
- `ICommitServiceClient` wrapper around `ChannelFactory<IGitHubCommitService>` (singleton factory, channel per call, proper close/abort), endpoint + `SendTimeout` from config.
- DTOs: `CommitResponseDto`, `ScheduledCommitsResponseDto { LastSyncTime, Commits }`.
- `ApiResponse<T> { success, message, data }` for every response, including errors.
- Global exception middleware / `IExceptionHandler`:
  - `FaultException<GitHubFault>` Unauthorized → 401 "GitHub authentication failed — check token"
  - RateLimited → 429, NotFound → 404, Timeout / `TimeoutException` → 504
  - `EndpointNotFoundException` / `CommunicationException` → 503 "WCF service unavailable"
- CORS for `http://localhost:4200`, Swagger/OpenAPI, structured logging, health check.

> Decision: a GitHub 401 is passed through as HTTP 401 with
> `success=false, message="GitHub authentication failed (401)..."`. The Angular interceptor shows it in the
> Kendo error dialog (there's no app login, so it won't be treated as a session expiry).

## Phase 5 — Angular front-end (1 day)

- Angular 19 standalone components, `provideHttpClient`, environment-based API URL.
- Kendo packages: `@progress/kendo-angular-grid`, `-dialog`, `-buttons`, `-indicators` (loader), `@progress/kendo-theme-default`.
  Kendo requires a license key (trial is fine) — activate with `npx kendo-ui-license activate`.
- `GitHubMonitorService` — typed calls returning `ApiResponse<T>`; HTTP error interceptor unwraps the envelope.
- `GitHubRepositoryMonitorComponent` (page title "GitHub Repository Monitor"):
  - **Live Commits** card: "Refresh Live Data" button (loading state), Kendo Grid.
  - **Scheduled Commits** card: Kendo Grid, "Last Sync: …" badge, refresh button, auto-poll every 60 s (RxJS `timer`).
  - Grid columns: Commit Message, Author, Date (`date` format), Commit SHA (short, link to GitHub, full in tooltip), Source (chip).
  - Sorting, paging (pageSize 10), column resizing.
- Errors → shared `kendo-dialog` component showing `message` from the envelope.
- Responsive: CSS grid; side-by-side on wide screens, stacked < 1024 px; secondary columns hidden on mobile via `[media]` on Kendo columns.

## Phase 6 — Tests, README, polish (½ day)

- Unit tests: GitHub client status mapping (mock `HttpMessageHandler`), cache persistence, job behaviour on failure, API exception → envelope mapping.
- README: prerequisites, setup token (user-secrets), run order (WCF → API → UI), ports, architecture diagram, auth handling, assumptions.
- Optional: `run-all.ps1` to start the three processes.

## Ports

| Component | URL |
|---|---|
| WCF service | http://localhost:5100/GitHubCommitService.svc |
| API | http://localhost:5200 (Swagger at /swagger) |
| Angular | http://localhost:4200 |

## Assumptions / open questions

- Demo repo: `ahmedabdelbadie/integration-task-private-repo` (private). Fine-grained PAT, Contents + Metadata read-only,
  verified 2026-11-07 (HTTP 200, 10+ commits). Token stored only via `dotnet user-secrets`, never in files.
- "Latest 10 commits" = default branch, ordered by GitHub (`per_page=10`).
- Single-instance deployment; in-memory + JSON cache is sufficient (no DB).
