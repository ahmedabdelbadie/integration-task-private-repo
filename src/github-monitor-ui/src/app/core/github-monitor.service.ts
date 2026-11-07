import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiError } from './api-error';
import { ApiResponse, CommitDto, CommitsResponseDto, CommitsResult } from './models/commit.models';

@Injectable({ providedIn: 'root' })
export class GitHubMonitorService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/github`;

  /** GET /api/github/live — real-time commits (WCF → GetLiveCommits). */
  getLiveCommits(): Observable<CommitsResult> {
    return this.get('live');
  }

  /** GET /api/github/scheduled — Quartz-cached commits (WCF → GetScheduledCommits). */
  getScheduledCommits(): Observable<CommitsResult> {
    return this.get('scheduled');
  }

  private get(path: 'live' | 'scheduled'): Observable<CommitsResult> {
    return this.http.get<ApiResponse<CommitsResponseDto>>(`${this.baseUrl}/${path}`).pipe(
      map((response) => {
        if (!response.success || !response.data) {
          throw new ApiError(response.message || 'The API returned an unsuccessful response.', 200, 'Request failed');
        }
        return toResult(response.data, response.message);
      }),
      catchError((error: unknown) =>
        throwError(() => (error instanceof HttpErrorResponse ? ApiError.fromHttpError(error) : error)),
      ),
    );
  }
}

function toResult(data: CommitsResponseDto, message: string): CommitsResult {
  return {
    source: data.source,
    message,
    lastSyncTime: data.lastSyncTime ? new Date(data.lastSyncTime) : null,
    lastSyncError: data.lastSyncError,
    commits: data.commits.map((c: CommitDto) => ({
      ...c,
      date: new Date(c.date),
      title: c.message.split('\n')[0],
    })),
  };
}
