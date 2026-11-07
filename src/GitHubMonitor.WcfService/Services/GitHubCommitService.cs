using CoreWCF;
using GitHubMonitor.Contracts;
using GitHubMonitor.WcfService.Caching;
using GitHubMonitor.WcfService.GitHub;

namespace GitHubMonitor.WcfService.Services;

/// <summary>CoreWCF implementation of <see cref="IGitHubCommitService"/>.</summary>
public class GitHubCommitService : IGitHubCommitService
{
    private readonly IGitHubApiClient _gitHub;
    private readonly ICommitCache _cache;
    private readonly ILogger<GitHubCommitService> _logger;

    public GitHubCommitService(IGitHubApiClient gitHub, ICommitCache cache, ILogger<GitHubCommitService> logger)
    {
        _gitHub = gitHub;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CommitsResult> GetLiveCommits()
    {
        _logger.LogInformation("GetLiveCommits called");
        try
        {
            var commits = await _gitHub.GetLatestCommitsAsync();
            return new CommitsResult
            {
                Source = CommitSource.Live,
                Commits = commits.Select(c => WithSource(c, CommitSource.Live)).ToList()
            };
        }
        catch (GitHubApiException ex)
        {
            _logger.LogWarning("GetLiveCommits failed ({Code}): {Message}", ex.Code, ex.Message);
            throw new FaultException<GitHubFault>(ex.ToFault(), new FaultReason(ex.Message));
        }
    }

    public Task<CommitsResult> GetScheduledCommits()
    {
        var snapshot = _cache.Get();
        _logger.LogInformation("GetScheduledCommits called: {Count} cached commits, LastSyncTime={LastSyncTime:o}",
            snapshot.Commits.Count, snapshot.LastSyncTime);

        return Task.FromResult(new CommitsResult
        {
            Source = CommitSource.Scheduled,
            LastSyncTime = snapshot.LastSyncTime,
            LastSyncError = snapshot.LastError,
            Commits = snapshot.Commits.Select(c => WithSource(c, CommitSource.Scheduled)).ToList()
        });
    }

    private static CommitDto WithSource(CommitDto c, CommitSource source) => new()
    {
        Sha = c.Sha,
        Message = c.Message,
        AuthorName = c.AuthorName,
        AuthorLogin = c.AuthorLogin,
        Date = c.Date,
        HtmlUrl = c.HtmlUrl,
        Source = source
    };
}
