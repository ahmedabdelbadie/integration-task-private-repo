using System.Text.Json.Serialization;

namespace GitHubMonitor.WcfService.GitHub.Responses;

internal sealed class GitActor
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset? Date { get; set; }
}
