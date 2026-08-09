import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Payment } from '../../../core/models';
import { AssetPath, paymentAssetForMethod } from '../../../core/config/app-assets';
import { EmptyStateComponent, RiyalComponent, UiButtonComponent, UiIconButtonComponent } from '../../../shared/ui';

export interface PaymentUpdate {
  index: number;
  patch: Partial<Payment>;
}

/**
 * A payment is one row entity: logo, method and any reference/metadata form a
 * single identity block, so the frequently-empty reference and metadata values
 * never reserve their own full-height columns. Only status and amount stay as
 * fixed-width editable cells, and the payment total is a summary strip aligned
 * with the amount column instead of a table footer cell.
 */
@Component({
  selector: 'app-payments-table',
  standalone: true,
  imports: [CommonModule, EmptyStateComponent, RiyalComponent, UiButtonComponent, UiIconButtonComponent],
  template: `
    <div id="payments-card" class="py">
      <div class="py__errors" role="alert" *ngIf="errors.length > 0">
        <p *ngFor="let message of errors"><i class="bi bi-exclamation-circle" aria-hidden="true"></i>{{ message }}</p>
      </div>

      <ng-container *ngIf="payments.length > 0; else paymentsEmpty">
        <div class="py__head" aria-hidden="true">
          <span>Method</span>
          <span>Status</span>
          <span>Amount</span>
          <span></span>
        </div>

        <ul class="py__list">
          <li class="pyrow" *ngFor="let payment of payments; let index = index; trackBy: trackByIndex">
            <div class="pyrow__id">
              <span class="pyrow__seq" aria-hidden="true">{{ index + 1 }}</span>
              <span class="pyrow__logo" aria-hidden="true">
                @if (paymentAsset(payment.paymentMethod); as logo) {
                  <img [src]="logo" alt="">
                } @else {
                  <i class="bi bi-credit-card-2-front"></i>
                }
              </span>
              <span class="pyrow__copy">
                <strong class="pyrow__method">{{ payment.paymentMethod }}</strong>
                <span class="pyrow__option" *ngIf="payment.paymentOption">{{ payment.paymentOption }}</span>
                <span class="pyrow__details">
                  <span class="reference-label" [class.is-empty]="!payment.transactionId">{{ payment.transactionId || '—' }}</span>
                  <span class="metadata-label" *ngIf="metadata(payment) !== '—'">{{ metadata(payment) }}</span>
                </span>
              </span>
            </div>

            <div class="cell cell--status">
              <label class="cell__label" [for]="'payment-status-' + index">Status</label>
              <select [id]="'payment-status-' + index" class="editor editor--select" [value]="payment.paymentStatus" (change)="onEdit(index, 'paymentStatus', $event)">
                <option *ngFor="let status of paymentStatuses" [value]="status">{{ status }}</option>
              </select>
            </div>

            <div class="cell cell--amount">
              <label class="cell__label" [for]="'payment-amount-' + index">Amount</label>
              <span class="editor-wrap">
                <app-riyal [decorative]="true" [size]="0.78"></app-riyal>
                <input
                  [id]="'payment-amount-' + index"
                  class="editor"
                  type="number" min="0" step="0.01"
                  [value]="payment.paymentAmount"
                  [attr.aria-label]="'Amount for ' + payment.paymentMethod"
                  (change)="onEdit(index, 'paymentAmount', $event)">
              </span>
            </div>

            <div class="cell cell--act">
              <ui-icon-button icon="bi-trash3" size="sm" variant="danger" ariaLabel="Remove payment" (pressed)="deletePayment.emit(index)"></ui-icon-button>
            </div>
          </li>
        </ul>

        <p class="py__total">
          <span class="py__total-label">Payment total</span>
          <span class="py__total-value"><app-riyal [size]="0.85"></app-riyal>{{ totalAmount() | number:'1.2-2' }}</span>
        </p>
      </ng-container>

      <ng-template #paymentsEmpty>
        <app-empty-state icon="bi-cash-coin" title="Cash on Delivery" description="No payment method is selected, so this order is sent as Cash on Delivery. Add a payment only if it was settled up front." data-testid="payments-cod-state">
          <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddDialog.emit()">Add payment</ui-button>
        </app-empty-state>
      </ng-template>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; container-type: inline-size; container-name: payments; }
    .py { min-width: 0; }
    .py__errors { display: flex; flex-direction: column; gap: 5px; margin-bottom: var(--space-3); padding: 10px 12px; border: 1px solid var(--state-danger-border); border-radius: var(--radius-md); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .py__errors p { display: flex; align-items: flex-start; gap: 7px; margin: 0; font-size: .78rem; line-height: 1.35; }

    /* Rows bleed to the hosting panel's edges so a hovered row and its divider
     * read as one continuous surface instead of a floating inner table. */
    .py__head, .pyrow, .py__total { display: grid; grid-template-columns: minmax(0, 1fr) 148px 132px 34px; column-gap: var(--space-3); align-items: center; margin-inline: calc(var(--panel-padding) * -1); padding-inline: var(--panel-padding); }
    .py__head { padding-bottom: 7px; border-bottom: 1px solid var(--border-strong); color: var(--text-muted); font-size: .67rem; font-weight: 800; letter-spacing: .05em; text-transform: uppercase; }
    .py__head > * { text-align: right; }
    .py__head > :first-child { text-align: left; }

    .py__list { margin: 0; padding: 0; list-style: none; }
    .pyrow { padding-block: 10px; border-bottom: 1px solid var(--divider); transition: background var(--transition-fast); }
    .pyrow:last-child { border-bottom: 0; }
    .pyrow:hover { background: var(--table-row-hover); }

    .pyrow__id { display: flex; align-items: center; gap: var(--space-2); min-width: 0; }
    .pyrow__seq { flex: 0 0 auto; min-width: 13px; color: var(--text-muted); font-size: .72rem; font-variant-numeric: tabular-nums; }
    .pyrow__logo { display: inline-grid; width: 32px; height: 24px; flex: 0 0 32px; place-items: center; border: 1px solid var(--border-subtle); border-radius: var(--radius-sm); background: var(--surface-raised); color: var(--text-muted); font-size: .85rem; }
    .pyrow__logo img { width: 24px; height: 17px; object-fit: contain; }
    .pyrow__copy { display: flex; flex-direction: column; min-width: 0; }
    .pyrow__method { color: var(--text-primary); font-size: .92rem; font-weight: 750; line-height: 1.3; }
    .pyrow__option { color: var(--text-secondary); font-size: .76rem; line-height: 1.35; }
    .pyrow__details { display: flex; align-items: center; gap: var(--space-3); flex-wrap: wrap; margin-top: 2px; }
    .reference-label { color: var(--text-secondary); font-family: var(--font-mono); font-size: .7rem; }
    .reference-label.is-empty { color: var(--text-muted); font-family: var(--font-main); }
    .metadata-label { color: var(--text-muted); font-size: .7rem; line-height: 1.3; }

    .cell { display: flex; flex-direction: column; align-items: stretch; gap: 3px; min-width: 0; }
    .cell--act { align-items: flex-end; }
    .cell__label { position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }

    .editor { width: 100%; min-height: 32px; box-sizing: border-box; padding: 0 8px; border: 1px solid var(--input-border); border-radius: var(--radius-sm); background: var(--input-bg); color: var(--text-primary); font: inherit; font-size: .85rem; text-align: right; font-variant-numeric: tabular-nums; }
    .editor:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .editor--select { text-align: left; }
    .editor-wrap { display: flex; align-items: center; gap: 4px; }
    .editor-wrap app-riyal { flex: 0 0 auto; color: var(--text-muted); }

    .py__total { margin: 0; padding-block: 11px; border-top: 1px solid var(--border-strong); }
    .py__total-label { grid-column: 1 / 3; color: var(--text-muted); font-size: .68rem; font-weight: 800; letter-spacing: .05em; text-align: right; text-transform: uppercase; }
    .py__total-value { display: inline-flex; align-items: center; justify-content: flex-end; gap: 4px; color: var(--text-primary); font-size: 1.02rem; font-weight: 800; font-variant-numeric: tabular-nums; }

    @container payments (max-width: 700px) {
      .py__head { display: none; }
      .pyrow { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 34px; grid-template-areas: "id id id" "status amount act"; row-gap: 10px; padding-block: 12px; align-items: end; }
      .pyrow__id { grid-area: id; }
      .cell--status { grid-area: status; } .cell--amount { grid-area: amount; } .cell--act { grid-area: act; }
      .cell__label { position: static; width: auto; height: auto; margin: 0; overflow: visible; clip-path: none; color: var(--text-muted); font-size: .65rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
      .py__total { grid-template-columns: minmax(0, 1fr) auto; }
      .py__total-label { grid-column: 1; text-align: left; }
    }
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
