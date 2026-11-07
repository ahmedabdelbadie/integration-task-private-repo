using System.Runtime.Serialization;

namespace GitHubMonitor.Contracts;

[DataContract(Namespace = ContractNamespaces.Data)]
public enum CommitSource
{
    [EnumMember] Live = 0,
    [EnumMember] Scheduled = 1
}

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

[DataContract(Namespace = ContractNamespaces.Data)]
public class CommitsResult
{
    [DataMember(Order = 1)] public List<CommitDto> Commits { get; set; } = new();
    [DataMember(Order = 2)] public CommitSource Source { get; set; }

    /// <summary>When the Quartz job last synced successfully. Null for live results or before the first sync.</summary>
    [DataMember(Order = 3)] public DateTimeOffset? LastSyncTime { get; set; }

    /// <summary>Error from the most recent sync attempt, if it failed (cached data is still returned).</summary>
    [DataMember(Order = 4)] public string? LastSyncError { get; set; }
}

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

/// <summary>Typed SOAP fault describing why a GitHub call failed.</summary>
[DataContract(Namespace = ContractNamespaces.Data)]
public class GitHubFault
{
    [DataMember(Order = 1)] public GitHubErrorCode Code { get; set; }
    [DataMember(Order = 2)] public string Message { get; set; } = string.Empty;
    [DataMember(Order = 3)] public int? UpstreamStatusCode { get; set; }
}
