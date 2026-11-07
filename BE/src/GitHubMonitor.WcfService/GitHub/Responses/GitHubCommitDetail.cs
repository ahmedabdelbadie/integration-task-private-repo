using System.Text.Json.Serialization;

namespace GitHubMonitor.WcfService.GitHub.Responses;

internal sealed class GitHubCommitDetail
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("author")] public GitActor? Author { get; set; }
    [JsonPropertyName("committer")] public GitActor? Committer { get; set; }
}
