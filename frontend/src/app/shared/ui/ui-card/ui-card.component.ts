import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

export type UiCardVariant = 'default' | 'raised' | 'interactive';

@Component({
  selector: 'ui-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <article
      class="ui-card"
      [class.ui-card--raised]="variant() === 'raised'"
      [class.ui-card--interactive]="isInteractive()"
      [class.ui-card--disabled]="disabled()"
      [attr.role]="isInteractive() ? 'button' : null"
      [attr.tabindex]="isInteractive() && !disabled() ? 0 : null"
      [attr.aria-disabled]="isInteractive() ? disabled() : null"
      [attr.aria-label]="ariaLabel() || null"
      (click)="activate($event)"
      (keydown)="onKeydown($event)">
      <header class="ui-card__header"><ng-content select="[uiCardHeader]"></ng-content></header>
      <div class="ui-card__body"><ng-content></ng-content></div>
      <footer class="ui-card__footer"><ng-content select="[uiCardFooter]"></ng-content></footer>
    </article>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-card {
      position: relative;
      overflow: hidden;
      background: var(--surface-panel);
      border: 1px solid var(--card-border);
      border-radius: var(--panel-radius);
      box-shadow: var(--shadow-sm);
      color: var(--text-primary);
      transition: border-color var(--transition-fast), transform var(--transition-normal);
    }
    .ui-card::before {
      content: '';
      position: absolute;
      inset: 0 auto 0 0;
      width: 2px;
      background: transparent;
      transition: background var(--transition-fast);
    }
    .ui-card--raised { background: var(--surface-raised); box-shadow: var(--shadow-md); }
    .ui-card--interactive { cursor: pointer; }
    .ui-card--interactive:hover { border-color: var(--border-strong); box-shadow: var(--shadow-md); transform: translateY(-1px); }
    .ui-card--interactive:hover::before, .ui-card--interactive:focus-visible::before { background: var(--accent); }
    .ui-card--interactive:focus-visible { outline: none; box-shadow: var(--focus-ring), var(--shadow-md); }
    .ui-card--disabled { cursor: not-allowed; opacity: .58; transform: none; }
    .ui-card__header:empty, .ui-card__footer:empty { display: none; }
    .ui-card__header { min-height: var(--section-header-height); padding: var(--panel-padding-compact) var(--panel-padding) 0; font-weight: 700; }
    .ui-card__body { padding: var(--panel-padding); }
    .ui-card__header + .ui-card__body { padding-top: var(--panel-padding-compact); }
    .ui-card__footer { padding: 0 var(--panel-padding) var(--panel-padding-compact); color: var(--text-secondary); }
  `]
})
export class UiCardComponent {
  readonly variant = input<UiCardVariant>('default');
  readonly interactive = input(false);
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly activated = output<void>();

  readonly isInteractive = computed(() => this.interactive() || this.variant() === 'interactive');

  activate(event: MouseEvent) {
    if (!this.isInteractive() || this.disabled()) return;
    const target = event.target as HTMLElement;
    if (target.closest('button, a, input, select, textarea')) return;
    this.activated.emit();
  }

  onKeydown(event: KeyboardEvent) {
    if (!this.isInteractive() || this.disabled() || !['Enter', ' '].includes(event.key)) return;
    event.preventDefault();
    this.activated.emit();
  }
}
