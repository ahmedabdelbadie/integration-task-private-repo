using GitHubMonitor.Contracts;

namespace GitHubMonitor.WcfService.Caching;

public interface ICommitCache
{
    CommitCacheSnapshot Get();

    /// <summary>Replaces the cached commits and stamps LastSyncTime.</summary>
    Task SaveSuccessAsync(IReadOnlyList<CommitDto> commits, DateTimeOffset syncTime, CancellationToken cancellationToken = default);

    /// <summary>Records a failed sync while keeping the last good commits.</summary>
    Task SaveFailureAsync(string error, DateTimeOffset attemptTime, CancellationToken cancellationToken = default);
}
