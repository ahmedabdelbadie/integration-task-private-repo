using System.ComponentModel.DataAnnotations;
using System.ServiceModel;
using GitHubMonitor.Contracts;
using Microsoft.Extensions.Options;

namespace GitHubMonitor.Api.Wcf;

public class WcfClientOptions
{
    public const string SectionName = "WcfService";

    [Required, Url]
    public string EndpointAddress { get; set; } = "http://localhost:5100/GitHubCommitService.svc";

    /// <summary>Must exceed the WCF service's own GitHub timeout so GitHub timeouts surface as typed faults.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Thin wrapper over the WCF channel so callers never deal with channel lifetime.</summary>
public interface ICommitServiceClient
{
    Task<CommitsResult> GetLiveCommitsAsync();
    Task<CommitsResult> GetScheduledCommitsAsync();
}

/// <summary>
/// Holds one thread-safe <see cref="ChannelFactory{TChannel}"/> (expensive to build) and
/// creates a short-lived channel per call, closing it on success and aborting it on failure.
/// </summary>
public sealed class CommitServiceClient : ICommitServiceClient, IDisposable
{
    private readonly ChannelFactory<IGitHubCommitService> _factory;

    public CommitServiceClient(IOptions<WcfClientOptions> options)
    {
        var o = options.Value;
        var timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        var binding = new BasicHttpBinding
        {
            OpenTimeout = timeout,
            SendTimeout = timeout,
            ReceiveTimeout = timeout,
            CloseTimeout = timeout,
            MaxReceivedMessageSize = 1024 * 1024
        };
        _factory = new ChannelFactory<IGitHubCommitService>(binding, new EndpointAddress(o.EndpointAddress));
    }

    public Task<CommitsResult> GetLiveCommitsAsync() => InvokeAsync(s => s.GetLiveCommits());

    public Task<CommitsResult> GetScheduledCommitsAsync() => InvokeAsync(s => s.GetScheduledCommits());

    private async Task<T> InvokeAsync<T>(Func<IGitHubCommitService, Task<T>> call)
    {
        var channel = _factory.CreateChannel();
        var communication = (ICommunicationObject)channel;
        var closed = false;
        try
        {
            var result = await call(channel);
            communication.Close();
            closed = true;
            return result;
        }
        finally
        {
            if (!closed)
            {
                communication.Abort();
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _factory.Close();
        }
        catch
        {
            _factory.Abort();
        }
    }
}
