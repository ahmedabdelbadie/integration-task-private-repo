import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError } from './core/api-error';
import { ErrorDialogService } from './core/error-dialog.service';
import { GitHubMonitorService } from './core/github-monitor.service';

describe('GitHubMonitorService', () => {
  let service: GitHubMonitorService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(GitHubMonitorService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('unwraps the envelope and parses dates', async () => {
    const result = firstValueFrom(service.getScheduledCommits());

    http.expectOne('http://localhost:5200/api/github/scheduled').flush({
      success: true,
      message: 'Returned 1 cached commits.',
      data: {
        source: 'Scheduled',
        count: 1,
        lastSyncTime: '2026-11-07T17:30:00Z',
        lastSyncError: null,
        commits: [
          {
            sha: '21b5c2ae5e37',
            shortSha: '21b5c2a',
            message: 'First line\n\nbody',
            author: 'Ahmed Badea',
            authorLogin: 'ahmedabdelbadie',
            date: '2026-11-07T17:12:04Z',
            url: null,
            source: 'Scheduled',
          },
        ],
      },
    });

    const value = await result;
    expect(value.lastSyncTime?.toISOString()).toBe('2026-11-07T17:30:00.000Z');
    expect(value.commits[0].date).toBeInstanceOf(Date);
    expect(value.commits[0].title).toBe('First line');
  });

  it('maps a 401 envelope to an ApiError with the server message', async () => {
    const result = firstValueFrom(service.getLiveCommits()).catch((e) => e);

    http
      .expectOne('http://localhost:5200/api/github/live')
      .flush(
        { success: false, message: 'GitHub rejected the access token (401).', data: null },
        { status: 401, statusText: 'Unauthorized' },
      );

    const error = await result;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(401);
    expect(error.title).toBe('GitHub authentication failed');
    expect(error.message).toBe('GitHub rejected the access token (401).');
  });
});

describe('ApiError', () => {
  it('reports an unreachable API for status 0', () => {
    const error = ApiError.fromHttpError(
      new HttpErrorResponse({ status: 0, url: 'http://localhost:5200/api/github/live' }),
    );
    expect(error.title).toBe('API unreachable');
    expect(error.message).toContain('http://localhost:5200');
  });
});

describe('ErrorDialogService', () => {
  it('does not replace an open dialog with the same message', () => {
    const dialog = new ErrorDialogService();
    dialog.show(new ApiError('down', 503, 'Service unavailable'), 'Live commits');
    const first = dialog.current();
    dialog.show(new ApiError('down', 503, 'Service unavailable'), 'Scheduled commits');
    expect(dialog.current()).toBe(first);

    dialog.close();
    expect(dialog.current()).toBeNull();
  });
});
