import { Component, HostListener, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavbarComponent } from '../../../../layout/navbar/navbar.component';
import { UiButtonComponent } from '../../../../shared/ui';
import { ClipboardService } from '../../services/clipboard.service';
import { PromptHistoryService, PromptHistoryRecord } from '../../services/prompt-history.service';
import { PromptQualityPanelComponent } from '../prompt-quality-panel/prompt-quality-panel.component';
import { PromptQualityResult } from '../../services/prompt-quality.service';
import { PromptPreviewComponent } from '../prompt-preview/prompt-preview.component';

@Component({
  selector: 'app-generator-workspace',
  standalone: true,
  imports: [RouterLink, NavbarComponent, UiButtonComponent, PromptPreviewComponent, PromptQualityPanelComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="generator-page">
      <header class="generator-header">
        <a routerLink="/tools/prompt-studio" class="generator-back">
          <i class="bi bi-arrow-left" aria-hidden="true"></i>
          Prompt Studio
        </a>
        <div class="generator-header__text">
          <p class="generator-header__eyebrow">{{ eyebrow() }}</p>
          <h1>{{ title() }}</h1>
          <p>{{ description() }}</p>
        </div>
      </header>

      <section class="generator-workspace" [attr.aria-label]="title() + ' workspace'">
        <div class="generator-workspace__input">
          <header class="generator-workspace__panel-header">
            <div>
              <h2>{{ formTitle() }}</h2>
              <p>{{ formSubtitle() }}</p>
            </div>
            <div class="generator-workspace__panel-actions">
              <ui-button variant="ghost" size="sm" icon="bi-magic" ariaLabel="Load sample data" (pressed)="sample.emit()">Sample</ui-button>
              <ui-button variant="ghost" size="sm" icon="bi-eraser" ariaLabel="Clear form" (pressed)="clear.emit()">Clear</ui-button>
            </div>
          </header>

          <div class="generator-workspace__form">
            <ng-content></ng-content>
          </div>

          @if (quality(); as qualityResult) {
            <div class="generator-workspace__quality">
              <app-prompt-quality-panel [result]="qualityResult"></app-prompt-quality-panel>
            </div>
          }

          <footer class="generator-workspace__actions">
            <ui-button icon="bi-stars" ariaLabel="Generate prompt" (pressed)="generate.emit()">Generate Prompt</ui-button>
          </footer>

          @if (history.records().length) {
            <section class="generator-history" aria-labelledby="recent-prompts-title">
              <header class="generator-history__header">
                <div>
                  <h2 id="recent-prompts-title">Recent Prompts</h2>
                  <p>Stored locally on this device.</p>
                </div>
                <ui-button variant="ghost" size="sm" icon="bi-trash3" ariaLabel="Clear prompt history" (pressed)="history.clear()">Clear History</ui-button>
              </header>
              <ul class="generator-history__list">
                @for (record of history.records(); track record.timestamp + record.title + $index) {
                  <li class="generator-history__item">
                    <div class="generator-history__meta">
                      <strong>{{ record.title }}</strong>
                      <span>{{ record.type }} · {{ formatTimestamp(record.timestamp) }}</span>
                    </div>
                    <div class="generator-history__actions">
                      <ui-button variant="ghost" size="sm" icon="bi-box-arrow-up-right" [ariaLabel]="'Open ' + record.title" (pressed)="openHistory(record)">Open</ui-button>
                      <ui-button variant="ghost" size="sm" icon="bi-clipboard" [ariaLabel]="'Copy ' + record.title" (pressed)="copyHistory(record)">Copy</ui-button>
                      <ui-button variant="ghost" size="sm" icon="bi-x-lg" [ariaLabel]="'Delete ' + record.title" (pressed)="history.delete(record)">Delete</ui-button>
                    </div>
                  </li>
                }
              </ul>
            </section>
          }
        </div>

        <app-prompt-preview [prompt]="prompt()" [subtitle]="previewSubtitle()" [filename]="filename()"></app-prompt-preview>
      </section>
    </main>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .generator-page { padding: calc(var(--navbar-height) + var(--space-5)) var(--space-6) var(--space-8); }
    .generator-header { width: min(100%, 1440px); margin: 0 auto var(--space-5); }
    .generator-back { display: inline-flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-4); color: var(--text-secondary); font-size: var(--text-sm); font-weight: var(--weight-semibold); text-decoration: none; border-radius: var(--radius-sm); }
    .generator-back:hover { color: var(--text-accent); }
    .generator-back:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .generator-header__eyebrow { margin: 0 0 var(--space-1); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); text-transform: uppercase; }
    .generator-header h1 { margin: 0; font-size: var(--text-2xl); font-weight: var(--weight-heavy); line-height: 1.1; }
    .generator-header__text > p:last-child { max-width: 760px; margin: var(--space-2) 0 0; color: var(--text-secondary); }
    .generator-workspace { display: grid; width: min(100%, 1440px); margin: 0 auto; grid-template-columns: minmax(360px, 1.02fr) minmax(400px, .98fr); align-items: stretch; gap: var(--space-5); }
    .generator-workspace__input { display: flex; min-width: 0; flex-direction: column; border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); box-shadow: var(--shadow-sm); overflow: hidden; }
    .generator-workspace__panel-header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); padding: var(--space-4) var(--space-5); border-bottom: 1px solid var(--divider); }
    .generator-workspace__panel-header h2 { margin: 0 0 4px; font-size: var(--text-lg); }
    .generator-workspace__panel-header p { margin: 0; color: var(--text-muted); font-size: var(--text-xs); }
    .generator-workspace__panel-actions { display: flex; flex-shrink: 0; gap: var(--space-2); }
    .generator-workspace__form { display: flex; min-width: 0; flex-direction: column; gap: var(--space-4); padding: var(--space-5); }
    .generator-workspace__quality { padding: 0 var(--space-5) var(--space-4); }
    .generator-workspace__actions { display: flex; gap: var(--space-3); padding: var(--space-4) var(--space-5); border-top: 1px solid var(--divider); }
    .generator-history { display: flex; flex-direction: column; gap: var(--space-3); padding: var(--space-4) var(--space-5); border-top: 1px solid var(--divider); }
    .generator-history__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); }
    .generator-history__header h2 { margin: 0 0 3px; font-size: var(--text-sm); }
    .generator-history__header p { margin: 0; color: var(--text-muted); font-size: var(--text-xs); }
    .generator-history__list { display: grid; gap: var(--space-2); margin: 0; padding: 0; list-style: none; }
    .generator-history__item { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); min-width: 0; padding: var(--space-2) 0; border-top: 1px solid var(--divider); }
    .generator-history__meta { display: flex; min-width: 0; flex-direction: column; gap: 3px; }
    .generator-history__meta strong { overflow: hidden; color: var(--text-primary); font-size: var(--text-xs); text-overflow: ellipsis; white-space: nowrap; }
    .generator-history__meta span { color: var(--text-muted); font-size: .7rem; }
    .generator-history__actions { display: flex; flex: 0 0 auto; gap: 2px; }
    @media (max-width: 1080px) { .generator-workspace { grid-template-columns: 1fr; } }
    @media (max-width: 680px) {
      .generator-page { padding: calc(var(--navbar-height) + var(--space-4)) var(--space-4) var(--space-6); }
      .generator-header h1 { font-size: var(--text-xl); }
      .generator-workspace__panel-header { align-items: flex-start; flex-direction: column; }
      .generator-workspace__panel-actions { width: 100%; }
      .generator-workspace__panel-actions ui-button { flex: 1; }
      .generator-workspace__form { padding: var(--space-4); }
      .generator-workspace__quality { padding: 0 var(--space-4) var(--space-4); }
      .generator-history { padding-inline: var(--space-4); }
      .generator-history__item { align-items: flex-start; flex-direction: column; }
      .generator-history__actions { width: 100%; }
      .generator-history__actions ui-button { flex: 1; }
    }
  `]
})
export class GeneratorWorkspaceComponent {
  readonly title = input.required<string>();
  readonly eyebrow = input('QA Prompt Studio');
  readonly description = input.required<string>();
  readonly formTitle = input.required<string>();
  readonly formSubtitle = input.required<string>();
  readonly prompt = input('');
  readonly quality = input<PromptQualityResult | null>(null);
  readonly previewSubtitle = input('Copy and paste this prompt into your approved AI workflow.');
  readonly filename = input('prompt');
  readonly generate = output<void>();
  readonly sample = output<void>();
  readonly clear = output<void>();
  readonly historyOpen = output<string>();

  readonly history = inject(PromptHistoryService);
  private readonly clipboard = inject(ClipboardService);

  @HostListener('document:keydown', ['$event'])
  onGenerateShortcut(event: KeyboardEvent): void {
    if (!event.ctrlKey && !event.metaKey) return;
    if (event.key !== 'Enter') return;
    if (event.cancelable) event.preventDefault();
    this.generate.emit();
  }

  openHistory(record: PromptHistoryRecord): void {
    this.historyOpen.emit(record.prompt);
  }

  copyHistory(record: PromptHistoryRecord): void {
    void this.clipboard.copy(record.prompt);
  }

  formatTimestamp(timestamp: string): string {
    return timestamp.slice(0, 16).replace('T', ' ');
  }
}
