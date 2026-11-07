using System.Runtime.Serialization;

namespace GitHubMonitor.Contracts;

[DataContract(Namespace = ContractNamespaces.Data)]
public enum CommitSource
{
    [EnumMember] Live = 0,
    [EnumMember] Scheduled = 1
}
