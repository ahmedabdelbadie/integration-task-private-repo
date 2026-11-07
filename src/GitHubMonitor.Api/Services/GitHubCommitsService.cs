using System.Diagnostics;
using GitHubMonitor.Api.Models;
using GitHubMonitor.Api.Wcf;
using GitHubMonitor.Contracts;

namespace GitHubMonitor.Api.Services;

public interface IGitHubCommitsService
{
    Task<CommitsResponseDto> GetLiveAsync();
    Task<CommitsResponseDto> GetScheduledAsync();
}

/// <summary>Calls the WCF service and maps its data contracts to API DTOs.</summary>
public class GitHubCommitsService : IGitHubCommitsService
{
    private readonly ICommitServiceClient _client;
    private readonly ILogger<GitHubCommitsService> _logger;

    public GitHubCommitsService(ICommitServiceClient client, ILogger<GitHubCommitsService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<CommitsResponseDto> GetLiveAsync() =>
        CallAsync(nameof(IGitHubCommitService.GetLiveCommits), _client.GetLiveCommitsAsync);

    public Task<CommitsResponseDto> GetScheduledAsync() =>
        CallAsync(nameof(IGitHubCommitService.GetScheduledCommits), _client.GetScheduledCommitsAsync);

    private async Task<CommitsResponseDto> CallAsync(string operation, Func<Task<CommitsResult>> call)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await call();
        _logger.LogInformation("WCF {Operation} returned {Count} commits in {ElapsedMs} ms",
            operation, result.Commits.Count, stopwatch.ElapsedMilliseconds);
        return Map(result);
    }

    internal static CommitsResponseDto Map(CommitsResult result) => new()
    {
        Source = Map(result.Source),
        Count = result.Commits.Count,
        LastSyncTime = result.LastSyncTime,
        LastSyncError = result.LastSyncError,
        Commits = result.Commits.Select(c => new CommitResponseDto
        {
            Sha = c.Sha,
            ShortSha = c.Sha.Length > 7 ? c.Sha[..7] : c.Sha,
            Message = c.Message,
            Author = c.AuthorName,
            AuthorLogin = c.AuthorLogin,
            Date = c.Date,
            Url = c.HtmlUrl,
            Source = Map(c.Source)
        }).ToList()
    };

    private static CommitSourceDto Map(CommitSource source) =>
        source == CommitSource.Scheduled ? CommitSourceDto.Scheduled : CommitSourceDto.Live;
}
