import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { KENDO_BUTTONS } from '@progress/kendo-angular-buttons';
import { KENDO_GRID } from '@progress/kendo-angular-grid';
import { KENDO_TOOLTIP } from '@progress/kendo-angular-tooltip';
import { SortDescriptor } from '@progress/kendo-data-query';
import { Commit } from '../../../core/models/commit.models';

/**
 * Kendo Grid showing commits with the required columns:
 * Commit Message, Author, Date, Commit SHA, Source.
 * On narrow screens the columns collapse into a single stacked "Commit" column.
 */
@Component({
  selector: 'app-commits-grid',
  imports: [DatePipe, NgTemplateOutlet, KENDO_GRID, KENDO_BUTTONS, KENDO_TOOLTIP],
  templateUrl: './commits-grid.html',
  styleUrl: './commits-grid.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommitsGrid {
  readonly commits = input.required<Commit[]>();
  readonly loading = input(false);
  readonly emptyText = input('No commits to display.');

  protected readonly sort: SortDescriptor[] = [{ field: 'date', dir: 'desc' }];
  protected readonly pageable = { buttonCount: 3, pageSizes: [5, 10], info: true };

  protected readonly wide = '(min-width: 768px)';
  protected readonly narrow = '(max-width: 767.98px)';
}
