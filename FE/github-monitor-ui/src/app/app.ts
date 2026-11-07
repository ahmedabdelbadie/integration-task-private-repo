import { ChangeDetectionStrategy, Component } from '@angular/core';
import { GitHubMonitorPage } from './features/github-monitor/github-monitor-page';
import { ErrorDialog } from './shared/error-dialog/error-dialog';

@Component({
  selector: 'app-root',
  imports: [GitHubMonitorPage, ErrorDialog],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
