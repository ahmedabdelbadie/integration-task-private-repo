using GitHubMonitor.Contracts;

namespace GitHubMonitor.WcfService.GitHub;

public class GitHubApiException : Exception
{
    public GitHubApiException(GitHubErrorCode code, string message, int? upstreamStatusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        UpstreamStatusCode = upstreamStatusCode;
    }

    public GitHubErrorCode Code { get; }
    public int? UpstreamStatusCode { get; }

    public GitHubFault ToFault() => new()
    {
        Code = Code,
        Message = Message,
        UpstreamStatusCode = UpstreamStatusCode
    };
}
