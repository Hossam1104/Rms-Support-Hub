import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RowItem } from '../../../core/models';
import { UiButtonComponent, UiCardComponent, UiTableComponent } from '../../../shared/ui';

@Component({
  selector: 'app-row-items-table',
  standalone: true,
  imports: [CommonModule, UiButtonComponent, UiCardComponent, UiTableComponent],
  template: `
    <ui-card variant="raised" class="card-section">
      <div uiCardHeader class="section-header">
        <span class="section-title"><i class="bi bi-list-stars" aria-hidden="true"></i> Invoice Row Items ({{ rowItems.length }})</span>
        <ui-button size="sm" icon="bi-plus-circle" (pressed)="openAddDialog.emit()">Add Row Item</ui-button>
      </div>

      <ui-table *ngIf="rowItems.length > 0; else emptyState" [dense]="true" [stickyHeader]="true" caption="Invoice row items">
        <thead>
          <tr>
            <th>#</th><th>Material #</th><th>Barcode</th><th>Qty</th><th>Price</th><th>Discount</th><th>Gross</th><th>VAT</th><th>Net Amount</th><th>Actions</th>
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
                <button type="button" class="delete-button" (click)="deleteRowItem.emit($index)" title="Remove Item" aria-label="Remove item">
                  <i class="bi bi-trash" aria-hidden="true"></i>
                </button>
              </td>
            </tr>
          }
        </tbody>
      </ui-table>

      <ng-template #emptyState>
        <div class="empty-placeholder">
          <i class="bi bi-inbox" aria-hidden="true"></i>
          <p>No row items in this invoice yet. Click &quot;Add Row Item&quot; to add SKUs.</p>
        </div>
      </ng-template>
    </ui-card>
  `,
  styles: [`
    .card-section { margin-bottom: 24px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
    .section-title { display: inline-flex; align-items: center; gap: 10px; color: var(--text-primary); }
    .section-title i { color: var(--accent); }
    .delete-button { display: grid; place-items: center; width: 32px; height: 32px; border: 1px solid transparent; border-radius: var(--radius-sm); background: transparent; color: var(--state-danger-fg); cursor: pointer; }
    .delete-button:hover { background: var(--state-danger-bg); border-color: var(--state-danger-border); }
    .delete-button:focus-visible { outline: none; box-shadow: var(--focus-ring-danger); }
    .empty-placeholder { display: grid; place-items: center; padding: 40px 20px; color: var(--text-muted); text-align: center; }
    .empty-placeholder i { display: block; margin-bottom: 8px; font-size: 2.5rem; }
    .empty-placeholder p { margin: 0; }
    @media (max-width: 720px) { .section-header { align-items: flex-start; flex-direction: column; } }
  `]
})
export class RowItemsTableComponent {
  @Input() rowItems: RowItem[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deleteRowItem = new EventEmitter<number>();

  getGrossAmount(item: RowItem): number { return (item.quantity || 0) * (item.itemPrice || 0); }
  getRowVat(item: RowItem): number {
    return ((item.itemPrice || 0) - (item.itemDiscount || 0)) * ((item.vatPercentage || 0) / 100) * (item.quantity || 0);
  }
  getRowNet(item: RowItem): number {
    return this.getGrossAmount(item) - (item.itemDiscount || 0) * (item.quantity || 0) + this.getRowVat(item);
  }
}
