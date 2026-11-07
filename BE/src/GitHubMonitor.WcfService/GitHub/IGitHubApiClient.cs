using GitHubMonitor.Contracts;

namespace GitHubMonitor.WcfService.GitHub;

public interface IGitHubApiClient
{
    /// <summary>Returns the latest commits of the configured repository, newest first.</summary>
    /// <exception cref="GitHubApiException">GitHub could not be reached or rejected the request.</exception>
    Task<IReadOnlyList<CommitDto>> GetLatestCommitsAsync(CancellationToken cancellationToken = default);
}
