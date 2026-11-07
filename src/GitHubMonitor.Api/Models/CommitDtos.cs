namespace GitHubMonitor.Api.Models;

public enum CommitSourceDto
{
    Live,
    Scheduled
}

/// <summary>One row of the Kendo grid.</summary>
public class CommitResponseDto
{
    public string Sha { get; init; } = string.Empty;
    public string ShortSha { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string? AuthorLogin { get; init; }
    public DateTimeOffset Date { get; init; }
    public string? Url { get; init; }
    public CommitSourceDto Source { get; init; }
}

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
