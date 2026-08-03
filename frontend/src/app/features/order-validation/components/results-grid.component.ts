import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrderRequestListItem } from '../../../core/models';
import { StatusPillComponent, UiButtonComponent, UiCardComponent, UiTableComponent } from '../../../shared/ui';

/** OrderRequestStatus.ResendBlockedStatuses -- see docs/api-spec.md and
 * backend/src/OnlineOrderTool.Core/OrderRequestStatus.cs. */
const RESEND_BLOCKED_STATUSES = new Set([4, 8, 9]);

@Component({
  selector: 'app-results-grid',
  standalone: true,
  imports: [CommonModule, StatusPillComponent, UiButtonComponent, UiCardComponent, UiTableComponent],
  template: `
    <ui-card variant="raised" class="results-card">
      <div uiCardHeader class="section-heading">
        <span class="section-title"><i class="bi bi-list-check" aria-hidden="true"></i> Search Results ({{ results.length }})</span>
        <span class="results-note">Read-only legacy lookup</span>
      </div>

      <ui-table *ngIf="results.length > 0; else emptyState" [dense]="true" [stickyHeader]="true" caption="Order validation results">
        <thead><tr><th>Order #</th><th>Branch</th><th>Status</th><th>Creation Date</th><th>Invoice Barcode</th><th>Invoice Date</th><th>Resend Eligibility</th><th>Actions</th></tr></thead>
        <tbody>
          @for (row of results; track row.id) {
            <tr>
              <td><strong>{{ row.orderNumber }}</strong></td>
              <td>{{ row.branchCode }}</td>
              <td><app-status-pill *ngIf="row.orderStatus as status" [status]="status" [label]="row.orderStatusLabel || undefined"></app-status-pill></td>
              <td>{{ row.orderDate | date:'short' }}</td>
              <td>{{ row.invoiceBarcode || '-' }}</td>
              <td>{{ (row.invoiceDate | date:'short') || '-' }}</td>
              <td><span class="eligibility-pill" [class.eligible]="canResend(row)" [class.blocked]="!canResend(row)">{{ canResend(row) ? 'Eligible' : 'Blocked (' + row.orderStatusLabel + ')' }}</span></td>
              <td><ui-button variant="secondary" size="sm" icon="bi-eye" (pressed)="viewDetails.emit(row.orderNumber)">Details</ui-button></td>
            </tr>
          }
        </tbody>
      </ui-table>

      <ng-template #emptyState>
        <div class="empty-placeholder"><i class="bi bi-inbox" aria-hidden="true"></i><p>No orders matched your search criteria in the database.</p></div>
      </ng-template>
    </ui-card>
  `,
  styles: [`
    .results-card { margin-bottom: 24px; }
    .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .section-title { display: inline-flex; align-items: center; gap: 10px; color: var(--text-primary); }
    .section-title i { color: var(--accent); }
    .results-note { color: var(--text-muted); font-size: .75rem; }
    .eligibility-pill { display: inline-flex; align-items: center; border-radius: var(--radius-pill); padding: 4px 8px; font-size: .75rem; font-weight: 700; }
    .eligibility-pill.eligible { background: var(--state-success-bg); color: var(--state-success-fg); }
    .eligibility-pill.blocked { background: var(--surface-muted); color: var(--text-muted); }
    .empty-placeholder { display: grid; place-items: center; padding: 40px 20px; color: var(--text-muted); text-align: center; }
    .empty-placeholder i { display: block; margin-bottom: 8px; font-size: 2.5rem; }
    .empty-placeholder p { margin: 0; }
  `]
})
export class ResultsGridComponent {
  @Input() results: OrderRequestListItem[] = [];
  @Output() viewDetails = new EventEmitter<string>();

  canResend(row: OrderRequestListItem): boolean { return row.orderStatus != null && !RESEND_BLOCKED_STATUSES.has(row.orderStatus); }

  getStatusClass(status: number | null): string {
    switch (status) {
      case 9: return 'badge-success'; case 4: return 'badge-warning'; case 8: return 'badge-info';
      case 5: case 6: case 7: return 'badge-danger'; default: return 'badge-info';
    }
  }
}
