import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { UiCardComponent } from '../ui-card/ui-card.component';

export type ToolCardAccent = 'brand' | 'info' | 'amber' | 'teal';
export type ToolCardStatus = 'available' | 'migration-pending';

/**
 * Base card for a hub tool entry: gradient-accent icon, title, description,
 * and a semantic status badge. Built on the interactive `ui-card`, so hover,
 * keyboard activation (Enter/Space), and the shared focus ring come from the
 * same contract; hover motion moves the card transform only and never blurs
 * the text.
 */
@Component({
  selector: 'app-tool-card',
  standalone: true,
  imports: [CommonModule, UiCardComponent],
  template: `
    <ui-card
      class="tool-card"
      variant="interactive"
      [disabled]="disabled()"
      [ariaLabel]="ariaLabel() || title()"
      (activated)="activated.emit()">
      <div uiCardHeader class="tool-card__header">
        <i class="bi tool-card__icon" [class]="icon() + ' tool-card__icon--' + accent()" aria-hidden="true"></i>
        <span class="tool-card__status" [class.tool-card__status--pending]="status() === 'migration-pending'">
          {{ statusLabel() }}
        </span>
      </div>
      <h3 class="tool-card__title">{{ title() }}</h3>
      <p class="tool-card__description" *ngIf="description()">{{ description() }}</p>
    </ui-card>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .tool-card { height: 100%; }
    .tool-card__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); }
    .tool-card__icon { font-size: 1.7rem; line-height: 1; -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .tool-card__icon--brand { background: var(--grad-brand); -webkit-background-clip: text; }
    .tool-card__icon--info { background: var(--grad-info); -webkit-background-clip: text; }
    .tool-card__icon--amber { background: var(--grad-amber); -webkit-background-clip: text; }
    .tool-card__icon--teal { background: var(--grad-teal); -webkit-background-clip: text; }
    .tool-card__status { flex-shrink: 0; padding: 4px 12px; border: 1px solid var(--state-success-border); border-radius: var(--radius-pill); background: var(--state-success-bg); color: var(--state-success-fg); font-size: var(--text-xs); font-weight: var(--weight-bold); white-space: nowrap; }
    .tool-card__status--pending { border-color: var(--state-warning-border); background: var(--state-warning-bg); color: var(--state-warning-fg); }
    .tool-card__title { margin: 0 0 var(--space-1); font-size: var(--text-lg); font-weight: var(--weight-bold); line-height: var(--leading-tight); }
    .tool-card__description { margin: 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
  `]
})
export class ToolCardComponent {
  readonly title = input.required<string>();
  readonly description = input<string>('');
  readonly icon = input('bi-grid');
  readonly accent = input<ToolCardAccent>('brand');
  readonly status = input<ToolCardStatus>('available');
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly activated = output<void>();

  readonly statusLabel = computed(() =>
    this.status() === 'migration-pending' ? 'Migration Pending' : 'Available');
}
