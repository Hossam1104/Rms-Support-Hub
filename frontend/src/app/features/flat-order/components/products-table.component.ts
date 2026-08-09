import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Product } from '../../../core/models';
import { APP_ASSETS } from '../../../core/config/app-assets';
import { EmptyStateComponent, RiyalComponent, UiButtonComponent, UiTableComponent, UiToolbarComponent } from '../../../shared/ui';

export interface ProductUpdate {
  index: number;
  patch: Partial<Product>;
}

@Component({
  selector: 'app-products-table',
  standalone: true,
  imports: [CommonModule, EmptyStateComponent, RiyalComponent, UiButtonComponent, UiTableComponent, UiToolbarComponent],
  template: `
    <div id="products-card" class="table-panel">
      <ui-toolbar [compact]="true" [wrap]="true" role="toolbar" ariaLabel="Product actions">
        <div uiToolbarStart class="table-panel__heading">
          <i class="bi bi-box-seam" aria-hidden="true"></i>
          <span>Products</span>
          <span class="table-panel__count">{{ products.length }}</span>
        </div>
        <div uiToolbarEnd>
          <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddDialog.emit()">Add product</ui-button>
        </div>
      </ui-toolbar>

      <div class="table-errors" role="alert" *ngIf="errors.length > 0">
        <p *ngFor="let message of errors"><i class="bi bi-exclamation-circle" aria-hidden="true"></i>{{ message }}</p>
      </div>

      <ui-table *ngIf="products.length > 0; else productsEmpty" [dense]="false" [stickyHeader]="true" [zebra]="true" [wide]="true" caption="Order products">
        <thead>
          <tr>
            <th scope="col" class="numeric-cell">#</th>
            <th scope="col">Item</th>
            <th scope="col">Code</th>
            <th scope="col" class="numeric-cell">Unit price</th>
            <th scope="col" class="numeric-cell">Qty</th>
            <th scope="col" class="numeric-cell">Discount</th>
            <th scope="col" class="numeric-cell">VAT</th>
            <th scope="col" class="numeric-cell">Row total</th>
            <th scope="col"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let product of products; let index = index; trackBy: trackByIndex">
            <td class="muted-cell numeric-cell">{{ index + 1 }}</td>
            <td>
              <strong>{{ product.itemName }}</strong>
              <span class="secondary-label" *ngIf="product.itemNameAr">{{ product.itemNameAr }}</span>
              <span class="offer-mark" *ngIf="product.offerCode || product.offerMessage">
                <img [src]="assets.commerce.offer" alt="" aria-hidden="true">
                <span>{{ product.offerCode || 'Offer' }}</span>
              </span>
            </td>
            <td><span class="code-label">{{ product.itemCode }}</span></td>
            <td class="numeric-cell"><app-riyal [size]=".82"></app-riyal>{{ product.unitPrice | number:'1.2-2' }}</td>
            <td class="numeric-cell">
              <input class="table-editor table-editor--quantity" type="number" min="0" step="1" [value]="product.quantity" [attr.aria-label]="'Quantity for ' + product.itemName" (change)="onEdit(index, 'quantity', $event)">
            </td>
            <td class="numeric-cell">
              <label class="sr-only" [for]="'product-discount-' + index">Discount for {{ product.itemName }}</label>
              <span class="currency-editor">
                <app-riyal [decorative]="true" [size]=".72"></app-riyal>
                <input [id]="'product-discount-' + index" class="table-editor" type="number" min="0" step="0.01" [value]="product.discount" [attr.aria-label]="'Discount for ' + product.itemName" (change)="onEdit(index, 'discount', $event)">
              </span>
            </td>
            <td class="numeric-cell">{{ product.vatPercentage | number:'1.0-2' }}%</td>
            <td class="numeric-cell total-cell"><app-riyal [size]=".82"></app-riyal>{{ getProductTotal(product) | number:'1.2-2' }}</td>
            <td class="action-cell">
              <ui-button variant="danger" size="sm" icon="bi bi-trash3" ariaLabel="Remove product" (pressed)="deleteProduct.emit(index)"></ui-button>
            </td>
          </tr>
        </tbody>
      </ui-table>

      <ng-template #productsEmpty>
        <app-empty-state icon="bi-box-seam" title="No products yet" description="Add an item to start building the order.">
          <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddDialog.emit()">Add product</ui-button>
        </app-empty-state>
      </ng-template>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .table-panel { min-width: 0; }
    .table-panel__heading { display: inline-flex; align-items: center; gap: 8px; color: var(--text-primary); font-size: var(--text-md); font-weight: var(--weight-heavy); }
    .table-panel__heading > i { color: var(--accent); }
    .table-panel__count { display: inline-grid; min-width: 22px; height: 22px; place-items: center; padding-inline: 5px; border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: .7rem; }
    .table-errors { display: flex; flex-direction: column; gap: 5px; margin: 12px 0; padding: 10px 12px; border: 1px solid var(--state-danger-border); border-radius: var(--radius-md); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .table-errors p { display: flex; align-items: flex-start; gap: 7px; margin: 0; font-size: .78rem; line-height: 1.35; }
    td strong { display: block; font-weight: 750; }
    .secondary-label { display: block; margin-top: 2px; color: var(--text-muted); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .offer-mark { display: inline-flex; align-items: center; gap: 4px; margin-top: 4px; color: var(--state-warning-fg); font-size: var(--text-xs); font-weight: var(--weight-bold); }
    .offer-mark img { width: 16px; height: 16px; object-fit: contain; }
    .code-label { color: var(--text-secondary); font-family: var(--font-mono, ui-monospace, monospace); font-size: var(--text-sm); }
    .numeric-cell { white-space: nowrap; font-variant-numeric: tabular-nums; }
    .total-cell { color: var(--text-primary); font-weight: 800; }
    .muted-cell { color: var(--text-muted); }
    .action-cell { width: 50px; text-align: right; }
    .currency-editor { display: inline-flex; align-items: center; justify-content: flex-end; gap: 3px; }
    .currency-editor app-riyal { color: var(--text-muted); }
    .table-editor { width: 82px; min-height: var(--control-height-compact); box-sizing: border-box; padding: 0 var(--space-2); border: 1px solid var(--input-border); border-radius: var(--radius-sm); background: var(--input-bg); color: var(--text-primary); font: inherit; font-size: var(--text-sm); }
    .table-editor--quantity { width: 68px; }
    .table-editor:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    @media (max-width: 767px) { .table-panel__heading { flex: 1 1 auto; } }
  `]
})
export class ProductsTableComponent {
  readonly assets = APP_ASSETS;
  @Input() products: Product[] = [];
  /** U4: server send-validation errors routed to the products section. */
  @Input() errors: string[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deleteProduct = new EventEmitter<number>();
  @Output() updateProduct = new EventEmitter<ProductUpdate>();

  trackByIndex(index: number): number { return index; }

  onEdit(index: number, field: 'quantity' | 'discount', event: Event) {
    const raw = (event.target as HTMLInputElement).value;
    const value = Number(raw);
    this.updateProduct.emit({ index, patch: { [field]: Number.isFinite(value) ? value : 0 } });
  }

  /** Existing display convention retained: flat row discount is applied once,
   * then VAT is calculated on the discounted row subtotal. Server totals stay
   * authoritative for the summary rail. */
  getProductTotal(prod: Product): number {
    const qty = prod.quantity || 0;
    const price = prod.unitPrice || 0;
    const discount = prod.discount || 0;
    const vatPct = prod.vatPercentage || 0;
    const rowSubtotal = qty * price;
    const totalVat = Math.round((rowSubtotal - discount) * (vatPct / 100) * 100) / 100;
    return Math.round((rowSubtotal - discount + totalVat) * 100) / 100;
  }
}
