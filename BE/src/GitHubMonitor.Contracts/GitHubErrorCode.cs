using System.Runtime.Serialization;

namespace GitHubMonitor.Contracts;

[DataContract(Namespace = ContractNamespaces.Data)]
public enum GitHubErrorCode
{
    [EnumMember] Unknown = 0,
    [EnumMember] Unauthorized = 1,
    [EnumMember] Forbidden = 2,
    [EnumMember] RateLimited = 3,
    [EnumMember] NotFound = 4,
    [EnumMember] Timeout = 5,
    [EnumMember] UpstreamError = 6
}
