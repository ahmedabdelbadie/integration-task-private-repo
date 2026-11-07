using System.Text.Json.Serialization;

namespace GitHubMonitor.WcfService.GitHub.Responses;

/// <summary>Minimal shape of one item returned by GET /repos/{owner}/{repo}/commits.</summary>
internal sealed class GitHubCommitResponse
{
    [JsonPropertyName("sha")] public string Sha { get; set; } = string.Empty;
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    [JsonPropertyName("commit")] public GitHubCommitDetail? Commit { get; set; }
    [JsonPropertyName("author")] public GitHubUser? Author { get; set; }
}
