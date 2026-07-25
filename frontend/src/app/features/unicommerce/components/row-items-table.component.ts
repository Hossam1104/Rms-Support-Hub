import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RowItem } from '../../../core/models';

@Component({
  selector: 'app-row-items-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card-section glass-card">
      <div class="section-header">
        <div class="card-title">
          <i class="bi bi-list-stars"></i>
          <span>Invoice Row Items ({{ rowItems.length }})</span>
        </div>
        <button type="button" class="glass-button" (click)="openAddDialog.emit()"><i class="bi bi-plus-circle"></i> Add Row Item</button>
      </div>

      <div class="table-responsive" *ngIf="rowItems.length > 0; else emptyState">
        <table class="data-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Material #</th>
              <th>Barcode</th>
              <th>Qty</th>
              <th>Price</th>
              <th>Discount</th>
              <th>Gross</th>
              <th>VAT</th>
              <th>Net Amount</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of rowItems; track $index) {
              <tr>
                <td>{{ $index + 1 }}</td>
                <td><strong>{{ item.materialNumber }}</strong></td>
                <td>{{ item.barcode }}</td>
                <td>{{ item.quantity }}</td>
                <td>{{ item.itemPrice | number:'1.2-2' }}</td>
                <td>{{ item.itemDiscount | number:'1.2-2' }}</td>
                <td>{{ getGrossAmount(item) | number:'1.2-2' }}</td>
                <td>{{ getRowVat(item) | number:'1.2-2' }}</td>
                <td><strong>{{ getRowNet(item) | number:'1.2-2' }}</strong> SAR</td>
                <td>
                  <button type="button" class="btn-icon danger" (click)="deleteRowItem.emit($index)" title="Remove Item">
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
          <i class="bi bi-inbox"></i>
          <p>No row items in this invoice yet. Click "Add Row Item" to add SKUs.</p>
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
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.85rem; }
    .data-table th, .data-table td { padding: 10px 14px; border-bottom: 1px solid var(--glass-border); }
    .data-table th { background: var(--bg-tertiary); color: var(--text-secondary); font-weight: 600; }
    .data-table tbody tr:hover { background: var(--glass-hover-bg); }
    .btn-icon { background: none; border: none; font-size: 1.1rem; cursor: pointer; }
    .btn-icon.danger { color: var(--danger); }
    .empty-placeholder { text-align: center; padding: 40px 20px; color: var(--text-muted); }
    .empty-placeholder i { font-size: 2.5rem; margin-bottom: 8px; display: block; }
  `]
})
export class RowItemsTableComponent {
  @Input() rowItems: RowItem[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deleteRowItem = new EventEmitter<number>();

  getGrossAmount(item: RowItem): number {
    return (item.quantity || 0) * (item.itemPrice || 0);
  }

  getRowVat(item: RowItem): number {
    const price = item.itemPrice || 0;
    const disc = item.itemDiscount || 0;
    const vatPct = (item.vatPercentage || 0) / 100;
    return (price - disc) * vatPct * (item.quantity || 0);
  }

  getRowNet(item: RowItem): number {
    const gross = this.getGrossAmount(item);
    const discTotal = (item.itemDiscount || 0) * (item.quantity || 0);
    const vatTotal = this.getRowVat(item);
    return gross - discTotal + vatTotal;
  }
}
