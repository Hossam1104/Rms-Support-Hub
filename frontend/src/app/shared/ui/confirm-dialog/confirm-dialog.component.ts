import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';

export type ConfirmVariant = 'danger' | 'brand';

/** Generic confirm dialog with an optional required-reason textarea slot --
 * the reusable form of this app's existing cancel-dialog.component.ts. */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule],
  template: `
    <div class="confirm-backdrop" (click)="cancel.emit()"></div>
    <div class="confirm-dialog" [class]="'variant-' + variant" cdkTrapFocus cdkTrapFocusAutoCapture role="alertdialog" aria-modal="true">
      <div class="confirm-header">
        <i class="bi" [class.bi-exclamation-triangle-fill]="variant === 'danger'" [class.bi-question-circle-fill]="variant === 'brand'"></i>
        <h3>{{ title }}</h3>
      </div>

      <p class="confirm-message" *ngIf="message">{{ message }}</p>

      <div class="reason-group" *ngIf="requireReason">
        <label>{{ reasonLabel }}</label>
        <textarea rows="3" [(ngModel)]="reason" [placeholder]="reasonPlaceholder"></textarea>
      </div>

      <div class="confirm-actions">
        <button type="button" class="btn-secondary" (click)="cancel.emit()">{{ cancelLabel }}</button>
        <button type="button" class="btn-confirm" [disabled]="isConfirmDisabled()" (click)="confirm.emit(reason.trim())">
          {{ confirmLabel }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    :host { position: fixed; inset: 0; z-index: 2100; }
    .confirm-backdrop { position: absolute; inset: 0; background: rgba(2, 6, 23, .55); backdrop-filter: blur(2px); }
    .confirm-dialog {
      position: absolute; top: 50%; left: 50%;
      transform: translate(-50%, -50%);
      width: min(480px, calc(100vw - 32px));
      background: var(--bg-secondary);
      border-radius: var(--radius-lg);
      border: 1px solid var(--glass-border);
      box-shadow: var(--shadow-lg);
      padding: 24px;
      animation: dialogSpringIn var(--d-slow) var(--ease-spring);
    }
    .confirm-header { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
    .confirm-header h3 { margin: 0; font-size: 1.1rem; color: var(--text-primary); }
    .confirm-header i { font-size: 1.3rem; color: var(--danger); }
    .variant-brand .confirm-header i { color: var(--brand-500); }
    .confirm-message { color: var(--text-secondary); font-size: 0.9rem; margin: 0 0 16px; }
    .reason-group { display: flex; flex-direction: column; gap: 6px; margin-bottom: 20px; }
    .reason-group label { font-size: 0.82rem; font-weight: 600; color: var(--text-secondary); }
    .reason-group textarea {
      background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-sm);
      color: var(--text-primary); padding: 8px 10px; font-family: inherit; resize: vertical;
    }
    .confirm-actions { display: flex; justify-content: flex-end; gap: 10px; }
    .btn-secondary {
      background: var(--bg-tertiary); border: 1px solid var(--glass-border); color: var(--text-secondary);
      border-radius: var(--radius-md); padding: 8px 16px; cursor: pointer;
    }
    .btn-confirm {
      background: var(--grad-danger); color: var(--on-gradient); border: none;
      border-radius: var(--radius-md); padding: 8px 18px; font-weight: 600; cursor: pointer;
      transition: transform var(--transition-fast), opacity var(--transition-fast);
    }
    .variant-brand .btn-confirm { background: var(--grad-brand); }
    .btn-confirm:hover:not(:disabled) { transform: scale(1.03); }
    .btn-confirm:disabled { opacity: 0.5; cursor: not-allowed; }
    @keyframes dialogSpringIn {
      from { opacity: 0; transform: translate(-50%, -50%) scale(.92); }
      to { opacity: 1; transform: translate(-50%, -50%) scale(1); }
    }
  `]
})
export class ConfirmDialogComponent {
  @Input() title: string = 'Are you sure?';
  @Input() message?: string;
  @Input() variant: ConfirmVariant = 'danger';
  @Input() requireReason: boolean = false;
  @Input() reasonLabel: string = 'Reason';
  @Input() reasonPlaceholder: string = '';
  @Input() confirmLabel: string = 'Confirm';
  @Input() cancelLabel: string = 'Cancel';

  /** U1 (UI_Rework_Plan.md §3 decision 4): when set, the operator must type
   * this exact value (typically the environment's Key, e.g. "UPC Production")
   * into the reason field before Confirm enables -- gates Production sends
   * and cancels behind a real typed confirmation instead of a single click. */
  @Input() requiredTypedValue?: string;

  @Output() confirm = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();

  reason: string = '';

  isConfirmDisabled(): boolean {
    if (this.requiredTypedValue) return this.reason.trim() !== this.requiredTypedValue;
    return this.requireReason && !this.reason.trim();
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.cancel.emit();
  }
}
