using GitHubMonitor.Contracts;

namespace GitHubMonitor.Api.Wcf;

/// <summary>Thin wrapper over the WCF channel so callers never deal with channel lifetime.</summary>
public interface ICommitServiceClient
{
    Task<CommitsResult> GetLiveCommitsAsync();
    Task<CommitsResult> GetScheduledCommitsAsync();
}
