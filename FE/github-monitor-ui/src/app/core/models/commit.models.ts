/** Unified API envelope returned by every GitHubMonitor.Api endpoint. */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export type CommitSource = 'Live' | 'Scheduled';

/** Wire format of a commit (dates are ISO strings). */
export interface CommitDto {
  sha: string;
  shortSha: string;
  message: string;
  author: string;
  authorLogin: string | null;
  date: string;
  url: string | null;
  source: CommitSource;
}

export interface CommitsResponseDto {
  source: CommitSource;
  count: number;
  lastSyncTime: string | null;
  lastSyncError: string | null;
  commits: CommitDto[];
}

/** Grid row: dates parsed so Kendo can sort and format them. */
export interface Commit extends Omit<CommitDto, 'date'> {
  date: Date;
  /** First line of the commit message, used in the grid. */
  title: string;
}

export interface CommitsResult {
  source: CommitSource;
  commits: Commit[];
  lastSyncTime: Date | null;
  lastSyncError: string | null;
  message: string;
}
