using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.Api.Wcf;

public class WcfClientOptions
{
    public const string SectionName = "WcfService";

    [Required, Url]
    public string EndpointAddress { get; set; } = "http://localhost:5100/GitHubCommitService.svc";

    /// <summary>Must exceed the WCF service's own GitHub timeout so GitHub timeouts surface as typed faults.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
