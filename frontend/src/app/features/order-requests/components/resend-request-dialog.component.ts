import { Component, Input, Output, EventEmitter, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';

import { A11yModule } from '@angular/cdk/a11y';
import { BranchOption } from '../../../core/models';
import { SearchableSelectComponent } from '../../../shared/ui';
import { canResend as canResendStatus } from '../resend-eligibility';

@Component({
  selector: 'app-resend-request-dialog',
  standalone: true,
  imports: [CommonModule, A11yModule, SearchableSelectComponent],
  template: `
    <div class="dialog-backdrop" (click)="close.emit()"></div>
    <div class="dialog-panel" cdkTrapFocus cdkTrapFocusAutoCapture role="alertdialog" aria-modal="true">
      <div class="dialog-header">
        <i class="bi bi-arrow-repeat"></i>
        <h3>Resend order {{ orderNumber }}</h3>
        <button type="button" class="close-btn" aria-label="Close" (click)="close.emit()"><i class="bi bi-x-lg" aria-hidden="true"></i></button>
      </div>

      <div class="blocked-banner" *ngIf="!isEligible()">
        This order cannot be resent: {{ blockedReason }}
      </div>

      <ng-container *ngIf="isEligible()">
        <div class="resend-facts">
          <div><span>Current status</span><strong>{{ statusLabel || 'Unknown' }}</strong></div>
          <div><span>Target environment</span><strong>{{ environmentKey || 'Current environment' }}</strong></div>
          <div><span>Original number</span><strong class="mono">{{ orderNumber }}</strong></div>
        </div>
        <p class="dialog-desc">The original stored request and the same order number will be sent again. No replacement number will be generated. Currently recorded branch: <strong>{{ currentBranchCode || 'none' }}</strong>.</p>

        <div class="form-group">
          <app-searchable-select
            label="Target branch"
            placeholder="Search branches"
            [options]="branches"
            [value]="selectedBranch || null"
            [loading]="branchesLoading"
            [error]="branchError"
            (valueChange)="selectedBranch = $event || ''"
            (refresh)="branchRefresh.emit()">
          </app-searchable-select>
        </div>

        <p class="error-message" *ngIf="errorMessage">{{ errorMessage }}</p>

        <div class="dialog-actions">
          <button type="button" class="btn-secondary" (click)="close.emit()">Close</button>
          <button type="button" class="btn-brand" [disabled]="!selectedBranch || submitting || !isEligible()" (click)="onConfirm()">
            {{ submitting ? 'Resending...' : 'Resend order' }}
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
      width: min(480px, calc(100vw - 32px));
      background: var(--surface-panel); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg); padding: 24px;
      animation: dialogSpring var(--d-slow) var(--ease-spring);
    }
    @keyframes dialogSpring { from { opacity: 0; transform: translate(-50%,-50%) scale(.92); } to { opacity: 1; transform: translate(-50%,-50%) scale(1); } }
    .dialog-header { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
    .dialog-header i { color: var(--accent); font-size: 1.3rem; }
    .dialog-header h3 { flex: 1; margin: 0; font-size: 1.05rem; color: var(--text-primary); }
    .close-btn { background: none; border: none; color: var(--text-muted); cursor: pointer; font-size: 1rem; }
    .dialog-desc { color: var(--text-secondary); font-size: 0.88rem; margin: 0 0 14px; }
    .resend-facts { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; margin: 0 0 14px; }
    .resend-facts div { display: flex; flex-direction: column; gap: 3px; min-width: 0; padding: 9px 10px; background: var(--surface-raised); border-radius: var(--radius-sm); }
    .resend-facts span { color: var(--text-muted); font-size: .7rem; text-transform: uppercase; letter-spacing: .03em; }
    .resend-facts strong { overflow: hidden; color: var(--text-primary); font-size: .8rem; text-overflow: ellipsis; white-space: nowrap; }
    .mono { font-family: 'JetBrains Mono', monospace; }
    .blocked-banner { background: var(--state-danger-bg); color: var(--state-danger-fg); padding: 12px 16px; border-radius: var(--radius-sm); font-size: 0.88rem; }
    .form-group { display: flex; flex-direction: column; gap: 6px; margin-bottom: 16px; }
    .form-group label { font-size: 0.82rem; font-weight: 600; color: var(--text-secondary); }
    .error-message { color: var(--state-danger-fg); font-size: 0.82rem; margin: 0 0 12px; }
    .dialog-actions { display: flex; justify-content: flex-end; gap: 10px; }
    .btn-secondary { background: var(--surface-interactive); border: 1px solid var(--border-strong); color: var(--text-secondary); border-radius: var(--radius-md); padding: 8px 16px; cursor: pointer; }
    .btn-brand { background: var(--grad-brand); color: var(--on-gradient); border: none; border-radius: var(--radius-md); padding: 8px 18px; font-weight: 600; cursor: pointer; }
    .btn-brand:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class ResendRequestDialogComponent implements OnInit {
  @Input() orderNumber: string = '';
  @Input() currentBranchCode: string | null = null;
  @Input() canResend: boolean = false;
  @Input() status: number | null = null;
  @Input() statusLabel: string | null = null;
  @Input() environmentKey: string | null = null;
  @Input() blockedReason: string = '';
  @Input() branches: BranchOption[] = [];
  @Input() branchesLoading = false;
  @Input() branchError: string | null = null;
  @Input() submitting: boolean = false;
  @Input() errorMessage: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<string>();
  @Output() branchRefresh = new EventEmitter<void>();

  selectedBranch = '';

  ngOnInit() {
    this.selectedBranch = this.currentBranchCode || '';
  }

  isEligible(): boolean {
    return this.canResend && canResendStatus(this.status ?? this.statusLabel);
  }

  onConfirm() {
    const branchCode = this.selectedBranch.trim();
    if (!branchCode || this.submitting || !this.isEligible()) return;
    this.confirm.emit(branchCode);
  }

  @HostListener('document:keydown.escape')
  onEscape() { this.close.emit(); }
}
