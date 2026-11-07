using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GitHubMonitor.Contracts;
using GitHubMonitor.WcfService.GitHub.Responses;
using GitHubMonitor.WcfService.Options;
using Microsoft.Extensions.Options;

namespace GitHubMonitor.WcfService.GitHub;

public class GitHubApiClient : IGitHubApiClient
{
    private readonly HttpClient _http;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubApiClient> _logger;

    public GitHubApiClient(HttpClient http, IOptions<GitHubOptions> options, ILogger<GitHubApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CommitDto>> GetLatestCommitsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new GitHubApiException(GitHubErrorCode.Unauthorized,
                "GitHub token is not configured. Set 'GitHub:Token' via user-secrets or the GitHub__Token environment variable.");
        }

        var path = $"repos/{Uri.EscapeDataString(_options.Owner)}/{Uri.EscapeDataString(_options.Repository)}/commits?per_page={_options.CommitCount}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed (not a caller cancellation).
            throw new GitHubApiException(GitHubErrorCode.Timeout,
                $"GitHub did not respond within {_options.TimeoutSeconds} seconds.", null, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubApiException(GitHubErrorCode.UpstreamError,
                $"Could not reach GitHub: {ex.Message}", null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw CreateException(response);
            }

            var commits = await response.Content.ReadFromJsonAsync<List<GitHubCommitResponse>>(cancellationToken)
                          ?? new List<GitHubCommitResponse>();

            _logger.LogDebug("Fetched {Count} commits from {Owner}/{Repo}", commits.Count, _options.Owner, _options.Repository);

            return commits
                .Take(_options.CommitCount)
                .Select(Map)
                .ToList();
        }
    }

    private GitHubApiException CreateException(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        var repo = $"{_options.Owner}/{_options.Repository}";

        var rateLimitExhausted = response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
                                 && remaining.FirstOrDefault() == "0";

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new GitHubApiException(GitHubErrorCode.Unauthorized,
                "GitHub rejected the access token (401). The token may be invalid, expired or revoked.", status),
            HttpStatusCode.TooManyRequests => RateLimited(response, status),
            HttpStatusCode.Forbidden when rateLimitExhausted => RateLimited(response, status),
            HttpStatusCode.Forbidden => new GitHubApiException(GitHubErrorCode.Forbidden,
                $"The access token is not permitted to read {repo} (403). Grant 'Contents: Read-only' to the token.", status),
            HttpStatusCode.NotFound => new GitHubApiException(GitHubErrorCode.NotFound,
                $"Repository {repo} was not found (404). For private repositories this also means the token has no access to it.", status),
            _ => new GitHubApiException(GitHubErrorCode.UpstreamError,
                $"GitHub returned an unexpected status {status} ({response.ReasonPhrase}).", status)
        };
    }

    private static GitHubApiException RateLimited(HttpResponseMessage response, int status)
    {
        var message = "GitHub API rate limit exceeded.";
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset)
            && long.TryParse(reset.FirstOrDefault(), out var epoch))
        {
            message += $" Resets at {DateTimeOffset.FromUnixTimeSeconds(epoch):u}.";
        }
        return new GitHubApiException(GitHubErrorCode.RateLimited, message, status);
    }

    private static CommitDto Map(GitHubCommitResponse c) => new()
    {
        Sha = c.Sha,
        Message = c.Commit?.Message ?? string.Empty,
        AuthorName = c.Commit?.Author?.Name ?? c.Author?.Login ?? "unknown",
        AuthorLogin = c.Author?.Login,
        Date = c.Commit?.Author?.Date ?? c.Commit?.Committer?.Date ?? DateTimeOffset.MinValue,
        HtmlUrl = c.HtmlUrl
    };
}
