import { HttpErrorResponse } from '@angular/common/http';
import { ApiResponse } from './models/commit.models';

/** Normalised error raised by every API call, ready to show to the user. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly title: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  static fromHttpError(error: HttpErrorResponse): ApiError {
    if (error.status === 0) {
      return new ApiError(
        'Cannot reach the GitHub Monitor API. Make sure GitHubMonitor.Api is running on ' + new URL(error.url ?? 'http://localhost').origin + '.',
        0,
        'API unreachable',
      );
    }

    const envelope = error.error as Partial<ApiResponse<unknown>> | null;
    const message =
      envelope && typeof envelope.message === 'string' && envelope.message
        ? envelope.message
        : `The request failed with HTTP ${error.status} ${error.statusText}.`;

    return new ApiError(message, error.status, ApiError.titleFor(error.status));
  }

  private static titleFor(status: number): string {
    switch (status) {
      case 401:
        return 'GitHub authentication failed';
      case 403:
        return 'Access denied by GitHub';
      case 404:
        return 'Repository not found';
      case 429:
        return 'GitHub rate limit reached';
      case 503:
        return 'Service unavailable';
      case 504:
        return 'Request timed out';
      default:
        return 'Something went wrong';
    }
  }
}
