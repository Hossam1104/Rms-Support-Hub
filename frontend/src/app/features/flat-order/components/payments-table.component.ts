import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Payment } from '../../../core/models';
import { AssetPath, paymentAssetForMethod } from '../../../core/config/app-assets';
import { EmptyStateComponent, RiyalComponent, UiButtonComponent, UiTableComponent, UiToolbarComponent } from '../../../shared/ui';

export interface PaymentUpdate {
  index: number;
  patch: Partial<Payment>;
}

@Component({
  selector: 'app-payments-table',
  standalone: true,
  imports: [CommonModule, EmptyStateComponent, RiyalComponent, UiButtonComponent, UiTableComponent, UiToolbarComponent],
  template: `
    <div id="payments-card" class="table-panel">
      <ui-toolbar [compact]="true" [wrap]="true" role="toolbar" ariaLabel="Payment actions">
        <div uiToolbarStart class="table-panel__heading">
          <i class="bi bi-wallet2" aria-hidden="true"></i>
          <span>Payments</span>
          <span class="table-panel__count">{{ payments.length }}</span>
        </div>
        <div uiToolbarEnd>
          <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddDialog.emit()">Add payment</ui-button>
        </div>
      </ui-toolbar>

      <div class="table-errors" role="alert" *ngIf="errors.length > 0">
        <p *ngFor="let message of errors"><i class="bi bi-exclamation-circle" aria-hidden="true"></i>{{ message }}</p>
      </div>

      <ui-table *ngIf="payments.length > 0; else paymentsEmpty" [dense]="false" [stickyHeader]="true" [zebra]="true" [wide]="true" caption="Order payments">
        <thead>
          <tr>
            <th scope="col" class="numeric-cell">#</th>
            <th scope="col">Method</th>
            <th scope="col">Status</th>
            <th scope="col" class="numeric-cell">Amount</th>
            <th scope="col">Transaction / reference</th>
            <th scope="col">Method metadata</th>
            <th scope="col"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let payment of payments; let index = index; trackBy: trackByIndex">
            <td class="muted-cell numeric-cell">{{ index + 1 }}</td>
            <td>
              <span class="payment-method">
                <span class="payment-logo" aria-hidden="true">
                  @if (paymentAsset(payment.paymentMethod); as logo) {
                    <img [src]="logo" alt="">
                  } @else {
                    <i class="bi bi-credit-card-2-front"></i>
                  }
                </span>
                <span class="payment-method__copy">
                  <strong>{{ payment.paymentMethod }}</strong>
                  <span class="secondary-label" *ngIf="payment.paymentOption">{{ payment.paymentOption }}</span>
                </span>
              </span>
            </td>
            <td>
              <label class="sr-only" [for]="'payment-status-' + index">Status for {{ payment.paymentMethod }}</label>
              <select [id]="'payment-status-' + index" class="table-editor table-select" [value]="payment.paymentStatus" (change)="onEdit(index, 'paymentStatus', $event)">
                <option *ngFor="let status of paymentStatuses" [value]="status">{{ status }}</option>
              </select>
            </td>
            <td class="numeric-cell">
              <label class="sr-only" [for]="'payment-amount-' + index">Amount for {{ payment.paymentMethod }}</label>
              <span class="amount-editor"><app-riyal [size]=".82"></app-riyal><input [id]="'payment-amount-' + index" class="table-editor" type="number" min="0" step="0.01" [value]="payment.paymentAmount" (change)="onEdit(index, 'paymentAmount', $event)"></span>
            </td>
            <td>
              <span class="reference-label">{{ payment.transactionId || 'No transaction reference' }}</span>
            </td>
            <td><span class="metadata-label">{{ metadata(payment) }}</span></td>
            <td class="action-cell">
              <ui-button variant="danger" size="sm" icon="bi bi-trash3" ariaLabel="Remove payment" (pressed)="deletePayment.emit(index)"></ui-button>
            </td>
          </tr>
        </tbody>
        <tfoot>
          <tr>
            <th scope="row" colspan="3">Payment total</th>
            <td class="numeric-cell total-cell"><app-riyal [decorative]="true" [size]=".82"></app-riyal>{{ totalAmount() | number:'1.2-2' }}</td>
            <td colspan="3"><span class="sr-only">Sum of payment amounts</span></td>
          </tr>
        </tfoot>
      </ui-table>

      <ng-template #paymentsEmpty>
        <app-empty-state icon="bi-cash-coin" title="Cash on Delivery" description="No payment method is selected, so this order is sent as Cash on Delivery. Add a payment only if it was settled up front." data-testid="payments-cod-state">
          <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddDialog.emit()">Add payment</ui-button>
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
    .secondary-label, .metadata-label { display: block; margin-top: 2px; color: var(--text-muted); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .payment-method { display: inline-flex; align-items: center; min-width: 150px; gap: 8px; }
    .payment-logo { display: inline-grid; width: 32px; height: 25px; flex: 0 0 32px; place-items: center; border: 1px solid var(--border-subtle); border-radius: var(--radius-sm); background: var(--surface-raised); color: var(--text-muted); font-size: .9rem; }
    .payment-logo img { width: 27px; height: 20px; object-fit: contain; }
    .payment-method__copy { min-width: 0; }
    .reference-label { color: var(--text-secondary); font-family: var(--font-mono, ui-monospace, monospace); font-size: var(--text-sm); }
    .muted-cell { color: var(--text-muted); }
    .action-cell { width: 50px; text-align: right; }
    .amount-editor { display: inline-flex; align-items: center; gap: 4px; white-space: nowrap; }
    .table-editor { width: 94px; min-height: var(--control-height-compact); box-sizing: border-box; padding: 0 var(--space-2); border: 1px solid var(--input-border); border-radius: var(--radius-sm); background: var(--input-bg); color: var(--text-primary); font: inherit; font-size: var(--text-sm); }
    .table-select { width: 126px; }
    .table-editor:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
  `]
})
export class PaymentsTableComponent {
  @Input() payments: Payment[] = [];
  /** U4: server send-validation errors routed to the payments section. */
  @Input() errors: string[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deletePayment = new EventEmitter<number>();
  @Output() updatePayment = new EventEmitter<PaymentUpdate>();

  readonly paymentStatuses = ['not_payment', 'done_payment', 'failed_payment'];

  trackByIndex(index: number): number { return index; }

  paymentAsset(method: string): AssetPath | null {
    return paymentAssetForMethod(method);
  }

  totalAmount(): number {
    return this.payments.reduce((total, payment) => total + (Number(payment.paymentAmount) || 0), 0);
  }

  onEdit(index: number, field: 'paymentAmount' | 'paymentStatus', event: Event) {
    const raw = (event.target as HTMLInputElement | HTMLSelectElement).value;
    const patch: Partial<Payment> = field === 'paymentAmount'
      ? { paymentAmount: Number.isFinite(Number(raw)) ? Number(raw) : 0 }
      : { paymentStatus: raw };
    this.updatePayment.emit({ index, patch });
  }

  metadata(payment: Payment): string {
    const values = [payment.cardName, payment.bankCode, payment.customerName, payment.customerNumber].filter(Boolean);
    return values.length ? values.join(' · ') : '—';
  }
}
