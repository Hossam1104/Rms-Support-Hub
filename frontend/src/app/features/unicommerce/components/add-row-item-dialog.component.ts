import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-add-row-item-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop glass-panel" (click)="close.emit()">
      <div class="modal-dialog glass-card fade-in-up" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h3><i class="bi bi-list-stars"></i> Add Row Item</h3>
          <button type="button" class="btn-close" (click)="close.emit()">&times;</button>
        </div>

        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">Material Number *</label>
              <input type="text" class="glass-input" [(ngModel)]="item.materialNumber" placeholder="6-digit material #" required />
            </div>
            <div class="form-group">
              <label class="form-label">Barcode *</label>
              <input type="text" class="glass-input" [(ngModel)]="item.barcode" required />
            </div>
            <div class="form-group">
              <label class="form-label">Quantity *</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="item.quantity" required />
            </div>
            <div class="form-group">
              <label class="form-label">Item Price (SAR) *</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="item.itemPrice" required />
            </div>
            <div class="form-group">
              <label class="form-label">Item Discount (SAR)</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="item.itemDiscount" />
            </div>
            <div class="form-group">
              <label class="form-label">VAT %</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="item.vatPercentage" />
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <button type="button" class="btn-secondary glass-input" (click)="close.emit()">Cancel</button>
          <button type="button" class="glass-button" (click)="onAdd()">Add Row Item</button>
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
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 24px; }
  `]
})
export class AddRowItemDialogComponent {
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<any>();

  item = {
    materialNumber: '',
    barcode: '',
    quantity: 1,
    itemPrice: 0,
    itemDiscount: 0,
    vatPercentage: 15
  };

  onAdd() {
    if (this.item.materialNumber && this.item.quantity > 0) {
      if (!this.item.barcode) this.item.barcode = `BC${this.item.materialNumber}`;
      this.add.emit(this.item);
    }
  }
}
