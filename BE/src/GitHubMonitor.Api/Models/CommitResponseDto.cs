namespace GitHubMonitor.Api.Models;

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
