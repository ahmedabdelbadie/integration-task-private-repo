using System.Runtime.Serialization;

namespace GitHubMonitor.Contracts;

/// <summary>Typed SOAP fault describing why a GitHub call failed.</summary>
[DataContract(Namespace = ContractNamespaces.Data)]
public class GitHubFault
{
    [DataMember(Order = 1)] public GitHubErrorCode Code { get; set; }
    [DataMember(Order = 2)] public string Message { get; set; } = string.Empty;
    [DataMember(Order = 3)] public int? UpstreamStatusCode { get; set; }
}
