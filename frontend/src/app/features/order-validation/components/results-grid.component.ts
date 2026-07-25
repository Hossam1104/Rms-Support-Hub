import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-results-grid',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="results-card glass-card">
      <div class="card-title">
        <i class="bi bi-list-check"></i>
        <span>Search Results ({{ results.length }})</span>
      </div>

      <div class="table-responsive" *ngIf="results.length > 0; else emptyState">
        <table class="data-table">
          <thead>
            <tr>
              <th>Order #</th>
              <th>Branch</th>
              <th>Status</th>
              <th>Creation Date</th>
              <th>Invoice Barcode</th>
              <th>Invoice Date</th>
              <th>Resend Eligibility</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (row of results; track row.headerId) {
              <tr>
                <td><strong>{{ row.orderNumber }}</strong></td>
                <td>{{ row.branchCode }}</td>
                <td><span class="badge" [class]="getStatusClass(row.status)">{{ row.statusLabel }}</span></td>
                <td>{{ row.creationDate | date:'short' }}</td>
                <td>{{ row.invoiceBarcode || '-' }}</td>
                <td>{{ (row.invoiceDate | date:'short') || '-' }}</td>
                <td>
                  <span class="eligibility-pill" [class.eligible]="row.canResend" [class.blocked]="!row.canResend">
                    {{ row.canResend ? 'Eligible' : 'Blocked (' + row.statusLabel + ')' }}
                  </span>
                </td>
                <td>
                  <button type="button" class="btn-action glass-card" (click)="viewDetails.emit(row.orderNumber)">
                    <i class="bi bi-eye"></i> Details
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <ng-template #emptyState>
        <div class="empty-placeholder">
          <i class="bi bi-inbox"></i>
          <p>No orders matched your search criteria in the database.</p>
        </div>
      </ng-template>
    </div>
  `,
  styles: [`
    .results-card { padding: 24px; }
    .card-title { display: flex; align-items: center; gap: 10px; font-size: 1.1rem; font-weight: 600; margin-bottom: 20px; color: var(--text-primary); }
    .card-title i { color: var(--primary); }
    .table-responsive { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.85rem; }
    .data-table th, .data-table td { padding: 12px 14px; border-bottom: 1px solid var(--glass-border); }
    .data-table th { background: var(--bg-tertiary); color: var(--text-secondary); font-weight: 600; }
    .data-table tbody tr:hover { background: var(--glass-hover-bg); }
    .badge { padding: 4px 8px; border-radius: var(--radius-sm); font-size: 0.75rem; font-weight: 600; }
    .badge-info { background: rgba(99, 102, 241, 0.15); color: var(--primary); }
    .badge-success { background: var(--success-bg); color: var(--success); }
    .badge-danger { background: var(--danger-bg); color: var(--danger); }
    .badge-warning { background: var(--warning-bg); color: var(--warning); }
    .eligibility-pill { font-size: 0.75rem; font-weight: 600; padding: 4px 8px; border-radius: var(--radius-pill); }
    .eligibility-pill.eligible { background: var(--success-bg); color: var(--success); }
    .eligibility-pill.blocked { background: var(--bg-tertiary); color: var(--text-muted); }
    .btn-action { display: inline-flex; align-items: center; gap: 6px; padding: 6px 12px; font-size: 0.8rem; cursor: pointer; }
    .empty-placeholder { text-align: center; padding: 40px 20px; color: var(--text-muted); }
    .empty-placeholder i { font-size: 2.5rem; margin-bottom: 8px; display: block; }
  `]
})
export class ResultsGridComponent {
  @Input() results: any[] = [];
  @Output() viewDetails = new EventEmitter<string>();

  getStatusClass(status: number): string {
    switch (status) {
      case 9: return 'badge-success';
      case 4: return 'badge-warning';
      case 8: return 'badge-info';
      case 5:
      case 6:
      case 7: return 'badge-danger';
      default: return 'badge-info';
    }
  }
}
