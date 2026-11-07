using System.Runtime.Serialization;

namespace GitHubMonitor.Contracts;

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
