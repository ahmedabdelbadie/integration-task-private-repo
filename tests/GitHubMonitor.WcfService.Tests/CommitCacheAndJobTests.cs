using GitHubMonitor.Contracts;
using GitHubMonitor.WcfService.Caching;
using GitHubMonitor.WcfService.GitHub;
using GitHubMonitor.WcfService.Jobs;
using GitHubMonitor.WcfService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace GitHubMonitor.WcfService.Tests;

public sealed class CommitCacheAndJobTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 17, 30, 0, TimeSpan.Zero);

    private readonly string _cacheFile = Path.Combine(Path.GetTempPath(), $"commits-{Guid.NewGuid():N}.json");
    private readonly FakeTimeProvider _time = new(Now);
    private readonly IGitHubApiClient _gitHub = Substitute.For<IGitHubApiClient>();

    public void Dispose() => File.Delete(_cacheFile);

    [Fact]
    public async Task Cache_persists_to_disk_and_reloads()
    {
        var cache = NewCache();
        await cache.SaveSuccessAsync(SampleCommits(3), Now);

        var reloaded = NewCache().Get();

        Assert.Equal(3, reloaded.Commits.Count);
        Assert.Equal(Now, reloaded.LastSyncTime);
        Assert.Null(reloaded.LastError);
    }

    [Fact]
    public async Task Job_success_caches_commits_and_sets_LastSyncTime()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>()).Returns(SampleCommits(10));
        var cache = NewCache();

        await NewJob(cache).Execute(JobContext());

        var snapshot = cache.Get();
        Assert.Equal(10, snapshot.Commits.Count);
        Assert.Equal(Now, snapshot.LastSyncTime);
    }

    [Fact]
    public async Task Job_failure_keeps_last_good_commits_and_records_error()
    {
        var cache = NewCache();
        await cache.SaveSuccessAsync(SampleCommits(10), Now);

        _time.Advance(TimeSpan.FromMinutes(1));
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitHubApiException(GitHubErrorCode.Unauthorized, "bad token", 401));

        await NewJob(cache).Execute(JobContext());

        var snapshot = cache.Get();
        Assert.Equal(10, snapshot.Commits.Count);
        Assert.Equal(Now, snapshot.LastSyncTime);
        Assert.Equal(Now.AddMinutes(1), snapshot.LastAttemptTime);
        Assert.Equal("bad token", snapshot.LastError);
    }

    [Fact]
    public async Task Service_tags_scheduled_commits_and_returns_LastSyncTime()
    {
        var cache = NewCache();
        await cache.SaveSuccessAsync(SampleCommits(2), Now);
        var service = new GitHubCommitService(_gitHub, cache, NullLogger<GitHubCommitService>.Instance);

        var result = await service.GetScheduledCommits();

        Assert.Equal(CommitSource.Scheduled, result.Source);
        Assert.Equal(Now, result.LastSyncTime);
        Assert.All(result.Commits, c => Assert.Equal(CommitSource.Scheduled, c.Source));
    }

    [Fact]
    public async Task Service_turns_GitHub_errors_into_typed_faults()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitHubApiException(GitHubErrorCode.Unauthorized, "bad token", 401));
        var service = new GitHubCommitService(_gitHub, NewCache(), NullLogger<GitHubCommitService>.Instance);

        var fault = await Assert.ThrowsAsync<CoreWCF.FaultException<GitHubFault>>(() => service.GetLiveCommits());

        Assert.Equal(GitHubErrorCode.Unauthorized, fault.Detail.Code);
        Assert.Equal(401, fault.Detail.UpstreamStatusCode);
    }

    private JsonFileCommitCache NewCache() => new(_cacheFile, NullLogger<JsonFileCommitCache>.Instance);

    private SyncCommitsJob NewJob(ICommitCache cache) =>
        new(_gitHub, cache, _time, NullLogger<SyncCommitsJob>.Instance);

    private static IJobExecutionContext JobContext()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private static IReadOnlyList<CommitDto> SampleCommits(int count) =>
        Enumerable.Range(1, count).Select(i => new CommitDto
        {
            Sha = $"sha{i:D3}",
            Message = $"Commit {i}",
            AuthorName = "Tester",
            Date = Now.AddMinutes(-i)
        }).ToList();
}
