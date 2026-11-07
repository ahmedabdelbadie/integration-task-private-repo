using GitHubMonitor.Api.Models;
using GitHubMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitHubMonitor.Api.Controllers;

[ApiController]
[Route("api/github")]
[Produces("application/json")]
public class GitHubController : ControllerBase
{
    private readonly IGitHubCommitsService _commits;

    public GitHubController(IGitHubCommitsService commits) => _commits = commits;

    /// <summary>Latest commits fetched from GitHub right now (WCF → GetLiveCommits).</summary>
    [HttpGet("live")]
    [ProducesResponseType<ApiResponse<CommitsResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ApiResponse<CommitsResponseDto>>> GetLive()
    {
        var result = await _commits.GetLiveAsync();
        return Ok(ApiResponse<CommitsResponseDto>.Ok(result, $"Fetched {result.Count} live commits from GitHub."));
    }

    /// <summary>Commits cached by the Quartz job (WCF → GetScheduledCommits).</summary>
    [HttpGet("scheduled")]
    [ProducesResponseType<ApiResponse<CommitsResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<CommitsResponseDto>>> GetScheduled()
    {
        var result = await _commits.GetScheduledAsync();

        var message = result.LastSyncTime is null
            ? result.LastSyncError is not null
                ? $"No successful sync yet: {result.LastSyncError}"
                : "The scheduled sync has not completed yet."
            : result.LastSyncError is not null
                ? $"Showing cached data; the most recent sync failed: {result.LastSyncError}"
                : $"Returned {result.Count} cached commits.";

        return Ok(ApiResponse<CommitsResponseDto>.Ok(result, message));
    }
}
