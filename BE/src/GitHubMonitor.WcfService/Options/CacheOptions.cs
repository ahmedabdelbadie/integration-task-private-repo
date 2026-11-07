using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.WcfService.Options;

public class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>JSON cache file; relative paths resolve against the content root.</summary>
    [Required]
    public string FilePath { get; set; } = "App_Data/commits-cache.json";
}
