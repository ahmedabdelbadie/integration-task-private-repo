using GitHubMonitor.Api.Models;

namespace GitHubMonitor.Api.Services;

public interface IGitHubCommitsService
{
    Task<CommitsResponseDto> GetLiveAsync();
    Task<CommitsResponseDto> GetScheduledAsync();
}
