import { Injectable, signal } from '@angular/core';
import { ApiError } from './api-error';

export interface ErrorDialogState {
  title: string;
  message: string;
  status?: number;
}

/** Holds the error currently shown in the Kendo error dialog. */
@Injectable({ providedIn: 'root' })
export class ErrorDialogService {
  private readonly _current = signal<ErrorDialogState | null>(null);
  readonly current = this._current.asReadonly();

  show(error: unknown, context?: string): void {
    const state = toState(error, context);
    const open = this._current();
    // Don't stack identical errors (e.g. the auto-refresh failing repeatedly).
    if (open && open.message === state.message) {
      return;
    }
    this._current.set(state);
  }

  close(): void {
    this._current.set(null);
  }
}

function toState(error: unknown, context?: string): ErrorDialogState {
  if (error instanceof ApiError) {
    return {
      title: context ? `${context}: ${error.title}` : error.title,
      message: error.message,
      status: error.status || undefined,
    };
  }
  return {
    title: context ?? 'Unexpected error',
    message: error instanceof Error ? error.message : 'An unexpected error occurred.',
  };
}
