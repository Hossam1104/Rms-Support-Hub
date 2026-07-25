import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-payments-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card-section glass-card">
      <div class="section-header">
        <div class="card-title">
          <i class="bi bi-wallet2"></i>
          <span>Payment Methods ({{ payments.length }})</span>
        </div>
        <button type="button" class="glass-button" (click)="openAddDialog.emit()"><i class="bi bi-plus-circle"></i> Add Payment</button>
      </div>

      <div class="table-responsive" *ngIf="payments.length > 0; else emptyState">
        <table class="data-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Method</th>
              <th>Status</th>
              <th>Amount</th>
              <th>Transaction ID</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (pay of payments; track $index) {
              <tr>
                <td>{{ $index + 1 }}</td>
                <td><strong>{{ pay.paymentMethod }}</strong></td>
                <td><span class="badge" [class.badge-success]="pay.paymentStatus === 'done_payment'" [class.badge-warning]="pay.paymentStatus === 'not_payment'">{{ pay.paymentStatus }}</span></td>
                <td><strong>{{ pay.paymentAmount | number:'1.2-2' }}</strong> SAR</td>
                <td>{{ pay.transactionId || '-' }}</td>
                <td>
                  <button type="button" class="btn-icon danger" (click)="deletePayment.emit($index)" title="Remove Payment">
                    <i class="bi bi-trash"></i>
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <ng-template #emptyState>
        <div class="empty-placeholder">
          <i class="bi bi-credit-card"></i>
          <p>No payment methods added yet. Click "Add Payment" to specify how this order is paid.</p>
        </div>
      </ng-template>
    </div>
  `,
  styles: [`
    .card-section { padding: 24px; margin-bottom: 24px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .card-title { display: flex; align-items: center; gap: 10px; font-size: 1.1rem; font-weight: 600; margin: 0; color: var(--text-primary); }
    .card-title i { color: var(--primary); }
    .table-responsive { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.9rem; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid var(--glass-border); }
    .data-table th { background: var(--bg-tertiary); color: var(--text-secondary); font-weight: 600; }
    .data-table tbody tr:hover { background: var(--glass-hover-bg); }
    .badge { padding: 4px 8px; border-radius: var(--radius-sm); font-size: 0.75rem; font-weight: 600; }
    .badge-success { background: var(--success-bg); color: var(--success); }
    .badge-warning { background: var(--warning-bg); color: var(--warning); }
    .btn-icon { background: none; border: none; font-size: 1.1rem; cursor: pointer; }
    .btn-icon.danger { color: var(--danger); }
    .empty-placeholder { text-align: center; padding: 40px 20px; color: var(--text-muted); }
    .empty-placeholder i { font-size: 2.5rem; margin-bottom: 8px; display: block; }
  `]
})
export class PaymentsTableComponent {
  @Input() payments: any[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deletePayment = new EventEmitter<number>();
}
