using System.Text.Json.Serialization;

namespace GitHubMonitor.WcfService.GitHub.Responses;

internal sealed class GitHubUser
{
    [JsonPropertyName("login")] public string? Login { get; set; }
}
