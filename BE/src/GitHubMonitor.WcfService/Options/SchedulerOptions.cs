using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.WcfService.Options;

public class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    [Range(10, 86400)]
    public int IntervalSeconds { get; set; } = 60;
}
