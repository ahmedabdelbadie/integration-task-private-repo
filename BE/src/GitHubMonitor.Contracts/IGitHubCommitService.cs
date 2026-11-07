using System.ServiceModel;

namespace GitHubMonitor.Contracts;

/// <summary>
/// WCF contract shared by the service (CoreWCF) and its client (ASP.NET Core API).
/// </summary>
[ServiceContract(Namespace = ContractNamespaces.Service, Name = "GitHubCommitService")]
public interface IGitHubCommitService
{
    /// <summary>Calls the GitHub API directly and returns the latest commits.</summary>
    [OperationContract]
    [FaultContract(typeof(GitHubFault))]
    Task<CommitsResult> GetLiveCommits();

    /// <summary>Returns the commits cached by the Quartz sync job.</summary>
    [OperationContract]
    [FaultContract(typeof(GitHubFault))]
    Task<CommitsResult> GetScheduledCommits();
}
