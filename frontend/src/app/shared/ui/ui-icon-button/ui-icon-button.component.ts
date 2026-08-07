import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';

export type UiIconButtonVariant = 'ghost' | 'secondary' | 'danger';
export type UiIconButtonSize = 'sm' | 'md';

/**
 * Icon-only action button. An accessible label is mandatory because there is
 * no visible text. `active` renders the pressed/toggled state and exposes
 * `aria-pressed` for toggle-style controls.
 */
@Component({
  selector: 'ui-icon-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      class="ui-icon-button"
      [class]="'ui-icon-button--' + variant()"
      [class.ui-icon-button--sm]="size() === 'sm'"
      [class.ui-icon-button--active]="active()"
      type="button"
      [disabled]="disabled()"
      [attr.aria-label]="ariaLabel()"
      [attr.aria-pressed]="active() === null ? null : active()"
      [attr.title]="title() || null"
      (click)="onClick()">
      <i class="bi" [class]="icon()" aria-hidden="true"></i>
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }
    .ui-icon-button { display: inline-grid; width: 42px; height: 42px; place-items: center; padding: 0; border: 1px solid transparent; border-radius: var(--radius-md); cursor: pointer; font: inherit; font-size: 1.05rem; line-height: 1; color: var(--text-secondary); background: transparent; transition: background var(--transition-fast), border-color var(--transition-fast), color var(--transition-fast), box-shadow var(--transition-fast); }
    .ui-icon-button:hover:not(:disabled) { background: var(--surface-hover); color: var(--text-primary); }
    .ui-icon-button:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .ui-icon-button:disabled { cursor: not-allowed; opacity: .55; }
    .ui-icon-button--secondary { background: var(--surface-interactive); border-color: var(--border-strong); color: var(--text-primary); }
    .ui-icon-button--secondary:hover:not(:disabled) { background: var(--surface-hover); }
    .ui-icon-button--danger { color: var(--state-danger-fg); }
    .ui-icon-button--danger:hover:not(:disabled) { background: var(--state-danger-bg); }
    .ui-icon-button--danger:focus-visible { box-shadow: var(--focus-ring-danger); }
    .ui-icon-button--sm { width: 34px; height: 34px; border-radius: var(--radius-sm); font-size: .92rem; }
    .ui-icon-button--active { background: var(--accent-soft); border-color: var(--accent); color: var(--text-accent); }
    .ui-icon-button--active:hover:not(:disabled) { background: var(--accent-soft); color: var(--text-accent); }
  `]
})
export class UiIconButtonComponent {
  readonly icon = input.required<string>();
  readonly ariaLabel = input.required<string>();
  readonly title = input<string | null>(null);
  readonly variant = input<UiIconButtonVariant>('ghost');
  readonly size = input<UiIconButtonSize>('md');
  readonly disabled = input(false);
  readonly active = input<boolean | null>(null);
  readonly pressed = output<void>();

  onClick() {
    if (!this.disabled()) this.pressed.emit();
  }
}
