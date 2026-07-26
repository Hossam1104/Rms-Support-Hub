import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Product } from '../../../core/models';

@Component({
  selector: 'app-add-product-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop glass-panel" (click)="close.emit()">
      <div class="modal-dialog glass-card fade-in-up" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h3><i class="bi bi-box-seam"></i> Add Product</h3>
          <button type="button" class="btn-close" (click)="close.emit()">&times;</button>
        </div>

        <div class="modal-body">
          <div class="search-bar mb-3">
            <div class="input-group">
              <input type="text" class="glass-input" [(ngModel)]="searchCode" placeholder="Enter item/material number..." maxlength="18" [disabled]="!branchCode.trim()" />
              <button type="button" class="glass-button" [disabled]="!branchCode.trim()" (click)="onItemLookup()"><i class="bi bi-search"></i> Find Item</button>
            </div>
            <p class="branch-hint" *ngIf="!branchCode.trim()">Set a branch code in Order Header Information first -- item pricing is branch-specific.</p>
          </div>

          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">Item Code *</label>
              <input type="text" class="glass-input" [(ngModel)]="product.itemCode" required />
            </div>
            <div class="form-group">
              <label class="form-label">Item Name *</label>
              <input type="text" class="glass-input" [(ngModel)]="product.itemName" required />
            </div>
            <div class="form-group">
              <label class="form-label">Quantity *</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.quantity" required />
            </div>
            <div class="form-group">
              <label class="form-label">Unit Price (SAR) *</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.unitPrice" required />
            </div>
            <div class="form-group">
              <label class="form-label">VAT %</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.vatPercentage" />
            </div>
            <div class="form-group">
              <label class="form-label">Discount (SAR)</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.discount" />
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <button type="button" class="btn-secondary glass-input" (click)="close.emit()">Cancel</button>
          <button type="button" class="glass-button" (click)="onAdd()">Add Product</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 2000; display: flex; align-items: center; justify-content: center; }
    .modal-dialog { width: 100%; max-width: 600px; padding: 24px; border-radius: var(--radius-lg); }
    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .modal-header h3 { margin: 0; font-size: 1.2rem; display: flex; align-items: center; gap: 8px; }
    .btn-close { background: none; border: none; font-size: 1.5rem; color: var(--text-muted); cursor: pointer; }
    .input-group { display: flex; gap: 8px; }
    .input-group input { flex: 1; }
    .branch-hint { font-size: 0.78rem; color: var(--warning); margin: 8px 0 0; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-top: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 24px; }
  `]
})
export class AddProductDialogComponent {
  @Input() moduleKey: string = '';
  @Input() branchCode: string = '';
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<Product>();
  @Output() lookupItem = new EventEmitter<{ code: string, branchCode: string }>();

  searchCode: string = '';
  product: Product = {
    itemCode: '',
    itemName: '',
    quantity: 1,
    unitPrice: 0,
    vatPercentage: 15,
    discount: 0
  };

  onItemLookup() {
    if (this.searchCode.trim()) {
      this.lookupItem.emit({ code: this.searchCode.trim(), branchCode: this.branchCode });
    }
  }

  onAdd() {
    if (this.product.itemCode && this.product.itemName && this.product.quantity > 0) {
      this.add.emit(this.product);
    }
  }
}
