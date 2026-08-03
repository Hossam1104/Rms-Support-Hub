import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Product } from '../../../core/models';

@Component({
  selector: 'app-products-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card-section glass-card" id="products-card">
      <div class="section-header">
        <div class="card-title">
          <i class="bi bi-box-seam"></i>
          <span>Order Products ({{ products.length }})</span>
        </div>
        <button type="button" class="glass-button" (click)="openAddDialog.emit()"><i class="bi bi-plus-circle"></i> Add Product</button>
      </div>

      <div class="section-errors" role="alert" *ngIf="errors.length > 0">
        <p class="field-error" *ngFor="let message of errors">{{ message }}</p>
      </div>

      <div class="table-responsive" *ngIf="products.length > 0; else emptyState">
        <table class="data-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Item Code</th>
              <th>Item Name</th>
              <th>Qty</th>
              <th>Unit Price</th>
              <th>VAT %</th>
              <th>Discount</th>
              <th>Total</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (prod of products; track $index) {
              <tr>
                <td>{{ $index + 1 }}</td>
                <td><strong>{{ prod.itemCode }}</strong></td>
                <td>{{ prod.itemName }}</td>
                <td>{{ prod.quantity }}</td>
                <td>{{ prod.unitPrice | number:'1.2-2' }}</td>
                <td>{{ prod.vatPercentage }}%</td>
                <td>{{ prod.discount | number:'1.2-2' }}</td>
                <td><strong>{{ getProductTotal(prod) | number:'1.2-2' }}</strong> SAR</td>
                <td>
                  <button type="button" class="btn-icon danger" (click)="deleteProduct.emit($index)" title="Remove Product">
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
          <p>No products added yet. Click "Add Product" to add items to this order.</p>
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
    .btn-icon { background: none; border: none; font-size: 1.1rem; cursor: pointer; }
    .btn-icon.danger { color: var(--danger); }
    .empty-placeholder { text-align: center; padding: 40px 20px; color: var(--text-muted); }
    .empty-placeholder i { font-size: 2.5rem; margin-bottom: 8px; display: block; }
    .section-errors { border: 1px solid var(--danger); border-radius: var(--radius-md); background: var(--danger-bg); padding: 8px 14px; margin-bottom: 12px; }
    .field-error { font-size: 0.8rem; color: var(--danger); margin: 2px 0; }
  `]
})
export class ProductsTableComponent {
  @Input() products: Product[] = [];
  /** U4: server send-validation errors routed to the products section (see
   * send-validation.ts). */
  @Input() errors: string[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deleteProduct = new EventEmitter<number>();

  /** Row total (row_net_total) using the project's verified calculation
   * convention -- Product.cs EstimatedTotal: quantity * unitPrice, minus
   * the flat row-level discount ONCE (not per unit), plus VAT on the
   * discounted subtotal. Display-only; the server owns the real totals. */
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
