import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Payment } from '../../../core/models';
import { RiyalComponent, UiButtonComponent, UiCardComponent, UiDropdownOption, UiDropdownSelectComponent, UiFieldComponent, UiInputComponent } from '../../../shared/ui';
import { PAYMENT_STATUS_OPTIONS, defaultPaymentMethod, defaultPaymentStatus, paymentMethodOptions } from '../payment-policy';

@Component({
  selector: 'app-add-payment-dialog',
  standalone: true,
  imports: [CommonModule, RiyalComponent, UiButtonComponent, UiCardComponent, UiDropdownSelectComponent, UiFieldComponent, UiInputComponent],
  template: `
    <div class="modal-backdrop" role="presentation">
      <ui-card variant="raised" class="modal-dialog" (click)="$event.stopPropagation()">
        <div uiCardHeader class="modal-header">
          <h3><i class="bi bi-wallet2" aria-hidden="true"></i> Add Payment Method</h3>
          <ui-button variant="ghost" size="sm" icon="bi-x-lg" ariaLabel="Close" (pressed)="close.emit()"></ui-button>
        </div>

        <div class="form-grid">
          <ui-field label="Payment Method" forId="payment-method" [required]="true">
            <ui-dropdown-select buttonId="payment-method" ariaLabel="Payment method" [options]="availablePaymentMethodOptions" [value]="payment.paymentMethod" (valueChange)="onMethodChange($event)"></ui-dropdown-select>
          </ui-field>
          <ui-field label="Payment Status" forId="payment-status" [required]="true">
            <ui-dropdown-select buttonId="payment-status" ariaLabel="Payment status" [options]="paymentStatusOptions" [value]="payment.paymentStatus" (valueChange)="payment.paymentStatus = $event"></ui-dropdown-select>
          </ui-field>
          <ui-field label="Amount" forId="payment-amount" [required]="true">
            <ui-input inputId="payment-amount" type="number" step="0.01" [value]="payment.paymentAmount" (valueChange)="onAmountChange($event)">
              <app-riyal uiInputSuffix [size]=".9"></app-riyal>
            </ui-input>
            <small class="amount-hint" *ngIf="payment.paymentMethod !== 'COD' && requiredAmount > 0">Required amount: {{ requiredAmount | number:'1.2-2' }}</small>
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
    .amount-hint { display: block; margin-top: 5px; color: var(--text-secondary); font-size: .72rem; }
    .full-width { grid-column: 1 / -1; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; }
    @media (max-width: 560px) { .form-grid { grid-template-columns: 1fr; } }
  `]
})
export class AddPaymentDialogComponent implements OnChanges {
  @Input() moduleKey = '';
  @Input() requiredAmount = 0;
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<Payment>();

  /** The methods the active module accepts, in policy order. UPC offers only
   * Visa, Tamara and Tabby; GHC keeps the full legacy list. */
  get availablePaymentMethodOptions(): UiDropdownOption[] {
    return paymentMethodOptions(this.moduleKey);
  }
  readonly paymentStatusOptions = PAYMENT_STATUS_OPTIONS;
  payment: Payment = { paymentMethod: 'COD', paymentStatus: 'not_payment', paymentAmount: 0, transactionId: '', customerName: '', customerNumber: '' };
  private amountAutoFilled = false;

  ngOnChanges(changes: SimpleChanges) {
    // The dialog is recreated on every open, so this is also the first-open
    // path: start on a method the active module actually accepts rather than
    // on whatever the previous module left behind.
    if (changes['moduleKey']) this.resetForModule();

    if (changes['requiredAmount'] && this.amountAutoFilled && this.payment.paymentMethod !== 'COD') {
      this.payment.paymentAmount = this.normalizedRequiredAmount();
    }
  }

  onMethodChange(method: string) {
    this.payment.paymentMethod = method;
    this.payment.paymentStatus = this.statusForMethod();
    if (this.payment.paymentMethod !== 'COD') {
      this.payment.paymentAmount = this.normalizedRequiredAmount();
      this.amountAutoFilled = true;
    } else {
      this.payment.paymentAmount = 0;
      this.amountAutoFilled = false;
    }
  }

  private resetForModule() {
    const method = defaultPaymentMethod(this.moduleKey);
    this.payment = {
      paymentMethod: method,
      paymentStatus: defaultPaymentStatus(method),
      paymentAmount: method === 'COD' ? 0 : this.normalizedRequiredAmount(),
      transactionId: '',
      customerName: '',
      customerNumber: ''
    };
    this.amountAutoFilled = method !== 'COD';
  }

  onAmountChange(value: unknown) {
    this.payment.paymentAmount = Number(value) || 0;
    this.amountAutoFilled = false;
  }

  onAdd() {
    this.payment.paymentStatus = this.statusForMethod();
    if (this.payment.paymentMethod && this.payment.paymentStatus && this.payment.paymentAmount > 0) this.add.emit({ ...this.payment });
  }

  private statusForMethod(): string {
    return defaultPaymentStatus(this.payment.paymentMethod);
  }

  private normalizedRequiredAmount(): number {
    const amount = Number(this.requiredAmount);
    return Number.isFinite(amount) && amount > 0 ? amount : 0;
  }
}
