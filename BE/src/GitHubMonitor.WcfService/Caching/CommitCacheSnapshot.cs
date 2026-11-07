using GitHubMonitor.Contracts;

namespace GitHubMonitor.WcfService.Caching;

/// <summary>Immutable view of what the Quartz job last stored.</summary>
public sealed record CommitCacheSnapshot(
    IReadOnlyList<CommitDto> Commits,
    DateTimeOffset? LastSyncTime,
    DateTimeOffset? LastAttemptTime,
    string? LastError)
{
    public static CommitCacheSnapshot Empty { get; } = new(Array.Empty<CommitDto>(), null, null, null);
}
