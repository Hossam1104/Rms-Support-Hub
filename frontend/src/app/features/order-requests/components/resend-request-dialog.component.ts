import { Component, Input, Output, EventEmitter, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { BranchSummary } from '../../../core/models';

@Component({
  selector: 'app-resend-request-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule],
  template: `
    <div class="dialog-backdrop" (click)="close.emit()"></div>
    <div class="dialog-panel" cdkTrapFocus cdkTrapFocusAutoCapture role="alertdialog" aria-modal="true">
      <div class="dialog-header">
        <i class="bi bi-arrow-repeat"></i>
        <h3>Resend order {{ orderNumber }}</h3>
        <button type="button" class="close-btn" (click)="close.emit()"><i class="bi bi-x-lg"></i></button>
      </div>

      <div class="blocked-banner" *ngIf="!canResend">
        This order cannot be resent: {{ blockedReason }}
      </div>

      <ng-container *ngIf="canResend">
        <p class="dialog-desc">Rebuilds the payload from this request's own stored data and sends it to the selected branch. Currently recorded branch: <strong>{{ currentBranchCode || 'none' }}</strong>.</p>

        <div class="form-group">
          <label>Target branch *</label>
          <select [(ngModel)]="selectedBranch">
            <option value="" disabled>Select a branch...</option>
            @for (b of branches; track b.branchCode) {
              <option [value]="b.branchCode">{{ b.branchCode }}{{ b.branchName ? ' - ' + b.branchName : '' }}</option>
            }
          </select>
        </div>

        <p class="error-message" *ngIf="errorMessage">{{ errorMessage }}</p>

        <div class="dialog-actions">
          <button type="button" class="btn-secondary" (click)="close.emit()">Close</button>
          <button type="button" class="btn-brand" [disabled]="!selectedBranch || submitting" (click)="confirm.emit(selectedBranch)">
            {{ submitting ? 'Resending...' : 'Resend order' }}
          </button>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    :host { position: fixed; inset: 0; z-index: 2100; }
    .dialog-backdrop { position: absolute; inset: 0; background: rgba(2,6,23,.55); backdrop-filter: blur(2px); }
    .dialog-panel {
      position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%);
      width: min(480px, calc(100vw - 32px));
      background: var(--bg-secondary); border: 1px solid var(--glass-border); border-radius: var(--radius-lg);
      box-shadow: var(--shadow-lg); padding: 24px;
      animation: dialogSpring var(--d-slow) var(--ease-spring);
    }
    @keyframes dialogSpring { from { opacity: 0; transform: translate(-50%,-50%) scale(.92); } to { opacity: 1; transform: translate(-50%,-50%) scale(1); } }
    .dialog-header { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
    .dialog-header i { color: var(--brand-500); font-size: 1.3rem; }
    .dialog-header h3 { flex: 1; margin: 0; font-size: 1.05rem; color: var(--text-primary); }
    .close-btn { background: none; border: none; color: var(--text-muted); cursor: pointer; font-size: 1rem; }
    .dialog-desc { color: var(--text-secondary); font-size: 0.88rem; margin: 0 0 14px; }
    .blocked-banner { background: var(--danger-bg); color: var(--danger); padding: 12px 16px; border-radius: var(--radius-sm); font-size: 0.88rem; }
    .form-group { display: flex; flex-direction: column; gap: 6px; margin-bottom: 16px; }
    .form-group label { font-size: 0.82rem; font-weight: 600; color: var(--text-secondary); }
    .form-group select {
      background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-sm);
      color: var(--text-primary); padding: 8px 10px;
    }
    .error-message { color: var(--danger); font-size: 0.82rem; margin: 0 0 12px; }
    .dialog-actions { display: flex; justify-content: flex-end; gap: 10px; }
    .btn-secondary { background: var(--bg-tertiary); border: 1px solid var(--glass-border); color: var(--text-secondary); border-radius: var(--radius-md); padding: 8px 16px; cursor: pointer; }
    .btn-brand { background: var(--grad-brand); color: var(--on-gradient); border: none; border-radius: var(--radius-md); padding: 8px 18px; font-weight: 600; cursor: pointer; }
    .btn-brand:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class ResendRequestDialogComponent implements OnInit {
  @Input() orderNumber: string = '';
  @Input() currentBranchCode: string | null = null;
  @Input() canResend: boolean = true;
  @Input() blockedReason: string = '';
  @Input() branches: BranchSummary[] = [];
  @Input() submitting: boolean = false;
  @Input() errorMessage: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<string>();

  selectedBranch = '';

  ngOnInit() {
    this.selectedBranch = this.currentBranchCode || '';
  }

  @HostListener('document:keydown.escape')
  onEscape() { this.close.emit(); }
}
