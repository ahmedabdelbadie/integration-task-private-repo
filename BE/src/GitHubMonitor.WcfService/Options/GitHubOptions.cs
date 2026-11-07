using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.WcfService.Options;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    [Required, Url]
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    [Required]
    public string Owner { get; set; } = string.Empty;

    [Required]
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// Fine-grained PAT (Contents: read-only). Supplied via user-secrets or the GitHub__Token
    /// environment variable — never committed. Optional at startup so the service can still
    /// report a clean "not configured" authentication error.
    /// </summary>
    public string? Token { get; set; }

    [Required]
    public string UserAgent { get; set; } = "IntegrationTaskApp";

    [Range(1, 100)]
    public int CommitCount { get; set; } = 10;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 15;
}
