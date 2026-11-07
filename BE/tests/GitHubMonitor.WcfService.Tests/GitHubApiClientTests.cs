using System.Net;
using GitHubMonitor.Contracts;
using GitHubMonitor.WcfService.GitHub;
using GitHubMonitor.WcfService.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitHubMonitor.WcfService.Tests;

public class GitHubApiClientTests
{
    private const string CommitsJson = """
        [
          {
            "sha": "21b5c2ae5e370ee99f8cf6a5bafcb5148eb7bbda",
            "html_url": "https://github.com/o/r/commit/21b5c2a",
            "commit": {
              "message": "Add Commit Number 11 to README",
              "author": { "name": "Ahmed Badea", "date": "2026-11-07T17:12:04Z" },
              "committer": { "name": "GitHub", "date": "2026-11-07T17:12:05Z" }
            },
            "author": { "login": "ahmedabdelbadie" }
          },
          {
            "sha": "5f42093d7ed4144965edbf24a7ede13bbc3113a7",
            "html_url": null,
            "commit": { "message": "Second", "author": { "name": "Someone", "date": "2026-11-07T17:11:50Z" } },
            "author": null
          }
        ]
        """;

    [Fact]
    public async Task Returns_mapped_commits_and_sends_required_headers()
    {
        var handler = new StubHandler(HttpStatusCode.OK, CommitsJson);
        var client = CreateClient(handler);

        var commits = await client.GetLatestCommitsAsync();

        Assert.Equal(2, commits.Count);
        var first = commits[0];
        Assert.Equal("21b5c2ae5e370ee99f8cf6a5bafcb5148eb7bbda", first.Sha);
        Assert.Equal("Add Commit Number 11 to README", first.Message);
        Assert.Equal("Ahmed Badea", first.AuthorName);
        Assert.Equal("ahmedabdelbadie", first.AuthorLogin);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 17, 12, 4, TimeSpan.Zero), first.Date);
        Assert.Null(commits[1].AuthorLogin);

        var request = handler.LastRequest!;
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
        Assert.Contains("IntegrationTaskApp", request.Headers.UserAgent.ToString());
        Assert.Equal("https://api.github.com/repos/owner/repo/commits?per_page=10", request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubErrorCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubErrorCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, GitHubErrorCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, GitHubErrorCode.UpstreamError)]
    public async Task Maps_error_status_codes(HttpStatusCode status, GitHubErrorCode expected)
    {
        var client = CreateClient(new StubHandler(status, "{}"));

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(expected, ex.Code);
        Assert.Equal((int)status, ex.UpstreamStatusCode);
    }

    [Fact]
    public async Task Forbidden_with_exhausted_rate_limit_is_reported_as_rate_limited()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden, "{}", r =>
        {
            r.Headers.Add("X-RateLimit-Remaining", "0");
            r.Headers.Add("X-RateLimit-Reset", "1793000000");
        });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(GitHubErrorCode.RateLimited, ex.Code);
        Assert.Contains("Resets at", ex.Message);
    }

    [Fact]
    public async Task Timeout_is_reported_as_timeout()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timed out", new TimeoutException()));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(GitHubErrorCode.Timeout, ex.Code);
    }

    [Fact]
    public async Task Network_failure_is_reported_as_upstream_error()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("DNS failure"));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(GitHubErrorCode.UpstreamError, ex.Code);
    }

    [Fact]
    public async Task Missing_token_fails_as_unauthorized_without_calling_github()
    {
        var handler = new StubHandler(HttpStatusCode.OK, CommitsJson);
        var client = CreateClient(handler, token: null);

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(GitHubErrorCode.Unauthorized, ex.Code);
        Assert.Null(handler.LastRequest);
    }

    private static GitHubApiClient CreateClient(StubHandler handler, string? token = "test-token")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("IntegrationTaskApp");
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubOptions
        {
            Owner = "owner",
            Repository = "repo",
            Token = token
        });
        return new GitHubApiClient(http, options, NullLogger<GitHubApiClient>.Instance);
    }
}
