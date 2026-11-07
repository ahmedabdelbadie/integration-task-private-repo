import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { KENDO_BUTTONS } from '@progress/kendo-angular-buttons';
import { KENDO_DIALOG } from '@progress/kendo-angular-dialog';
import { KENDO_SVGICON } from '@progress/kendo-angular-icons';
import { exclamationCircleIcon } from '@progress/kendo-svg-icons';
import { ErrorDialogService } from '../../core/error-dialog.service';

/** Global Kendo Dialog that displays the error held by ErrorDialogService. */
@Component({
  selector: 'app-error-dialog',
  imports: [KENDO_DIALOG, KENDO_BUTTONS, KENDO_SVGICON],
  template: `
    @if (errors.current(); as error) {
      <kendo-dialog (close)="errors.close()" [width]="480" [maxWidth]="'92vw'" [minWidth]="280">
        <kendo-dialog-titlebar>
          <div class="title">
            <kendo-svgicon [icon]="icon" themeColor="error"></kendo-svgicon>
            <span>{{ error.title }}</span>
          </div>
        </kendo-dialog-titlebar>
        <p class="message">{{ error.message }}</p>
        @if (error.status) {
          <p class="status">HTTP status: {{ error.status }}</p>
        }
        <kendo-dialog-actions>
          <button kendoButton themeColor="primary" (click)="errors.close()">OK</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    .title { display: flex; align-items: center; gap: 8px; }
    .message { margin: 0 0 8px; line-height: 1.5; }
    .status { margin: 0; font-size: 0.85em; color: var(--app-muted); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorDialog {
  protected readonly errors = inject(ErrorDialogService);
  protected readonly icon = exclamationCircleIcon;
}
