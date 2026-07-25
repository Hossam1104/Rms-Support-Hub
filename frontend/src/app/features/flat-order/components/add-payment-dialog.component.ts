import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-add-payment-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop glass-panel" (click)="close.emit()">
      <div class="modal-dialog glass-card fade-in-up" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h3><i class="bi bi-wallet2"></i> Add Payment Method</h3>
          <button type="button" class="btn-close" (click)="close.emit()">&times;</button>
        </div>

        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">Payment Method *</label>
              <select class="glass-input" [(ngModel)]="payment.paymentMethod" (change)="onMethodChange()" required>
                <option value="">Select Method</option>
                <option value="CashOnDelivery">CashOnDelivery</option>
                <option value="Visa">Visa</option>
                <option value="Tamara">Tamara</option>
                <option value="Tabby">Tabby</option>
                <option value="ApplePay">ApplePay</option>
                <option value="STCPay">STCPay</option>
                <option value="Mada">Mada</option>
                <option value="PostToCredit" *ngIf="moduleKey !== 'upc_ecommerce'">PostToCredit</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">Payment Status *</label>
              <select class="glass-input" [(ngModel)]="payment.paymentStatus" required>
                <option value="not_payment">not_payment (COD)</option>
                <option value="done_payment">done_payment (Paid)</option>
                <option value="failed_payment">failed_payment</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">Amount (SAR) *</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="payment.paymentAmount" required />
            </div>
            <div class="form-group">
              <label class="form-label">Transaction ID</label>
              <input type="text" class="glass-input" [(ngModel)]="payment.transactionId" placeholder="Optional" />
            </div>

            <div class="form-group full-width" *ngIf="payment.paymentMethod === 'PostToCredit' && moduleKey === 'ghc_ecommerce'">
              <label class="form-label">Credit Customer Name *</label>
              <input type="text" class="glass-input" [(ngModel)]="payment.customerName" required />
            </div>
            <div class="form-group full-width" *ngIf="payment.paymentMethod === 'PostToCredit' && moduleKey === 'ghc_ecommerce'">
              <label class="form-label">Credit Customer Number *</label>
              <input type="text" class="glass-input" [(ngModel)]="payment.customerNumber" required />
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <button type="button" class="btn-secondary glass-input" (click)="close.emit()">Cancel</button>
          <button type="button" class="glass-button" (click)="onAdd()">Add Payment</button>
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
    .full-width { grid-column: 1 / -1; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 24px; }
  `]
})
export class AddPaymentDialogComponent {
  @Input() moduleKey: string = '';
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<any>();

  payment = {
    paymentMethod: 'CashOnDelivery',
    paymentStatus: 'not_payment',
    paymentAmount: 0,
    transactionId: '',
    customerName: '',
    customerNumber: ''
  };

  onMethodChange() {
    if (this.payment.paymentMethod === 'CashOnDelivery') {
      this.payment.paymentStatus = 'not_payment';
    } else {
      this.payment.paymentStatus = 'done_payment';
    }
  }

  onAdd() {
    if (this.payment.paymentMethod && this.payment.paymentStatus && this.payment.paymentAmount > 0) {
      this.add.emit(this.payment);
    }
  }
}
