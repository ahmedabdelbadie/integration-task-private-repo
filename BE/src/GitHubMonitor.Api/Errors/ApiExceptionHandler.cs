using System.ServiceModel;
using GitHubMonitor.Api.Models;
using GitHubMonitor.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace GitHubMonitor.Api.Errors;

/// <summary>
/// Turns every unhandled exception into the unified <c>{ success:false, message, data:null }</c> envelope
/// with an HTTP status that reflects what actually failed (GitHub vs. WCF vs. this API).
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError && exception is not FaultException<GitHubFault>)
        {
            _logger.LogError(exception, "Request {Method} {Path} failed with {Status}", context.Request.Method, context.Request.Path, status);
        }
        else
        {
            _logger.LogWarning("Request {Method} {Path} failed with {Status}: {Message}", context.Request.Method, context.Request.Path, status, message);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message), cancellationToken);
        return true;
    }

    internal static (int Status, string Message) Map(Exception exception) => exception switch
    {
        FaultException<GitHubFault> fault => (StatusFor(fault.Detail.Code), fault.Detail.Message),

        // Order matters: these all derive from CommunicationException.
        EndpointNotFoundException => (StatusCodes.Status503ServiceUnavailable,
            "The WCF commit service is unavailable. Make sure GitHubMonitor.WcfService is running."),
        ServerTooBusyException => (StatusCodes.Status503ServiceUnavailable,
            "The WCF commit service is too busy. Please retry shortly."),
        FaultException => (StatusCodes.Status502BadGateway,
            "The WCF commit service returned an unexpected fault."),
        CommunicationException when IsConnectionFailure(exception) => (StatusCodes.Status503ServiceUnavailable,
            "The WCF commit service is unavailable. Make sure GitHubMonitor.WcfService is running."),
        CommunicationException => (StatusCodes.Status502BadGateway,
            "Communication with the WCF commit service failed."),
        TimeoutException => (StatusCodes.Status504GatewayTimeout,
            "The WCF commit service did not respond in time."),

        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };

    /// <summary>
    /// The .NET WCF client reports "connection refused" / DNS failures as a plain CommunicationException
    /// wrapping the socket error, rather than EndpointNotFoundException.
    /// </summary>
    private static bool IsConnectionFailure(Exception exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is System.Net.Sockets.SocketException)
            {
                return true;
            }
        }
        return false;
    }

    private static int StatusFor(GitHubErrorCode code) => code switch
    {
        GitHubErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        GitHubErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        GitHubErrorCode.NotFound => StatusCodes.Status404NotFound,
        GitHubErrorCode.RateLimited => StatusCodes.Status429TooManyRequests,
        GitHubErrorCode.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway
    };
}
