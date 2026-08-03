import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';

export type UiButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type UiButtonSize = 'sm' | 'md';

@Component({
  selector: 'ui-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      class="ui-button"
      [class]="'ui-button--' + variant()"
      [class.ui-button--sm]="size() === 'sm'"
      [class.ui-button--loading]="loading()"
      [disabled]="disabled() || loading()"
      [type]="buttonType()"
      [attr.aria-label]="ariaLabel() || null"
      [attr.aria-expanded]="ariaExpanded() === null ? null : ariaExpanded()"
      [attr.aria-busy]="loading()"
      (click)="onClick()">
      <i *ngIf="icon() && iconPosition() === 'start' && !loading()" class="bi" [class]="icon()" aria-hidden="true"></i>
      <span class="ui-button__projected"><ng-content></ng-content></span>
      <span class="ui-button__loading" *ngIf="loading()"><span class="ui-button__spinner" aria-hidden="true"></span>{{ loadingLabel() }}</span>
      <i *ngIf="icon() && iconPosition() === 'end' && !loading()" class="bi" [class]="icon()" aria-hidden="true"></i>
    </button>
  `,
  styles: [`
    :host { display: inline-flex; max-width: 100%; }
    .ui-button { display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 42px; max-width: 100%; padding: 0 16px; border: 1px solid transparent; border-radius: var(--radius-md); cursor: pointer; font: inherit; font-size: .88rem; font-weight: 750; line-height: 1; transition: background var(--transition-fast), border-color var(--transition-fast), color var(--transition-fast), box-shadow var(--transition-fast), transform var(--transition-fast); }
    .ui-button:hover:not(:disabled) { transform: translateY(-1px); }
    .ui-button:active:not(:disabled) { transform: translateY(0); }
    .ui-button:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .ui-button:disabled { cursor: not-allowed; opacity: .55; transform: none; }
    .ui-button--primary { background: var(--accent); color: var(--text-inverse); }
    .ui-button--primary:hover:not(:disabled) { background: var(--accent-hover); }
    .ui-button--secondary { background: var(--surface-interactive); border-color: var(--border-strong); color: var(--text-primary); }
    .ui-button--secondary:hover:not(:disabled) { background: var(--surface-hover); }
    .ui-button--ghost { background: transparent; color: var(--text-secondary); }
    .ui-button--ghost:hover:not(:disabled) { background: var(--surface-hover); color: var(--text-primary); }
    .ui-button--danger { background: var(--state-danger-bg); border-color: var(--state-danger-border); color: var(--state-danger-fg); }
    .ui-button--danger:hover:not(:disabled) { background: var(--state-danger-bg-strong); }
    .ui-button--sm { min-height: 34px; padding-inline: 12px; border-radius: var(--radius-sm); font-size: .8rem; }
    .ui-button__projected { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .ui-button--loading .ui-button__projected { display: none; }
    .ui-button__loading { display: inline-flex; align-items: center; gap: 8px; }
    .ui-button__spinner { width: 14px; height: 14px; border: 2px solid currentColor; border-right-color: transparent; border-radius: 50%; animation: uiButtonSpin .7s linear infinite; }
    @keyframes uiButtonSpin { to { transform: rotate(360deg); } }
  `]
})
export class UiButtonComponent {
  readonly variant = input<UiButtonVariant>('primary');
  readonly size = input<UiButtonSize>('md');
  readonly loading = input(false);
  readonly loadingLabel = input('Loading');
  readonly disabled = input(false);
  readonly icon = input<string | null>(null);
  readonly iconPosition = input<'start' | 'end'>('start');
  readonly ariaLabel = input<string | null>(null);
  readonly ariaExpanded = input<boolean | null>(null);
  readonly buttonType = input<'button' | 'submit' | 'reset'>('button');
  readonly pressed = output<void>();

  onClick() {
    if (!this.disabled() && !this.loading()) this.pressed.emit();
  }
}
