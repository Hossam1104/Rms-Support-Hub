import { Component, Input, Output, EventEmitter, HostListener, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { JsonTreeComponent, RiyalComponent } from '../../../shared/ui';

export interface CancelDialogResult {
  reason: string;
  customUrl?: string;
}

export type CancelErrorKind = 'blocked' | 'upstream' | 'network';

export interface CancelErrorState {
  kind: CancelErrorKind;
  message: string;
  rawBody?: string;
}

const QUICK_REASONS = ['Customer request', 'Out of stock', 'Duplicate order', 'Wrong branch'];

/**
 * Cancel confirmation -- required reason, quick-fill chips, an endpoint
 * override, and all four outcome branches from R9 step 7. The dialog stays
 * mounted (parent keeps *ngIf true) on every branch except a clean success,
 * so the operator never loses context or has to re-enter the reason.
 */
@Component({
  selector: 'app-cancel-request-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule, JsonTreeComponent, RiyalComponent],
  template: `
    <div class="dialog-backdrop" (click)="close.emit()"></div>
    <div class="dialog-panel" cdkTrapFocus cdkTrapFocusAutoCapture role="alertdialog" aria-modal="true">
      <div class="dialog-header">
        <i class="bi bi-x-circle-fill"></i>
        <h3>Cancel order {{ orderNumber }}</h3>
        <button type="button" class="close-btn" (click)="close.emit()"><i class="bi bi-x-lg"></i></button>
      </div>

      <div class="blocked-banner" *ngIf="!canCancel">
        This order cannot be cancelled: {{ blockedReason }}
      </div>

      <ng-container *ngIf="canCancel">
        <p class="dialog-desc">This sends a cancellation request to the upstream RMS API. Net total: <strong><app-riyal [size]=".9"></app-riyal>{{ netTotal | number:'1.2-2' }}</strong>.</p>

        <div class="quick-reasons">
          @for (r of quickReasons; track r) {
            <button type="button" class="quick-chip" (click)="reason = r">{{ r }}</button>
          }
        </div>

        <div class="form-group">
          <label>Cancellation reason *</label>
          <textarea rows="3" [(ngModel)]="reason" placeholder="Required..."></textarea>
        </div>

        <div class="form-group">
          <label>
            <input type="checkbox" [(ngModel)]="useCustomUrl" /> Use a custom cancel endpoint
          </label>
          <input type="text" *ngIf="useCustomUrl" [(ngModel)]="customUrl" placeholder="https://..." />
        </div>

        <div class="error-panel" *ngIf="errorState as err">
          <div class="error-message" [class]="'kind-' + err.kind">
            <i class="bi bi-exclamation-triangle-fill"></i>
            <span>{{ err.message }}</span>
          </div>
          <app-json-tree *ngIf="err.rawBody" title="Upstream response" [data]="err.rawBody"></app-json-tree>
          <button type="button" class="retry-btn" *ngIf="err.kind === 'network'" (click)="confirm.emit({ reason: reason.trim(), customUrl: useCustomUrl ? customUrl : undefined })">
            Retry
          </button>
        </div>

        <div class="dialog-actions">
          <button type="button" class="btn-secondary" (click)="close.emit()">Close</button>
          <button
            type="button"
            class="btn-danger"
            [disabled]="!reason.trim() || submitting"
            (click)="onConfirm()">
            {{ submitting ? 'Cancelling...' : 'Cancel order' }}
          </button>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    :host { position: fixed; inset: 0; z-index: 2100; }
    .dialog-backdrop { position: absolute; inset: 0; background: var(--backdrop); backdrop-filter: blur(2px); }
    .dialog-panel {
      position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%);
      width: min(560px, calc(100vw - 32px));
      max-height: 85vh; overflow-y: auto;
      background: var(--surface-panel); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg); padding: 24px;
      animation: dialogSpring var(--d-slow) var(--ease-spring);
    }
    @keyframes dialogSpring { from { opacity: 0; transform: translate(-50%,-50%) scale(.92); } to { opacity: 1; transform: translate(-50%,-50%) scale(1); } }
    .dialog-header { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
    .dialog-header i { color: var(--state-danger-fg); font-size: 1.3rem; }
    .dialog-header h3 { flex: 1; margin: 0; font-size: 1.05rem; color: var(--text-primary); }
    .close-btn { background: none; border: none; color: var(--text-muted); cursor: pointer; font-size: 1rem; }
    .dialog-desc { color: var(--text-secondary); font-size: 0.88rem; margin: 0 0 14px; }
    .blocked-banner { background: var(--state-danger-bg); color: var(--state-danger-fg); padding: 12px 16px; border-radius: var(--radius-sm); font-size: 0.88rem; }
    .quick-reasons { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 14px; }
    .quick-chip { background: var(--surface-raised); border: 1px solid var(--border-subtle); border-radius: var(--radius-pill); color: var(--text-secondary); font-size: 0.76rem; padding: 4px 12px; cursor: pointer; }
    .quick-chip:hover { color: var(--text-primary); background: var(--surface-hover); }
    .form-group { display: flex; flex-direction: column; gap: 6px; margin-bottom: 16px; }
    .form-group label { font-size: 0.82rem; font-weight: 600; color: var(--text-secondary); display: flex; align-items: center; gap: 6px; }
    .form-group textarea, .form-group input[type=text] {
      background: var(--input-bg); border: 1px solid var(--input-border); border-radius: var(--radius-sm);
      color: var(--text-primary); padding: 8px 10px; font-family: inherit; resize: vertical;
    }
    .error-panel { margin-bottom: 16px; }
    .error-message { display: flex; align-items: center; gap: 8px; padding: 10px 14px; border-radius: var(--radius-sm); font-size: 0.85rem; margin-bottom: 8px; }
    .error-message.kind-blocked { background: var(--state-warning-bg); color: var(--state-warning-fg); }
    .error-message.kind-upstream { background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .error-message.kind-network { background: var(--surface-raised); color: var(--text-secondary); }
    .retry-btn { background: var(--grad-brand); color: var(--on-gradient); border: none; border-radius: var(--radius-md); padding: 6px 16px; cursor: pointer; font-size: 0.82rem; }
    .dialog-actions { display: flex; justify-content: flex-end; gap: 10px; }
    .btn-secondary { background: var(--surface-interactive); border: 1px solid var(--border-strong); color: var(--text-secondary); border-radius: var(--radius-md); padding: 8px 16px; cursor: pointer; }
    .btn-danger { background: var(--grad-danger); color: var(--on-gradient); border: none; border-radius: var(--radius-md); padding: 8px 18px; font-weight: 600; cursor: pointer; }
    .btn-danger:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class CancelRequestDialogComponent {
  @Input() orderNumber: string = '';
  @Input() netTotal: number = 0;
  @Input() canCancel: boolean = true;
  @Input() blockedReason: string = '';
  @Input() submitting: boolean = false;
  @Input() errorState: CancelErrorState | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<CancelDialogResult>();

  quickReasons = QUICK_REASONS;
  reason = '';
  useCustomUrl = false;
  customUrl = '';

  @HostListener('document:keydown.escape')
  onEscape() { this.close.emit(); }

  onConfirm() {
    if (!this.reason.trim()) return;
    this.confirm.emit({ reason: this.reason.trim(), customUrl: this.useCustomUrl ? this.customUrl : undefined });
  }
}
