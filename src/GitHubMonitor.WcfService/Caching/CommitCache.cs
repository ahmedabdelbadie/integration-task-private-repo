using System.Text.Json;
using GitHubMonitor.Contracts;
using GitHubMonitor.WcfService.Options;
using Microsoft.Extensions.Options;

namespace GitHubMonitor.WcfService.Caching;

/// <summary>Immutable view of what the Quartz job last stored.</summary>
public sealed record CommitCacheSnapshot(
    IReadOnlyList<CommitDto> Commits,
    DateTimeOffset? LastSyncTime,
    DateTimeOffset? LastAttemptTime,
    string? LastError)
{
    public static CommitCacheSnapshot Empty { get; } = new(Array.Empty<CommitDto>(), null, null, null);
}

public interface ICommitCache
{
    CommitCacheSnapshot Get();

    /// <summary>Replaces the cached commits and stamps LastSyncTime.</summary>
    Task SaveSuccessAsync(IReadOnlyList<CommitDto> commits, DateTimeOffset syncTime, CancellationToken cancellationToken = default);

    /// <summary>Records a failed sync while keeping the last good commits.</summary>
    Task SaveFailureAsync(string error, DateTimeOffset attemptTime, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory cache backed by a JSON file so the last sync survives service restarts.
/// Reads are lock-free (snapshot swap); writes are serialized.
/// </summary>
public sealed class JsonFileCommitCache : ICommitCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<JsonFileCommitCache> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile CommitCacheSnapshot _snapshot;

    public JsonFileCommitCache(IOptions<CacheOptions> options, IHostEnvironment environment, ILogger<JsonFileCommitCache> logger)
        : this(Path.GetFullPath(options.Value.FilePath, environment.ContentRootPath), logger)
    {
    }

    public JsonFileCommitCache(string filePath, ILogger<JsonFileCommitCache> logger)
    {
        _filePath = filePath;
        _logger = logger;
        _snapshot = Load();
    }

    public CommitCacheSnapshot Get() => _snapshot;

    public Task SaveSuccessAsync(IReadOnlyList<CommitDto> commits, DateTimeOffset syncTime, CancellationToken cancellationToken = default)
        => WriteAsync(_ => new CommitCacheSnapshot(commits.ToList(), syncTime, syncTime, null), cancellationToken);

    public Task SaveFailureAsync(string error, DateTimeOffset attemptTime, CancellationToken cancellationToken = default)
        => WriteAsync(current => current with { LastAttemptTime = attemptTime, LastError = error }, cancellationToken);

    private async Task WriteAsync(Func<CommitCacheSnapshot, CommitCacheSnapshot> update, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var next = update(_snapshot);
            _snapshot = next;
            await PersistAsync(next, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task PersistAsync(CommitCacheSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The in-memory cache is still valid; persistence is best-effort.
            _logger.LogWarning(ex, "Could not persist commit cache to {Path}", _filePath);
        }
    }

    private CommitCacheSnapshot Load()
    {
        if (!File.Exists(_filePath))
        {
            return CommitCacheSnapshot.Empty;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize<CommitCacheSnapshot>(json, JsonOptions);
            _logger.LogInformation("Loaded {Count} cached commits from {Path} (last sync {LastSync})",
                snapshot?.Commits.Count ?? 0, _filePath, snapshot?.LastSyncTime);
            return snapshot ?? CommitCacheSnapshot.Empty;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Ignoring unreadable commit cache file {Path}", _filePath);
            return CommitCacheSnapshot.Empty;
        }
    }
}
