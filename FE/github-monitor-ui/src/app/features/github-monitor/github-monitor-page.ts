import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { KENDO_BUTTONS } from '@progress/kendo-angular-buttons';
import { KENDO_SVGICON } from '@progress/kendo-angular-icons';
import { KENDO_LAYOUT } from '@progress/kendo-angular-layout';
import { arrowRotateCwIcon, clockIcon, warningTriangleIcon } from '@progress/kendo-svg-icons';
import { EMPTY, Subject, catchError, exhaustMap, finalize, interval, map, merge, startWith, tap, timer } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ErrorDialogService } from '../../core/error-dialog.service';
import { GitHubMonitorService } from '../../core/github-monitor.service';
import { CommitsResult } from '../../core/models/commit.models';
import { CommitsGrid } from './commits-grid/commits-grid';

@Component({
  selector: 'app-github-monitor-page',
  imports: [DatePipe, KENDO_BUTTONS, KENDO_LAYOUT, KENDO_SVGICON, CommitsGrid],
  templateUrl: './github-monitor-page.html',
  styleUrl: './github-monitor-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GitHubMonitorPage implements OnInit {
  private readonly api = inject(GitHubMonitorService);
  private readonly errors = inject(ErrorDialogService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = { refresh: arrowRotateCwIcon, clock: clockIcon, warning: warningTriangleIcon };
  protected readonly refreshSeconds = environment.scheduledRefreshSeconds;

  // ---- Live section ----
  protected readonly live = signal<CommitsResult | null>(null);
  protected readonly liveLoading = signal(false);
  protected readonly liveFetchedAt = signal<Date | null>(null);
  private readonly liveRefresh$ = new Subject<void>();

  // ---- Scheduled section ----
  protected readonly scheduled = signal<CommitsResult | null>(null);
  protected readonly scheduledLoading = signal(false);
  private readonly scheduledRefresh$ = new Subject<void>();

  /** Ticks every few seconds so the "x ago" labels stay current. */
  private readonly now = toSignal(interval(5000).pipe(map(() => Date.now())), { initialValue: Date.now() });

  protected readonly lastSyncAgo = computed(() => {
    const lastSync = this.scheduled()?.lastSyncTime;
    return lastSync ? describeAgo(this.now() - lastSync.getTime()) : null;
  });

  ngOnInit(): void {
    // Live: fetched once on load, then only when the user clicks "Refresh Live Data".
    this.liveRefresh$
      .pipe(
        startWith(undefined),
        exhaustMap(() => {
          this.liveLoading.set(true);
          return this.api.getLiveCommits().pipe(
            tap((result) => {
              this.live.set(result);
              this.liveFetchedAt.set(new Date());
            }),
            catchError((error) => {
              this.errors.show(error, 'Live commits');
              return EMPTY;
            }),
            finalize(() => this.liveLoading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();

    // Scheduled: re-read the Quartz cache periodically and on demand.
    merge(timer(0, this.refreshSeconds * 1000), this.scheduledRefresh$)
      .pipe(
        exhaustMap(() => {
          this.scheduledLoading.set(true);
          return this.api.getScheduledCommits().pipe(
            tap((result) => this.scheduled.set(result)),
            catchError((error) => {
              this.errors.show(error, 'Scheduled commits');
              return EMPTY;
            }),
            finalize(() => this.scheduledLoading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  protected refreshLive(): void {
    this.liveRefresh$.next();
  }

  protected refreshScheduled(): void {
    this.scheduledRefresh$.next();
  }
}

function describeAgo(ms: number): string {
  const seconds = Math.max(0, Math.round(ms / 1000));
  if (seconds < 10) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  return `${Math.floor(minutes / 60)} h ago`;
}
