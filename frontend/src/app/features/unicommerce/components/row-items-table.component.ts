import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RowItem } from '../../../core/models';
import { RiyalComponent, UiButtonComponent, UiCardComponent, UiTableComponent } from '../../../shared/ui';

@Component({
  selector: 'app-row-items-table',
  standalone: true,
  imports: [CommonModule, RiyalComponent, UiButtonComponent, UiCardComponent, UiTableComponent],
  template: `
    <ui-card variant="raised" class="card-section">
      <div uiCardHeader class="section-header">
        <span class="section-title"><i class="bi bi-list-stars" aria-hidden="true"></i> Invoice Row Items ({{ rowItems.length }})</span>
        <ui-button size="sm" icon="bi-plus-circle" (pressed)="openAddDialog.emit()">Add Row Item</ui-button>
      </div>

      <ui-table *ngIf="rowItems.length > 0; else emptyState" [dense]="true" [stickyHeader]="true" [wide]="true" caption="Invoice row items">
        <thead>
          <tr>
            <th class="numeric-cell">#</th><th>Material #</th><th>Barcode</th><th class="numeric-cell">Qty</th><th class="numeric-cell">Price <span class="sr-only">Saudi Riyal</span></th><th class="numeric-cell">Discount <span class="sr-only">Saudi Riyal</span></th><th class="numeric-cell">Gross <span class="sr-only">Saudi Riyal</span></th><th class="numeric-cell">VAT <span class="sr-only">Saudi Riyal</span></th><th class="numeric-cell">Net Amount <span class="sr-only">Saudi Riyal</span></th><th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (item of rowItems; track $index) {
            <tr>
              <td class="numeric-cell">{{ $index + 1 }}</td>
              <td><strong>{{ item.materialNumber }}</strong></td>
              <td>{{ item.barcode }}</td>
              <td class="numeric-cell">{{ item.quantity }}</td>
              <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ item.itemPrice | number:'1.2-2' }}</td>
              <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ item.itemDiscount | number:'1.2-2' }}</td>
              <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ getGrossAmount(item) | number:'1.2-2' }}</td>
              <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ getRowVat(item) | number:'1.2-2' }}</td>
              <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal><strong>{{ getRowNet(item) | number:'1.2-2' }}</strong></td>
              <td>
                <button type="button" class="delete-button" (click)="deleteRowItem.emit($index)" title="Remove Item" aria-label="Remove item">
                  <i class="bi bi-trash" aria-hidden="true"></i>
                </button>
              </td>
            </tr>
          }
        </tbody>
        <tfoot>
          <tr class="totals-row">
            <th scope="row" colspan="3">Totals</th>
            <td class="numeric-cell">{{ totalQuantity() }}</td>
            <td class="numeric-cell">—</td>
            <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ totalDiscount() | number:'1.2-2' }}</td>
            <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ totalGross() | number:'1.2-2' }}</td>
            <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal>{{ totalVat() | number:'1.2-2' }}</td>
            <td class="money numeric-cell"><app-riyal [decorative]="true" [size]=".78"></app-riyal><strong>{{ totalNet() | number:'1.2-2' }}</strong></td>
            <td></td>
          </tr>
        </tfoot>
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
    .card-section { margin-bottom: var(--section-gap); }
    .money { white-space: nowrap; }
    .money app-riyal { margin-inline-end: 3px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; gap: var(--panel-gap); }
    .section-title { display: inline-flex; align-items: center; gap: 10px; color: var(--text-primary); }
    .section-title i { color: var(--accent); }
    .delete-button { display: grid; place-items: center; width: 32px; height: 32px; border: 1px solid transparent; border-radius: var(--radius-sm); background: transparent; color: var(--state-danger-fg); cursor: pointer; }
    .delete-button:hover { background: var(--state-danger-bg); border-color: var(--state-danger-border); }
    .delete-button:focus-visible { outline: none; box-shadow: var(--focus-ring-danger); }
    .totals-row th, .totals-row td { white-space: nowrap; }
    .empty-placeholder { display: grid; place-items: center; padding: var(--section-gap) var(--panel-padding); color: var(--text-muted); text-align: center; }
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

  /** Display-only rollups for the existing invoice row values. These do not
   * feed the draft or send payload; the API remains authoritative for order
   * calculations. */
  totalQuantity(): number {
    return this.rowItems.reduce((total, item) => total + (item.quantity || 0), 0);
  }

  totalDiscount(): number {
    return this.rowItems.reduce((total, item) => total + (item.itemDiscount || 0) * (item.quantity || 0), 0);
  }

  totalGross(): number {
    return this.rowItems.reduce((total, item) => total + this.getGrossAmount(item), 0);
  }

  totalVat(): number {
    return this.rowItems.reduce((total, item) => total + this.getRowVat(item), 0);
  }

  totalNet(): number {
    return this.rowItems.reduce((total, item) => total + this.getRowNet(item), 0);
  }
}
