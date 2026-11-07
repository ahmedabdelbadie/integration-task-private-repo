using GitHubMonitor.WcfService.Caching;
using GitHubMonitor.WcfService.GitHub;
using Quartz;

namespace GitHubMonitor.WcfService.Jobs;

/// <summary>
/// Quartz job: GitHub API → latest commits → cache (+ LastSyncTime).
/// </summary>
[DisallowConcurrentExecution]
public class SyncCommitsJob : IJob
{
    public static readonly JobKey Key = new("sync-github-commits", "github");

    private readonly IGitHubApiClient _gitHub;
    private readonly ICommitCache _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<SyncCommitsJob> _logger;

    public SyncCommitsJob(IGitHubApiClient gitHub, ICommitCache cache, TimeProvider time, ILogger<SyncCommitsJob> logger)
    {
        _gitHub = gitHub;
        _cache = cache;
        _time = time;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        try
        {
            var commits = await _gitHub.GetLatestCommitsAsync(ct);
            var syncTime = _time.GetUtcNow();
            await _cache.SaveSuccessAsync(commits, syncTime, ct);
            _logger.LogInformation("Commit sync succeeded: {Count} commits cached, LastSyncTime={LastSyncTime:o}",
                commits.Count, syncTime);
        }
        catch (GitHubApiException ex)
        {
            _logger.LogWarning("Commit sync failed ({Code}): {Message} Keeping previously cached commits.", ex.Code, ex.Message);
            await _cache.SaveFailureAsync(ex.Message, _time.GetUtcNow(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error during commit sync. Keeping previously cached commits.");
            await _cache.SaveFailureAsync("Unexpected error during sync.", _time.GetUtcNow(), ct);
        }
    }
}
