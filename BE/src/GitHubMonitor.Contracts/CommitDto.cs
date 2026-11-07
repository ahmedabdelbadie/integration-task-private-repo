using System.Runtime.Serialization;

namespace GitHubMonitor.Contracts;

[DataContract(Namespace = ContractNamespaces.Data)]
public class CommitDto
{
    [DataMember(Order = 1)] public string Sha { get; set; } = string.Empty;
    [DataMember(Order = 2)] public string Message { get; set; } = string.Empty;
    [DataMember(Order = 3)] public string AuthorName { get; set; } = string.Empty;
    [DataMember(Order = 4)] public string? AuthorLogin { get; set; }
    [DataMember(Order = 5)] public DateTimeOffset Date { get; set; }
    [DataMember(Order = 6)] public string? HtmlUrl { get; set; }
    [DataMember(Order = 7)] public CommitSource Source { get; set; }
}
