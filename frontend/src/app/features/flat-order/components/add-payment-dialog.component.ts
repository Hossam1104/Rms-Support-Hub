import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Payment } from '../../../core/models';
import { RiyalComponent, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';

@Component({
  selector: 'app-add-payment-dialog',
  standalone: true,
  imports: [CommonModule, RiyalComponent, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSelectComponent],
  template: `
    <div class="modal-backdrop" (click)="close.emit()">
      <ui-card variant="raised" class="modal-dialog" (click)="$event.stopPropagation()">
        <div uiCardHeader class="modal-header">
          <h3><i class="bi bi-wallet2" aria-hidden="true"></i> Add Payment Method</h3>
          <ui-button variant="ghost" size="sm" icon="bi-x-lg" ariaLabel="Close" (pressed)="close.emit()"></ui-button>
        </div>

        <div class="form-grid">
          <ui-field label="Payment Method" forId="payment-method" [required]="true">
            <ui-select selectId="payment-method" [options]="availablePaymentMethodOptions" [value]="payment.paymentMethod" (valueChange)="payment.paymentMethod = $any($event) || ''; onMethodChange()"></ui-select>
          </ui-field>
          <ui-field label="Payment Status" forId="payment-status" [required]="true">
            <ui-select selectId="payment-status" [options]="paymentStatusOptions" [value]="payment.paymentStatus" (valueChange)="payment.paymentStatus = $any($event) || ''"></ui-select>
          </ui-field>
          <ui-field label="Amount" forId="payment-amount" [required]="true">
            <ui-input inputId="payment-amount" type="number" step="0.01" [value]="payment.paymentAmount" (valueChange)="payment.paymentAmount = $any($event) || 0">
              <app-riyal uiInputSuffix [size]=".9"></app-riyal>
            </ui-input>
          </ui-field>
          <ui-field label="Transaction ID" forId="payment-transaction-id">
            <ui-input inputId="payment-transaction-id" placeholder="Optional" [value]="payment.transactionId || ''" (valueChange)="payment.transactionId = $any($event) || ''"></ui-input>
          </ui-field>
          <ui-field *ngIf="payment.paymentMethod === 'PostToCredit' && moduleKey === 'ghc_ecommerce'" label="Credit Customer Name" forId="payment-customer-name" [required]="true" class="full-width">
            <ui-input inputId="payment-customer-name" [value]="payment.customerName || ''" (valueChange)="payment.customerName = $any($event) || ''"></ui-input>
          </ui-field>
          <ui-field *ngIf="payment.paymentMethod === 'PostToCredit' && moduleKey === 'ghc_ecommerce'" label="Credit Customer Number" forId="payment-customer-number" [required]="true" class="full-width">
            <ui-input inputId="payment-customer-number" [value]="payment.customerNumber || ''" (valueChange)="payment.customerNumber = $any($event) || ''"></ui-input>
          </ui-field>
        </div>

        <div uiCardFooter class="modal-footer">
          <ui-button variant="secondary" (pressed)="close.emit()">Cancel</ui-button>
          <ui-button icon="bi-plus-circle" (pressed)="onAdd()">Add Payment</ui-button>
        </div>
      </ui-card>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; inset: 0; z-index: 2000; display: grid; place-items: center; padding: 16px; background: var(--backdrop); backdrop-filter: blur(3px); }
    .modal-dialog { width: min(620px, 100%); max-height: min(760px, calc(100vh - 32px)); overflow: auto; }
    .modal-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
    .modal-header h3 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 1.1rem; }
    .modal-header h3 i { color: var(--accent); }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; }
    @media (max-width: 560px) { .form-grid { grid-template-columns: 1fr; } }
  `]
})
export class AddPaymentDialogComponent {
  @Input() moduleKey = '';
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<Payment>();

  readonly paymentMethodOptions: UiSelectOption[] = [
    { value: 'CashOnDelivery', label: 'CashOnDelivery' }, { value: 'Visa', label: 'Visa' }, { value: 'Tamara', label: 'Tamara' },
    { value: 'Tabby', label: 'Tabby' }, { value: 'ApplePay', label: 'ApplePay' }, { value: 'STCPay', label: 'STCPay' }, { value: 'Mada', label: 'Mada' },
    { value: 'PostToCredit', label: 'PostToCredit' }
  ];
  get availablePaymentMethodOptions(): UiSelectOption[] {
    return this.paymentMethodOptions.filter(option => option.value !== 'PostToCredit' || this.moduleKey !== 'upc_ecommerce');
  }
  readonly paymentStatusOptions: UiSelectOption[] = [
    { value: 'not_payment', label: 'not_payment (COD)' }, { value: 'done_payment', label: 'done_payment (Paid)' }, { value: 'failed_payment', label: 'failed_payment' }
  ];
  payment: Payment = { paymentMethod: 'CashOnDelivery', paymentStatus: 'not_payment', paymentAmount: 0, transactionId: '', customerName: '', customerNumber: '' };

  onMethodChange() { this.payment.paymentStatus = this.payment.paymentMethod === 'CashOnDelivery' ? 'not_payment' : 'done_payment'; }
  onAdd() {
    if (this.payment.paymentMethod && this.payment.paymentStatus && this.payment.paymentAmount > 0) this.add.emit(this.payment);
  }
}
