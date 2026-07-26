import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderRequestsStore } from '../order-requests.store';
import { OrderRequestDetail, OrderRequestCancelResponse, SendOrderResult, ApiError } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import { ApiService } from '../../../core/services/api.service';
import {
  DrawerComponent, StatusPillComponent, JsonTreeComponent, RiyalComponent,
  CopyButtonComponent, EmptyStateComponent, SkeletonComponent
} from '../../../shared/ui';
import { CancelRequestDialogComponent, CancelDialogResult, CancelErrorState } from './cancel-request-dialog.component';
import { ResendRequestDialogComponent } from './resend-request-dialog.component';

type Tab = 'overview' | 'request' | 'response' | 'items' | 'payments' | 'invoice';

function materialNumberViews(m: string | null): { full: string; short: string } {
  if (!m) return { full: '', short: '' };
  const digits = m.replace(/\D/g, '');
  const full = digits.padStart(18, '0');
  const short = digits.replace(/^0+/, '').slice(-6).padStart(6, '0');
  return { full, short };
}

/**
 * Route-driven detail drawer (:requestId, see app.routes.ts) -- deep
 * linkable and bookmarkable per R9 step 6. Shares OrderRequestsStore with
 * the parent OrderRequestsComponent via normal Angular DI inheritance (this
 * component is rendered through the parent's <router-outlet>, so it never
 * declares its own `providers`).
 */
@Component({
  selector: 'app-order-request-drawer',
  standalone: true,
  imports: [
    CommonModule, DrawerComponent, StatusPillComponent, JsonTreeComponent, RiyalComponent,
    CopyButtonComponent, EmptyStateComponent, SkeletonComponent, CancelRequestDialogComponent, ResendRequestDialogComponent
  ],
  template: `
    <app-drawer [title]="store.selected()?.request?.orderNumber || 'Loading...'" (close)="onClose()">
      @if (store.detailStatus() === 'loading') {
        <div class="detail-skeleton">
          <app-skeleton height="24px" width="40%"></app-skeleton>
          <app-skeleton height="80px" width="100%"></app-skeleton>
          <app-skeleton height="200px" width="100%"></app-skeleton>
        </div>
      } @else if (store.detailStatus() === 'error') {
        <app-empty-state icon="bi-exclamation-octagon" title="This request no longer exists" description="It may have been removed, or the id in the URL is wrong.">
          <button type="button" class="action-btn" (click)="onClose()">Back to list</button>
        </app-empty-state>
      } @else if (store.selected(); as detail) {
        <div class="drawer-toolbar">
          <div class="toolbar-meta">
            <app-status-pill *ngIf="detail.request.header?.orderStatus as s" [status]="s"></app-status-pill>
            <span class="toolbar-total">{{ detail.request.netTotal | number:'1.2-2' }} <app-riyal [size]="0.9"></app-riyal></span>
            <app-copy-button [value]="detail.request.orderNumber" label="Copy order #"></app-copy-button>
          </div>
          <div class="toolbar-actions">
            <button
              type="button"
              class="action-btn brand"
              [disabled]="!detail.request.header?.canResend"
              [title]="detail.request.header?.canResend ? '' : 'Blocked: order status ' + detail.request.header?.orderStatusLabel"
              (click)="showResend.set(true)">
              <i class="bi bi-arrow-repeat"></i> Resend
            </button>
            <button
              type="button"
              class="action-btn danger"
              [disabled]="!detail.request.header?.canCancel"
              [title]="detail.request.header?.canCancel ? '' : 'Blocked: order status ' + detail.request.header?.orderStatusLabel"
              (click)="showCancel.set(true)">
              <i class="bi bi-x-circle"></i> Cancel order
            </button>
          </div>
        </div>

        <nav class="tab-nav">
          @for (t of tabs; track t.id) {
            <button type="button" class="tab-btn" [class.active]="activeTab() === t.id" (click)="activeTab.set(t.id)">{{ t.label }}</button>
          }
        </nav>

        <div class="tab-panel">
          @switch (activeTab()) {
            @case ('overview') { <ng-container *ngTemplateOutlet="overviewTpl; context: { detail }"></ng-container> }
            @case ('request') { <ng-container *ngTemplateOutlet="requestTpl; context: { detail }"></ng-container> }
            @case ('response') { <ng-container *ngTemplateOutlet="responseTpl; context: { detail }"></ng-container> }
            @case ('items') { <ng-container *ngTemplateOutlet="itemsTpl; context: { detail }"></ng-container> }
            @case ('payments') { <ng-container *ngTemplateOutlet="paymentsTpl; context: { detail }"></ng-container> }
            @case ('invoice') { <ng-container *ngTemplateOutlet="invoiceTpl; context: { detail }"></ng-container> }
          }
        </div>
      }
    </app-drawer>

    <!-- ===== Overview ===== -->
    <ng-template #overviewTpl let-detail="detail">
      @if (detail.request.header; as h) {
        <div class="amber-callout" *ngIf="h.rejectionMessage">
          <i class="bi bi-exclamation-triangle-fill"></i>
          <div><strong>Rejection message:</strong> {{ h.rejectionMessage }}</div>
        </div>

        <div class="field-grid">
          <div class="field"><span class="field-label">Branch</span><span>{{ h.branchCode }} <span class="muted" *ngIf="h.branchName">({{ h.branchName }})</span></span></div>
          <div class="field"><span class="field-label">Order date</span><span>{{ h.orderDate | date:'medium' }}</span></div>
          <div class="field"><span class="field-label">Consumer mobile</span><span>{{ h.consumerMobile || '—' }}</span></div>
          <div class="field"><span class="field-label">Address</span><span>{{ h.address || '—' }}</span></div>
          <div class="field"><span class="field-label">Payment method</span><span>{{ h.orderPaymentMethod || '—' }}</span></div>
          <div class="field"><span class="field-label">Note</span><span>{{ h.orderNote || '—' }}</span></div>
          <div class="field"><span class="field-label">Parent order</span><span>{{ h.parentOrderNumber || '—' }}</span></div>
        </div>

        <div class="totals-strip">
          <div><span class="field-label">Gross</span><strong>{{ h.grossTotal | number:'1.2-2' }}</strong></div>
          <div><span class="field-label">Discount</span><strong>{{ h.totalDiscount | number:'1.2-2' }}</strong></div>
          <div><span class="field-label">VAT</span><strong>{{ h.totalVat | number:'1.2-2' }}</strong></div>
          <div class="net"><span class="field-label">Net</span><strong>{{ h.netTotal | number:'1.2-2' }}</strong></div>
        </div>

        <div class="consistency-check" [class.mismatch]="!totalsConsistent(detail)">
          <i class="bi" [class.bi-check-circle-fill]="totalsConsistent(detail)" [class.bi-exclamation-triangle-fill]="!totalsConsistent(detail)"></i>
          <span>
            OrderRequests: {{ detail.request.netTotal | number:'1.2-2' }} &middot;
            Header: {{ h.netTotal | number:'1.2-2' }} &middot;
            Invoice: {{ detail.request.invoice?.netAmount != null ? (detail.request.invoice.netAmount | number:'1.2-2') : 'n/a' }}
          </span>
        </div>
      } @else {
        <app-empty-state icon="bi-file-earmark-x" title="No header recorded" description="This request never produced a RequestOrderHeaders row -- check the Response tab for what the upstream API actually returned.">
          <button type="button" class="action-btn" (click)="activeTab.set('response')">Go to Response tab</button>
        </app-empty-state>
      }
    </ng-template>

    <!-- ===== Request ===== -->
    <ng-template #requestTpl let-detail="detail">
      <app-json-tree title="RequestJson" [data]="detail.request.requestJson"></app-json-tree>
    </ng-template>

    <!-- ===== Response ===== -->
    <ng-template #responseTpl let-detail="detail">
      <div class="outcome-banner" [class.success]="detail.request.isSucceeded === true" [class.danger]="detail.request.isSucceeded === false">
        <i class="bi" [class.bi-check-circle-fill]="detail.request.isSucceeded === true" [class.bi-x-circle-fill]="detail.request.isSucceeded === false"></i>
        {{ detail.request.isSucceeded === true ? 'Succeeded' : detail.request.isSucceeded === false ? 'Failed' : 'Outcome unknown' }}
      </div>

      <div class="exception-card" *ngIf="detail.request.exceptionMessage">
        <div class="exception-header">
          <strong>Exception</strong>
          <app-copy-button [value]="detail.request.exceptionMessage" label="Copy"></app-copy-button>
        </div>
        <pre class="exception-text">{{ detail.request.exceptionMessage }}</pre>
      </div>

      <app-json-tree *ngIf="detail.request.responseJson" title="ResponseJson" [data]="detail.request.responseJson"></app-json-tree>

      <app-empty-state
        *ngIf="!detail.request.responseJson && !detail.request.exceptionMessage"
        icon="bi-inbox"
        title="No response recorded for this attempt"
        description="Neither ResponseJson nor ExceptionMessage were populated for this OrderRequests row.">
      </app-empty-state>
    </ng-template>

    <!-- ===== Line items ===== -->
    <ng-template #itemsTpl let-detail="detail">
      <div class="items-table" *ngIf="detail.request.details.length > 0; else noItems">
        <div class="items-head">
          <span>Item</span><span>Material #</span><span class="align-right">Qty</span>
          <span class="align-right">Unit price</span><span class="align-right">Discount</span>
          <span class="align-right">VAT</span><span class="align-right">Total</span>
        </div>
        @for (line of detail.request.details; track $index) {
          <div class="items-row">
            <span>{{ line.itemName || '—' }}<div class="muted small" *ngIf="line.offerMessage">{{ line.offerCode }}: {{ line.offerMessage }}</div></span>
            <span class="mono">
              <span [title]="'18-digit: ' + materialViews(line.materialNumber).full">{{ materialViews(line.materialNumber).short }}</span>
            </span>
            <span class="align-right">{{ line.quantity }}</span>
            <span class="align-right">{{ line.unitPrice | number:'1.2-2' }}</span>
            <span class="align-right">{{ line.totalDiscount | number:'1.2-2' }}</span>
            <span class="align-right">{{ line.itemVat | number:'1.2-2' }} ({{ line.itemVatPercentage }}%)</span>
            <span class="align-right">{{ line.totalPrice | number:'1.2-2' }}</span>
          </div>
        }
        <div class="items-footer" [class.mismatch]="!lineItemsConsistent(detail)">
          <span>Column sums</span><span></span><span></span><span></span>
          <span class="align-right">{{ sumDiscount(detail) | number:'1.2-2' }}</span>
          <span class="align-right">{{ sumVat(detail) | number:'1.2-2' }}</span>
          <span class="align-right">{{ sumTotal(detail) | number:'1.2-2' }}</span>
        </div>
      </div>
      <ng-template #noItems><app-empty-state icon="bi-box" title="No line items recorded"></app-empty-state></ng-template>
    </ng-template>

    <!-- ===== Payments ===== -->
    <ng-template #paymentsTpl let-detail="detail">
      <div class="payment-cards" *ngIf="detail.request.transactions.length > 0; else noPayments">
        @for (txn of detail.request.transactions; track $index) {
          <div class="payment-card">
            <span class="payment-method-pill">{{ txn.eCommercePaymentMethod || 'Unknown' }}</span>
            <div class="payment-meta">
              <span>{{ txn.eCommercePaymentOption || '—' }}</span>
              <span class="mono" *ngIf="txn.transactionCode">{{ txn.transactionCode }} <app-copy-button [value]="txn.transactionCode"></app-copy-button></span>
              <span *ngIf="txn.bankCode">Bank: {{ txn.bankCode }}</span>
              <span *ngIf="txn.cardName">Card: {{ txn.cardName }}</span>
            </div>
            <span class="payment-amount">{{ txn.paymentAmount | number:'1.2-2' }}</span>
          </div>
        }
        <div class="payment-match" [class.mismatch]="!paymentsConsistent(detail)">
          <i class="bi" [class.bi-check-circle-fill]="paymentsConsistent(detail)" [class.bi-exclamation-triangle-fill]="!paymentsConsistent(detail)"></i>
          Payments total {{ sumPayments(detail) | number:'1.2-2' }} vs header net {{ detail.request.header?.netTotal | number:'1.2-2' }}
        </div>
      </div>
      <ng-template #noPayments><app-empty-state icon="bi-credit-card" title="No payment transactions recorded"></app-empty-state></ng-template>
    </ng-template>

    <!-- ===== Invoice & lineage ===== -->
    <ng-template #invoiceTpl let-detail="detail">
      <div class="invoice-card" *ngIf="detail.request.invoice as inv; else noInvoice">
        <div class="field"><span class="field-label">Barcode</span><span class="mono">{{ inv.barcode }} <app-copy-button *ngIf="inv.barcode" [value]="inv.barcode"></app-copy-button></span></div>
        <div class="field"><span class="field-label">Close date</span><span>{{ inv.closeDateLocalTime | date:'medium' }}</span></div>
        <div class="field"><span class="field-label">Net amount</span><span>{{ inv.netAmount | number:'1.2-2' }}</span></div>
        <div class="field"><span class="field-label">Paid amount</span><span>{{ inv.paidAmount | number:'1.2-2' }}</span></div>
      </div>
      <ng-template #noInvoice><app-empty-state icon="bi-receipt" title="Not invoiced yet"></app-empty-state></ng-template>

      <h4 class="section-title">Attempts timeline</h4>
      <div class="attempts-timeline">
        @for (a of detail.attempts; track a.id) {
          <button type="button" class="attempt-row" [class.current]="a.id === detail.request.id" (click)="viewAttempt(a.id)">
            <span class="outcome-dot" [class.dot-success]="a.isSucceeded === true" [class.dot-danger]="a.isSucceeded === false"></span>
            <span>{{ a.orderDate | date:'short' }}</span>
            <span class="muted" *ngIf="a.hasException">has exception</span>
            <span class="muted" *ngIf="a.id === detail.request.id">(this attempt)</span>
          </button>
        }
      </div>

      <h4 class="section-title">Order lineage</h4>
      <div class="lineage-trail">
        <button type="button" class="lineage-node" *ngIf="detail.lineage.parent as p" (click)="viewByOrderNumber(p.orderNumber)">
          {{ p.orderNumber }} <span class="muted">(parent)</span>
        </button>
        <span *ngIf="detail.lineage.parent">→</span>
        <span class="lineage-node current">{{ detail.request.orderNumber }}</span>
        @if (detail.lineage.children.length > 0) {
          <span>→</span>
          @for (c of detail.lineage.children; track c.orderNumber) {
            <button type="button" class="lineage-node" (click)="viewByOrderNumber(c.orderNumber)">{{ c.orderNumber }}</button>
          }
        }
        <span class="muted" *ngIf="!detail.lineage.parent && detail.lineage.children.length === 0">No related orders.</span>
      </div>
    </ng-template>

    <app-cancel-request-dialog
      *ngIf="showCancel()"
      [orderNumber]="store.selected()?.request?.orderNumber || ''"
      [netTotal]="store.selected()?.request?.netTotal || 0"
      [canCancel]="store.selected()?.request?.header?.canCancel ?? false"
      [blockedReason]="'order status ' + (store.selected()?.request?.header?.orderStatusLabel || 'unknown')"
      [submitting]="cancelSubmitting()"
      [errorState]="cancelError()"
      (close)="showCancel.set(false); cancelError.set(null)"
      (confirm)="onCancelConfirm($event)">
    </app-cancel-request-dialog>

    <app-resend-request-dialog
      *ngIf="showResend()"
      [orderNumber]="store.selected()?.request?.orderNumber || ''"
      [currentBranchCode]="store.selected()?.request?.header?.branchCode || null"
      [canResend]="store.selected()?.request?.header?.canResend ?? false"
      [blockedReason]="'order status ' + (store.selected()?.request?.header?.orderStatusLabel || 'unknown')"
      [branches]="store.branches()"
      [submitting]="resendSubmitting()"
      [errorMessage]="resendError()"
      (close)="showResend.set(false); resendError.set(null)"
      (confirm)="onResendConfirm($event)">
    </app-resend-request-dialog>
  `,
  styles: [`
    .detail-skeleton { display: flex; flex-direction: column; gap: 16px; }
    .drawer-toolbar { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 12px; margin-bottom: 16px; }
    .toolbar-meta { display: flex; align-items: center; gap: 12px; }
    .toolbar-total { font-weight: 700; font-size: 1.1rem; display: inline-flex; align-items: center; gap: 4px; }
    .toolbar-actions { display: flex; gap: 8px; }
    .action-btn {
      display: flex; align-items: center; gap: 6px;
      border: 1px solid var(--glass-border); background: var(--bg-tertiary); color: var(--text-primary);
      border-radius: var(--radius-md); padding: 7px 14px; font-size: 0.82rem; cursor: pointer; font-weight: 600;
    }
    .action-btn.brand { background: var(--grad-brand); color: var(--on-gradient); border: none; }
    .action-btn.danger { background: var(--grad-danger); color: var(--on-gradient); border: none; }
    .action-btn:disabled { opacity: 0.4; cursor: not-allowed; }
    .tab-nav { display: flex; gap: 4px; border-bottom: 1px solid var(--glass-border); margin-bottom: 20px; overflow-x: auto; }
    .tab-btn {
      background: none; border: none; color: var(--text-secondary); padding: 10px 14px; font-size: 0.85rem;
      cursor: pointer; position: relative; white-space: nowrap;
    }
    .tab-btn.active { color: var(--text-primary); font-weight: 700; }
    .tab-btn.active::after {
      content: ''; position: absolute; left: 8px; right: 8px; bottom: -1px; height: 2px;
      background: var(--grad-brand); border-radius: var(--radius-pill);
      animation: tabGrow var(--d) var(--ease-out);
    }
    @keyframes tabGrow { from { transform: scaleX(0); } to { transform: scaleX(1); } }
    .amber-callout { display: flex; gap: 10px; background: var(--warning-bg); color: var(--warning); padding: 12px 16px; border-radius: var(--radius-md); margin-bottom: 18px; font-size: 0.85rem; }
    .field-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 14px; margin-bottom: 20px; }
    .field { display: flex; flex-direction: column; gap: 2px; font-size: 0.88rem; }
    .field-label { font-size: 0.72rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted); letter-spacing: .02em; }
    .muted { color: var(--text-muted); font-size: 0.82rem; }
    .small { font-size: 0.72rem; }
    .totals-strip { display: flex; gap: 20px; padding: 14px 18px; background: var(--bg-tertiary); border-radius: var(--radius-md); margin-bottom: 16px; }
    .totals-strip .net strong { color: var(--brand-500); font-size: 1.1rem; }
    .consistency-check { display: flex; align-items: center; gap: 8px; font-size: 0.8rem; color: var(--success); }
    .consistency-check.mismatch { color: var(--warning); }
    .outcome-banner { display: flex; align-items: center; gap: 8px; padding: 12px 16px; border-radius: var(--radius-md); font-weight: 700; margin-bottom: 16px; background: var(--bg-tertiary); }
    .outcome-banner.success { background: var(--success-bg); color: var(--success); }
    .outcome-banner.danger { background: var(--danger-bg); color: var(--danger); }
    .exception-card { background: var(--danger-bg); border-radius: var(--radius-md); padding: 14px 16px; margin-bottom: 16px; }
    .exception-header { display: flex; justify-content: space-between; align-items: center; color: var(--danger); margin-bottom: 8px; }
    .exception-text { margin: 0; white-space: pre-wrap; word-break: break-word; font-family: 'JetBrains Mono', monospace; font-size: 0.8rem; color: var(--danger); }
    .items-table, .items-head, .items-row, .items-footer { display: grid; grid-template-columns: 2fr 1fr 0.6fr 0.9fr 0.9fr 1fr 0.9fr; gap: 10px; align-items: center; }
    .items-head { font-size: 0.72rem; font-weight: 700; color: var(--text-muted); text-transform: uppercase; padding: 8px 0; border-bottom: 1px solid var(--glass-border); }
    .items-row { padding: 10px 0; border-bottom: 1px solid var(--glass-border); font-size: 0.85rem; }
    .items-footer { padding: 10px 0; font-weight: 700; color: var(--success); }
    .items-footer.mismatch { color: var(--warning); }
    .align-right { text-align: right; }
    .mono { font-family: 'JetBrains Mono', monospace; font-size: 0.8rem; }
    .payment-cards { display: flex; flex-direction: column; gap: 10px; margin-bottom: 16px; }
    .payment-card { display: flex; align-items: center; gap: 16px; padding: 12px 16px; background: var(--bg-tertiary); border-radius: var(--radius-md); }
    .payment-method-pill { background: var(--grad-brand); color: var(--on-gradient); padding: 4px 12px; border-radius: var(--radius-pill); font-size: 0.75rem; font-weight: 700; }
    .payment-meta { flex: 1; display: flex; flex-direction: column; gap: 2px; font-size: 0.8rem; color: var(--text-secondary); }
    .payment-amount { font-weight: 700; }
    .payment-match { display: flex; align-items: center; gap: 8px; font-size: 0.8rem; color: var(--success); }
    .payment-match.mismatch { color: var(--warning); }
    .invoice-card { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 14px; background: var(--bg-tertiary); border-radius: var(--radius-md); padding: 16px; margin-bottom: 24px; }
    .section-title { font-size: 0.9rem; color: var(--text-primary); margin: 20px 0 12px; }
    .attempts-timeline { display: flex; flex-direction: column; gap: 4px; }
    .attempt-row { display: flex; align-items: center; gap: 10px; background: none; border: 1px solid transparent; border-radius: var(--radius-sm); padding: 8px 10px; cursor: pointer; color: var(--text-primary); font-size: 0.82rem; text-align: left; }
    .attempt-row:hover { background: var(--glass-hover-bg); }
    .attempt-row.current { border-color: var(--brand-500); }
    .outcome-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--text-muted); }
    .dot-success { background: var(--success); }
    .dot-danger { background: var(--danger); }
    .lineage-trail { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .lineage-node { background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-pill); padding: 6px 14px; font-size: 0.82rem; cursor: pointer; color: var(--text-primary); }
    .lineage-node.current { background: var(--grad-brand); color: var(--on-gradient); border: none; font-weight: 700; }
  `]
})
export class OrderRequestDrawerComponent implements OnInit, OnDestroy {
  store = inject(OrderRequestsStore);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toast = inject(ToastService);
  private api = inject(ApiService);

  tabs: { id: Tab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'request', label: 'Request' },
    { id: 'response', label: 'Response' },
    { id: 'items', label: 'Line items' },
    { id: 'payments', label: 'Payments' },
    { id: 'invoice', label: 'Invoice & lineage' }
  ];
  activeTab = signal<Tab>('overview');

  showCancel = signal(false);
  cancelSubmitting = signal(false);
  cancelError = signal<CancelErrorState | null>(null);

  showResend = signal(false);
  resendSubmitting = signal(false);
  resendError = signal<string | null>(null);

  private paramSub = this.route.paramMap.subscribe(params => {
    const id = Number(params.get('requestId'));
    if (Number.isFinite(id) && id > 0) {
      this.activeTab.set('overview');
      this.store.openDetail(id);
    }
  });

  ngOnInit() {}

  ngOnDestroy() {
    this.paramSub.unsubscribe();
  }

  onClose() {
    this.store.closeDetail();
    this.router.navigate(['..'], { relativeTo: this.route });
  }

  viewAttempt(id: number) {
    this.router.navigate(['..', id], { relativeTo: this.route });
  }

  viewByOrderNumber(orderNumber: string) {
    const key = this.store.moduleKey();
    this.api.get<{ request: OrderRequestDetail }>(`modules/${key}/order-requests/by-order/${orderNumber}`, { envKey: this.store.envKey() || undefined }).subscribe({
      next: res => this.router.navigate(['..', res.request.id], { relativeTo: this.route }),
      error: () => {}
    });
  }

  materialViews(m: string | null) { return materialNumberViews(m); }

  totalsConsistent(detail: { request: OrderRequestDetail }): boolean {
    const h = detail.request.header;
    if (!h) return true;
    const invoiceNet = detail.request.invoice?.netAmount;
    const close = (a: number, b: number) => Math.abs(a - b) < 0.05;
    return close(detail.request.netTotal, h.netTotal) && (invoiceNet == null || close(h.netTotal, invoiceNet));
  }

  sumDiscount(detail: { request: OrderRequestDetail }): number {
    return detail.request.details.reduce((s: number, l) => s + (l.totalDiscount || 0), 0);
  }
  sumVat(detail: { request: OrderRequestDetail }): number {
    return detail.request.details.reduce((s: number, l) => s + (l.itemVat || 0), 0);
  }
  sumTotal(detail: { request: OrderRequestDetail }): number {
    return detail.request.details.reduce((s: number, l) => s + (l.totalPrice || 0), 0);
  }
  lineItemsConsistent(detail: { request: OrderRequestDetail }): boolean {
    const h = detail.request.header;
    if (!h) return true;
    return Math.abs(this.sumTotal(detail) - h.netTotal) < 0.5;
  }

  sumPayments(detail: { request: OrderRequestDetail }): number {
    return detail.request.transactions.reduce((s: number, t) => s + (t.paymentAmount || 0), 0);
  }
  paymentsConsistent(detail: { request: OrderRequestDetail }): boolean {
    const h = detail.request.header;
    if (!h || detail.request.transactions.length === 0) return true;
    return Math.abs(this.sumPayments(detail) - h.netTotal) < 0.5;
  }

  onCancelConfirm(result: CancelDialogResult) {
    const detail = this.store.selected();
    if (!detail) return;
    const key = this.store.moduleKey();

    this.cancelSubmitting.set(true);
    this.cancelError.set(null);

    this.api.post<OrderRequestCancelResponse>(
      `modules/${key}/order-requests/${detail.request.id}/cancel`,
      { reason: result.reason, customUrl: result.customUrl }
    ).subscribe({
      next: res => {
        this.cancelSubmitting.set(false);
        if (res.success) {
          this.showCancel.set(false);
          this.store.refreshDetailInPlace({ ...detail, request: res.request });
          this.toast.showSuccess(`Order ${detail.request.orderNumber} cancelled successfully.`);
        } else {
          // Upstream responded but not 2xx -- keep the dialog open, show the raw body.
          this.cancelError.set({ kind: 'upstream', message: `Upstream returned status ${res.statusCode}.`, rawBody: res.responseText });
        }
      },
      error: (err: ApiError) => {
        this.cancelSubmitting.set(false);
        if (err?.status === 409) {
          this.cancelError.set({ kind: 'blocked', message: err.message || 'This order can no longer be cancelled.' });
        } else {
          this.cancelError.set({ kind: 'network', message: err?.message || 'The cancel request could not be completed.' });
        }
      }
    });
  }

  onResendConfirm(branchCode: string) {
    const detail = this.store.selected();
    if (!detail) return;
    const key = this.store.moduleKey();

    this.resendSubmitting.set(true);
    this.resendError.set(null);

    this.api.post<SendOrderResult>(`modules/${key}/order-requests/${detail.request.id}/resend`, { branchCode }).subscribe({
      next: res => {
        this.resendSubmitting.set(false);
        this.showResend.set(false);
        if (res.success) {
          this.toast.showSuccess(`Order ${detail.request.orderNumber} resent to branch ${branchCode}.`);
        } else {
          this.toast.showError(`Resend failed. Status: ${res.statusCode}`);
        }
        this.store.refresh();
      },
      error: (err: ApiError) => {
        this.resendSubmitting.set(false);
        this.resendError.set(err?.message || 'The resend request could not be completed.');
      }
    });
  }
}
