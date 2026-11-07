using System.Net;
using System.Net.Http.Json;
using System.ServiceModel;
using System.Text.Json;
using GitHubMonitor.Api.Wcf;
using GitHubMonitor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GitHubMonitor.Api.Tests;

public class GitHubEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ICommitServiceClient _wcf = Substitute.For<ICommitServiceClient>();

    public GitHubEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<ICommitServiceClient>();
            services.AddSingleton(_wcf);
        }));
    }

    [Fact]
    public async Task Live_returns_envelope_with_commits()
    {
        _wcf.GetLiveCommitsAsync().Returns(new CommitsResult
        {
            Source = CommitSource.Live,
            Commits = [new CommitDto { Sha = "21b5c2ae5e370ee9", Message = "Hello", AuthorName = "Ahmed", Source = CommitSource.Live }]
        });

        var response = await _factory.CreateClient().GetAsync("/api/github/live");
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("success").GetBoolean());
        var commit = body.GetProperty("data").GetProperty("commits")[0];
        Assert.Equal("21b5c2a", commit.GetProperty("shortSha").GetString());
        Assert.Equal("Ahmed", commit.GetProperty("author").GetString());
        Assert.Equal("Live", commit.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Scheduled_returns_LastSyncTime()
    {
        var syncTime = new DateTimeOffset(2026, 11, 7, 17, 30, 0, TimeSpan.Zero);
        _wcf.GetScheduledCommitsAsync().Returns(new CommitsResult
        {
            Source = CommitSource.Scheduled,
            LastSyncTime = syncTime,
            Commits = [new CommitDto { Sha = "abc", Message = "Hi", AuthorName = "A", Source = CommitSource.Scheduled }]
        });

        var response = await _factory.CreateClient().GetAsync("/api/github/scheduled");
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(syncTime, body.GetProperty("data").GetProperty("lastSyncTime").GetDateTimeOffset());
        Assert.Equal("Scheduled", body.GetProperty("data").GetProperty("source").GetString());
    }

    [Fact]
    public async Task GitHub_401_is_passed_through_in_envelope()
    {
        _wcf.GetLiveCommitsAsync().ThrowsAsync(new FaultException<GitHubFault>(
            new GitHubFault { Code = GitHubErrorCode.Unauthorized, Message = "GitHub rejected the access token (401).", UpstreamStatusCode = 401 },
            new FaultReason("unauthorized")));

        var response = await _factory.CreateClient().GetAsync("/api/github/live");
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("GitHub rejected the access token (401).", body.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Wcf_timeout_returns_504_envelope()
    {
        _wcf.GetLiveCommitsAsync().ThrowsAsync(new TimeoutException("send timeout"));

        var response = await _factory.CreateClient().GetAsync("/api/github/live");
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Wcf_unreachable_returns_503_envelope()
    {
        _wcf.GetScheduledCommitsAsync().ThrowsAsync(new CommunicationException("refused",
            new HttpRequestException("refused", new System.Net.Sockets.SocketException(10061))));

        var response = await _factory.CreateClient().GetAsync("/api/github/scheduled");
        var body = await ReadJson(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("unavailable", body.GetProperty("message").GetString());
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
