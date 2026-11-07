namespace GitHubMonitor.Api.Models;

public class CommitsResponseDto
{
    public CommitSourceDto Source { get; init; }
    public int Count { get; init; }

    /// <summary>Time of the last successful Quartz sync (scheduled results only).</summary>
    public DateTimeOffset? LastSyncTime { get; init; }

    /// <summary>Set when the most recent Quartz sync failed; <see cref="Commits"/> then holds the last good data.</summary>
    public string? LastSyncError { get; init; }

    public IReadOnlyList<CommitResponseDto> Commits { get; init; } = Array.Empty<CommitResponseDto>();
}
