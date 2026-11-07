using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using GitHubMonitor.Contracts;
using GitHubMonitor.WcfService.Caching;
using GitHubMonitor.WcfService.GitHub;
using GitHubMonitor.WcfService.Jobs;
using GitHubMonitor.WcfService.Options;
using GitHubMonitor.WcfService.Services;
using Microsoft.Extensions.Options;
using Quartz;

const string ServicePath = "/GitHubCommitService.svc";

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ---------------------------------------------------------
builder.Services.AddOptions<GitHubOptions>()
    .Bind(builder.Configuration.GetSection(GitHubOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SchedulerOptions>()
    .Bind(builder.Configuration.GetSection(SchedulerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CacheOptions>()
    .Bind(builder.Configuration.GetSection(CacheOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---- GitHub client + cache -------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICommitCache, JsonFileCommitCache>();
builder.Services.AddHttpClient<IGitHubApiClient, GitHubApiClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
    http.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
});

// ---- Quartz scheduler ------------------------------------------------------
var interval = TimeSpan.FromSeconds(
    builder.Configuration.GetValue<int?>($"{SchedulerOptions.SectionName}:{nameof(SchedulerOptions.IntervalSeconds)}") ?? 60);

builder.Services.AddQuartz(q =>
{
    q.AddJob<SyncCommitsJob>(SyncCommitsJob.Key);
    q.AddTrigger(t => t
        .ForJob(SyncCommitsJob.Key)
        .WithIdentity("sync-github-commits-trigger", "github")
        .StartNow() // populate the cache immediately on startup
        .WithSimpleSchedule(s => s.WithInterval(interval).RepeatForever()));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

// ---- CoreWCF ---------------------------------------------------------------
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddTransient<GitHubCommitService>();

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<GitHubCommitService>(o => o.DebugBehavior.IncludeExceptionDetailInFaults = app.Environment.IsDevelopment());
    serviceBuilder.AddServiceEndpoint<GitHubCommitService, IGitHubCommitService>(
        new BasicHttpBinding(), ServicePath);

    var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    metadata.HttpGetEnabled = true; // WSDL at {ServicePath}?wsdl
});

app.MapGet("/", () => Results.Ok(new
{
    service = "GitHubMonitor WCF Service",
    endpoint = ServicePath,
    wsdl = ServicePath + "?wsdl"
}));

app.MapGet("/health", (ICommitCache cache) =>
{
    var snapshot = cache.Get();
    return Results.Ok(new
    {
        status = "ok",
        cachedCommits = snapshot.Commits.Count,
        snapshot.LastSyncTime,
        snapshot.LastAttemptTime,
        snapshot.LastError
    });
});

app.Run();
