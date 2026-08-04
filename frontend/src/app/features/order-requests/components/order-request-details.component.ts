import { Component, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { OrderRequestsStore } from '../order-requests.store';
import { OrderRequestDetail, OrderRequestCancelResponse, SendOrderResult, ApiError } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import { ApiService } from '../../../core/services/api.service';
import { ModuleService } from '../../../core/services/module.service';
import {
  StatusPillComponent, JsonTreeComponent, RiyalComponent, UiSectionComponent,
  CopyButtonComponent, EmptyStateComponent, SkeletonComponent, ConfirmDialogComponent, UiTableComponent
} from '../../../shared/ui';
import { CancelRequestDialogComponent, CancelDialogResult, CancelErrorState } from './cancel-request-dialog.component';
import { ResendRequestDialogComponent } from './resend-request-dialog.component';
import { canResend as canResendStatus, resendBlockedReason } from '../resend-eligibility';

function materialNumberViews(materialNumber: string | null): { full: string; short: string } {
  if (!materialNumber) return { full: '', short: '' };
  const digits = materialNumber.replace(/\D/g, '');
  return {
    full: digits.padStart(18, '0'),
    short: digits.replace(/^0+/, '').slice(-6).padStart(6, '0')
  };
}

/** Route-level Order Requests detail page. It shares the parent route's
 * OrderRequestsStore so list filters remain intact when the operator returns.
 * The former six-tab drawer data is mapped into the five required sections. */
@Component({
  selector: 'app-order-request-details',
  standalone: true,
  imports: [
    CommonModule, StatusPillComponent, JsonTreeComponent, RiyalComponent, UiSectionComponent,
    CopyButtonComponent, EmptyStateComponent, SkeletonComponent, CancelRequestDialogComponent,
    ResendRequestDialogComponent, ConfirmDialogComponent, UiTableComponent
  ],
  template: `
    <main class="order-details-page">
      @if (invalidId()) {
        <section class="state-panel" aria-live="polite">
          <i class="bi bi-exclamation-octagon" aria-hidden="true"></i>
          <h1>Invalid order request link</h1>
          <p>The request identifier in this URL is not valid.</p>
          <button type="button" class="action-btn" (click)="onClose()">Back to Order Requests</button>
        </section>
      } @else if (store.detailStatus() === 'loading') {
        <div class="detail-skeleton" aria-live="polite" aria-label="Loading order request">
          <app-skeleton height="32px" width="45%"></app-skeleton>
          <app-skeleton height="86px" width="100%"></app-skeleton>
          <app-skeleton height="260px" width="100%"></app-skeleton>
        </div>
      } @else if (store.detailStatus() === 'error') {
        <section class="state-panel" aria-live="assertive">
          <i class="bi bi-exclamation-octagon" aria-hidden="true"></i>
          <h1>Order request unavailable</h1>
          <p>{{ store.detailError() || 'The request could not be loaded.' }}</p>
          <div class="state-actions">
            <button type="button" class="action-btn" (click)="retry()">Retry</button>
            <button type="button" class="action-btn secondary" (click)="onClose()">Back to Order Requests</button>
          </div>
        </section>
      } @else if (store.selected(); as detail) {
        <header class="detail-header">
          <div class="detail-heading">
            <button type="button" class="action-btn secondary" (click)="onClose()">
              <i class="bi bi-arrow-left" aria-hidden="true"></i> Back to Order Requests
            </button>
            <div class="heading-row">
              <div>
                <p class="eyebrow">Order request</p>
                <h1 class="order-number mono">{{ detail.request.orderNumber }}</h1>
              </div>
              <app-status-pill *ngIf="detail.request.header?.orderStatus as status" [status]="status" [label]="detail.request.header?.orderStatusLabel || undefined"></app-status-pill>
            </div>
          </div>
          <div class="header-actions">
            <span class="header-total" aria-label="Order net total">
              {{ detail.request.netTotal | number:'1.2-2' }} <app-riyal [size]="0.9"></app-riyal>
            </span>
            <button
              type="button"
              class="action-btn brand"
              [disabled]="!canResend(detail.request) || resendSubmitting()"
              [title]="resendActionTitle(detail.request)"
              (click)="openResend(detail.request)">
              <i class="bi bi-arrow-repeat" aria-hidden="true"></i>
              {{ resendSubmitting() ? 'Resending...' : 'Resend' }}
            </button>
            <button
              type="button"
              class="action-btn danger"
              [disabled]="!detail.request.header?.canCancel || cancelSubmitting()"
              [title]="detail.request.header?.canCancel ? '' : 'Blocked: order status ' + (detail.request.header?.orderStatusLabel || 'unknown')"
              (click)="showCancel.set(true)">
              <i class="bi bi-x-circle" aria-hidden="true"></i> Cancel order
            </button>
          </div>
        </header>

        <div class="detail-context" aria-label="Order request context">
          <span><strong>Module</strong> {{ moduleService.activeModule()?.label || store.moduleKey() }}</span>
          <span><strong>Environment</strong> {{ store.envKey() || 'Default environment' }}</span>
          <span><strong>Created</strong> {{ detail.request.orderDate | date:'medium' }}</span>
          <span *ngIf="detail.request.header && !canResend(detail.request)" class="blocked-hint" role="note">
            {{ resendActionTitle(detail.request) }}
          </span>
        </div>

        <div class="detail-sections">
          <ui-section title="Items" [collapsible]="false">
            @if (detail.request.details.length > 0) {
              <ui-table class="detail-table" [dense]="true" [wide]="true" caption="Order items and Riyal amounts" [captionHidden]="true">
                  <thead>
                    <tr>
                      <th scope="col">Item</th>
                      <th scope="col">Material #</th>
                      <th scope="col" class="align-right">Qty</th>
                      <th scope="col" class="align-right">Unit price <span class="sr-only">Saudi Riyal</span></th>
                      <th scope="col" class="align-right">Discount <span class="sr-only">Saudi Riyal</span></th>
                      <th scope="col" class="align-right">VAT <span class="sr-only">Saudi Riyal</span></th>
                      <th scope="col" class="align-right">Total <span class="sr-only">Saudi Riyal</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (line of detail.request.details; track $index) {
                      <tr>
                        <td>
                          {{ line.itemName || '—' }}
                          <span class="muted small" *ngIf="line.offerMessage">{{ line.offerCode }}: {{ line.offerMessage }}</span>
                        </td>
                        <td class="mono" [title]="'18-digit: ' + materialViews(line.materialNumber).full">{{ materialViews(line.materialNumber).short || '—' }}</td>
                        <td class="align-right">{{ line.quantity }}</td>
                        <td class="align-right amount">{{ line.unitPrice | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal></td>
                        <td class="align-right amount">{{ line.totalDiscount | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal></td>
                        <td class="align-right amount">{{ line.itemVat | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal> <span class="muted">({{ line.itemVatPercentage }}%)</span></td>
                        <td class="align-right amount">{{ line.totalPrice | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal></td>
                      </tr>
                    }
                  </tbody>
                  <tfoot class="detail-table-footer">
                    <tr>
                      <th scope="row" colspan="4">Column sums</th>
                      <td class="align-right amount">{{ sumDiscount(detail) | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal></td>
                      <td class="align-right amount">{{ sumVat(detail) | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal></td>
                      <td class="align-right amount">{{ sumTotal(detail) | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.8"></app-riyal></td>
                    </tr>
                  </tfoot>
              </ui-table>
            } @else {
              <app-empty-state icon="bi-box" title="No line items recorded"></app-empty-state>
            }
          </ui-section>

          <ui-section title="Order Info" [collapsible]="false">
            <dl class="field-grid">
              <div class="field"><dt>Request id</dt><dd class="mono">{{ detail.request.id }}</dd></div>
              <div class="field"><dt>Order/request number</dt><dd class="mono">{{ detail.request.orderNumber }}</dd></div>
              <div class="field"><dt>Module</dt><dd>{{ moduleService.activeModule()?.label || store.moduleKey() }}</dd></div>
              <div class="field"><dt>Environment</dt><dd>{{ store.envKey() || 'Default environment' }}</dd></div>
              <div class="field"><dt>Created</dt><dd>{{ detail.request.orderDate | date:'medium' }}</dd></div>
              @if (detail.request.header; as h) {
                <div class="field"><dt>Status</dt><dd>{{ h.orderStatusLabel }}</dd></div>
                <div class="field"><dt>Branch</dt><dd>{{ h.branchCode || '—' }} <span class="muted" *ngIf="h.branchName">({{ h.branchName }})</span></dd></div>
                <div class="field"><dt>Customer mobile</dt><dd>{{ h.consumerMobile || '—' }}</dd></div>
                <div class="field"><dt>Delivery address</dt><dd>{{ h.address || '—' }}</dd></div>
                <div class="field"><dt>Payment method</dt><dd>{{ h.orderPaymentMethod || '—' }}</dd></div>
                <div class="field"><dt>Order date</dt><dd>{{ h.orderDate | date:'medium' }}</dd></div>
                <div class="field"><dt>Order note</dt><dd>{{ h.orderNote || '—' }}</dd></div>
                <div class="field"><dt>Parent order</dt><dd>{{ h.parentOrderNumber || '—' }}</dd></div>
              }
            </dl>

            @if (detail.request.header; as h) {
              <div class="amber-callout" *ngIf="h.rejectionMessage" role="note">
                <i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>
                <div><strong>Rejection message:</strong> {{ h.rejectionMessage }}</div>
              </div>

              <div class="totals-grid" aria-label="Order totals">
                <div><span>Gross</span><strong>{{ h.grossTotal | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></strong></div>
                <div><span>Discount</span><strong>{{ h.totalDiscount | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></strong></div>
                <div><span>VAT</span><strong>{{ h.totalVat | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></strong></div>
                <div class="net"><span>Net</span><strong>{{ h.netTotal | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></strong></div>
              </div>

              <div class="consistency-check" [class.mismatch]="!totalsConsistent(detail)">
                <i class="bi" [class.bi-check-circle-fill]="totalsConsistent(detail)" [class.bi-exclamation-triangle-fill]="!totalsConsistent(detail)" aria-hidden="true"></i>
                <span>
                  OrderRequests: {{ detail.request.netTotal | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.75"></app-riyal>
                  · Header: {{ h.netTotal | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.75"></app-riyal>
                  · Invoice: {{ detail.request.invoice?.netAmount != null ? (detail.request.invoice!.netAmount | number:'1.2-2') : 'n/a' }}<span *ngIf="detail.request.invoice?.netAmount != null"> <app-riyal [decorative]="true" [size]="0.75"></app-riyal></span>
                </span>
              </div>
            } @else {
              <app-empty-state icon="bi-file-earmark-x" title="No order header recorded" description="This request has no RequestOrderHeaders row. The stored request and response remain available below."></app-empty-state>
            }

            <div class="invoice-card" *ngIf="detail.request.invoice as invoice; else noInvoice">
              <div class="field"><span class="field-label">Invoice barcode</span><span class="mono">{{ invoice.barcode || '—' }} <app-copy-button *ngIf="invoice.barcode" [value]="invoice.barcode" label="Copy invoice barcode"></app-copy-button></span></div>
              <div class="field"><span class="field-label">Close date</span><span>{{ invoice.closeDateLocalTime ? (invoice.closeDateLocalTime | date:'medium') : '—' }}</span></div>
              <div class="field"><span class="field-label">Invoice net</span><span *ngIf="invoice.netAmount != null; else noInvoiceNet">{{ invoice.netAmount | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></span></div>
              <div class="field"><span class="field-label">Paid amount</span><span *ngIf="invoice.paidAmount != null; else noPaidAmount">{{ invoice.paidAmount | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></span></div>
            </div>
            <ng-template #noInvoice><app-empty-state icon="bi-receipt" title="Not invoiced yet"></app-empty-state></ng-template>
            <ng-template #noInvoiceNet>—</ng-template>
            <ng-template #noPaidAmount>—</ng-template>

            <h2 class="subsection-title">Attempts timeline</h2>
            @if (detail.attempts.length > 0) {
              <div class="attempts-timeline">
                @for (attempt of detail.attempts; track attempt.id) {
                  <button type="button" class="attempt-row" [class.current]="attempt.id === detail.request.id" (click)="viewAttempt(attempt.id)">
                    <span class="outcome-dot" [class.dot-success]="attempt.isSucceeded === true" [class.dot-danger]="attempt.isSucceeded === false" aria-hidden="true"></span>
                    <span>{{ attempt.orderDate | date:'short' }}</span>
                    <span class="muted">{{ attempt.id === detail.request.id ? 'This attempt' : 'Request #' + attempt.id }}</span>
                    <span class="muted" *ngIf="attempt.hasException">has exception</span>
                  </button>
                }
              </div>
            } @else {
              <p class="muted">No other attempts are recorded for this order number.</p>
            }

            <h2 class="subsection-title">Order lineage</h2>
            <div class="lineage-trail">
              <button type="button" class="lineage-node" *ngIf="detail.lineage.parent as parent" (click)="viewByOrderNumber(parent.orderNumber)">{{ parent.orderNumber }} <span class="muted">(parent)</span></button>
              <span *ngIf="detail.lineage.parent" aria-hidden="true">→</span>
              <span class="lineage-node current">{{ detail.request.orderNumber }}</span>
              @if (detail.lineage.children.length > 0) {
                <span aria-hidden="true">→</span>
                @for (child of detail.lineage.children; track child.orderNumber) {
                  <button type="button" class="lineage-node" (click)="viewByOrderNumber(child.orderNumber)">{{ child.orderNumber }}</button>
                }
              } @else if (!detail.lineage.parent) {
                <span class="muted">No related orders.</span>
              }
            </div>
          </ui-section>

          <ui-section title="Transactions" [collapsible]="false">
            @if (detail.request.transactions.length > 0) {
              <div class="transaction-list">
                @for (transaction of detail.request.transactions; track $index) {
                  <article class="transaction-card">
                    <div class="transaction-main">
                      <strong>{{ transaction.eCommercePaymentMethod || 'Unknown method' }}</strong>
                      <span>{{ transaction.eCommercePaymentOption || '—' }}</span>
                    </div>
                    <div class="transaction-meta">
                      <span *ngIf="transaction.paymentStatus">Status: {{ transaction.paymentStatus }}</span>
                      <span *ngIf="transaction.transactionCode">Reference: <span class="mono">{{ transaction.transactionCode }}</span> <app-copy-button [value]="transaction.transactionCode" label="Copy transaction reference"></app-copy-button></span>
                      <span *ngIf="transaction.bankCode">Bank: {{ transaction.bankCode }}</span>
                      <span *ngIf="transaction.cardName">Card: {{ transaction.cardName }}</span>
                      <span *ngIf="transaction.optionCommission">Commission: {{ transaction.optionCommission | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.75"></app-riyal></span>
                    </div>
                    <strong class="transaction-amount">{{ transaction.paymentAmount | number:'1.2-2' }} <app-riyal [size]="0.8"></app-riyal></strong>
                  </article>
                }
              </div>
              <div class="consistency-check" [class.mismatch]="!paymentsConsistent(detail)">
                <i class="bi" [class.bi-check-circle-fill]="paymentsConsistent(detail)" [class.bi-exclamation-triangle-fill]="!paymentsConsistent(detail)" aria-hidden="true"></i>
                Payments total {{ sumPayments(detail) | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.75"></app-riyal>
                vs header net {{ detail.request.header?.netTotal ?? 0 | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.75"></app-riyal>
              </div>
            } @else {
              <app-empty-state icon="bi-credit-card" title="No payment transactions recorded"></app-empty-state>
            }
          </ui-section>

          <ui-section
            title="Request JSON"
            description="Stored request payload; collapsed by default"
            [expanded]="false">
            <app-json-tree *ngIf="detail.request.requestJson; else noRequestJson" title="Request JSON" [data]="detail.request.requestJson"></app-json-tree>
            <ng-template #noRequestJson><app-empty-state icon="bi-file-earmark-x" title="No RequestJson recorded"></app-empty-state></ng-template>
          </ui-section>

          <ui-section
            title="Response JSON"
            description="Stored response payload; collapsed by default"
            [expanded]="false"
            [hasIssues]="!!detail.request.exceptionMessage"
            [issueCount]="detail.request.exceptionMessage ? 1 : 0">
            <div class="outcome-banner" [class.success]="detail.request.isSucceeded === true" [class.danger]="detail.request.isSucceeded === false">
              <i class="bi" [class.bi-check-circle-fill]="detail.request.isSucceeded === true" [class.bi-x-circle-fill]="detail.request.isSucceeded === false" aria-hidden="true"></i>
              {{ detail.request.isSucceeded === true ? 'Succeeded' : detail.request.isSucceeded === false ? 'Failed' : 'Outcome unknown' }}
            </div>
            <div class="exception-card" *ngIf="detail.request.exceptionMessage">
              <div class="exception-header"><strong>ExceptionMessage</strong><app-copy-button [value]="detail.request.exceptionMessage" label="Copy exception"></app-copy-button></div>
              <pre class="exception-text">{{ detail.request.exceptionMessage }}</pre>
            </div>
            <app-json-tree *ngIf="detail.request.responseJson; else noResponseJson" title="Response JSON" [data]="detail.request.responseJson"></app-json-tree>
            <ng-template #noResponseJson>
              <app-empty-state *ngIf="!detail.request.exceptionMessage" icon="bi-inbox" title="No response recorded for this attempt" description="Neither ResponseJson nor ExceptionMessage was populated."></app-empty-state>
            </ng-template>
          </ui-section>
        </div>
      }
    </main>

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
      [status]="store.selected()?.request?.header?.orderStatus ?? null"
      [statusLabel]="store.selected()?.request?.header?.orderStatusLabel || null"
      [environmentKey]="moduleService.activeEnvironment()?.key || store.envKey()"
      [canResend]="canResend(store.selected()?.request)"
      [blockedReason]="resendActionTitle(store.selected()?.request)"
      [branches]="store.branches()"
      [branchesLoading]="store.branchStatus() === 'loading'"
      [branchError]="store.branchError()"
      (branchRefresh)="store.loadBranches(true)"
      [submitting]="resendSubmitting()"
      [errorMessage]="resendError()"
      (close)="showResend.set(false); resendError.set(null)"
      (confirm)="onResendConfirm($event)">
    </app-resend-request-dialog>

    <app-confirm-dialog
      *ngIf="showProdResendConfirm()"
      variant="danger"
      title="Resend on Production?"
      [message]="'This will send the stored request to the live API in ' + (moduleService.activeEnvironment()?.key || store.envKey() || 'the selected environment') + ' using the same order number. Confirm only when duplicate processing is intentional.'"
      [requiredTypedValue]="moduleService.activeEnvironment()?.key || store.envKey() || ''"
      reasonLabel="Type the environment name to confirm"
      [reasonPlaceholder]="moduleService.activeEnvironment()?.key || store.envKey() || ''"
      confirmLabel="Resend on Production"
      (cancel)="showProdResendConfirm.set(false); pendingResendBranch = null"
      (confirm)="onConfirmProdResend()">
    </app-confirm-dialog>

    <app-confirm-dialog
      *ngIf="showProdCancelConfirm()"
      variant="danger"
      title="Cancel on Production?"
      [message]="'This will send a real cancellation to the live RMS API on ' + (moduleService.activeEnvironment()?.key) + '.'"
      [requireReason]="true"
      [requiredTypedValue]="moduleService.activeEnvironment()?.key || ''"
      reasonLabel="Type the environment name to confirm"
      [reasonPlaceholder]="moduleService.activeEnvironment()?.key || ''"
      confirmLabel="Cancel order on Production"
      (cancel)="showProdCancelConfirm.set(false); pendingCancelResult = null"
      (confirm)="onConfirmProdCancel()">
    </app-confirm-dialog>
  `,
  styles: [`
    :host { display: block; }
    .order-details-page { display: flex; flex-direction: column; gap: 18px; }
    .detail-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 18px; flex-wrap: wrap; }
    .detail-heading { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
    .heading-row { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
    .eyebrow { margin: 0 0 3px; }
    h1.order-number { margin: 0; color: var(--text-primary); font-size: clamp(1.35rem, 3vw, 2rem); }
    .header-actions { display: flex; align-items: center; justify-content: flex-end; gap: 8px; flex-wrap: wrap; }
    .header-total { display: inline-flex; align-items: center; gap: 4px; color: var(--text-primary); font-size: 1rem; font-weight: 750; }
    .action-btn, .attempt-row, .lineage-node { cursor: pointer; font: inherit; }
    .action-btn { display: inline-flex; align-items: center; gap: 7px; min-height: 36px; padding: 8px 14px; border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); color: var(--text-primary); font-size: .82rem; font-weight: 650; }
    .action-btn.brand { border: 0; background: var(--grad-brand); color: var(--on-gradient); }
    .action-btn.danger { border: 0; background: var(--grad-danger); color: var(--on-gradient); }
    .action-btn.secondary { background: var(--surface-interactive); }
    .action-btn:disabled { cursor: not-allowed; opacity: .45; }
    .detail-context { display: flex; align-items: center; gap: 8px 18px; flex-wrap: wrap; padding: 11px 14px; border: 1px solid var(--border-subtle); color: var(--text-secondary); font-size: .78rem; }
    .blocked-hint { color: var(--state-warning-fg); }
    .detail-sections { display: flex; flex-direction: column; gap: 16px; }
    .detail-table-footer th, .detail-table-footer td { color: var(--state-success-fg); font-weight: 750; }
    .align-right { text-align: right !important; }
    .amount { white-space: nowrap; }
    .muted { color: var(--text-muted); }
    .small { display: block; font-size: .72rem; }
    .field-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 14px 18px; margin: 0; }
    .field { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .eyebrow, .detail-context strong, .field dt, .field-label, .totals-grid span { color: var(--text-muted); font-size: .7rem; font-weight: 750; letter-spacing: .04em; text-transform: uppercase; }
    .field dd { margin: 0; overflow-wrap: anywhere; color: var(--text-primary); font-size: .86rem; }
    .amber-callout { display: flex; gap: 10px; margin-top: 18px; padding: 12px 16px; border-radius: var(--radius-md); background: var(--state-warning-bg); color: var(--state-warning-fg); font-size: .84rem; }
    .totals-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; margin-top: 18px; }
    .totals-grid > div { display: flex; flex-direction: column; gap: 5px; padding: 12px; }
    .totals-grid strong { color: var(--text-primary); font-size: .92rem; }
    .totals-grid .net strong { color: var(--accent); font-size: 1.05rem; }
    .consistency-check { display: flex; align-items: center; gap: 7px; margin-top: 14px; color: var(--state-success-fg); font-size: .78rem; }
    .consistency-check.mismatch { color: var(--state-warning-fg); }
    .detail-context, .invoice-card, .totals-grid > div, .transaction-card, .outcome-banner { border-radius: var(--radius-md); background: var(--surface-raised); }
    .invoice-card { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 14px; margin-top: 18px; padding: 15px; }
    .attempts-timeline { display: flex; flex-direction: column; gap: 4px; }
    .attempt-row { display: flex; align-items: center; gap: 10px; width: 100%; padding: 8px 10px; border: 1px solid transparent; border-radius: var(--radius-sm); background: transparent; color: var(--text-primary); font-size: .8rem; text-align: left; }
    .attempt-row:hover { background: var(--surface-hover); }
    .attempt-row.current { border-color: var(--accent); }
    .outcome-dot { width: 8px; height: 8px; flex: 0 0 8px; border-radius: 50%; background: var(--text-muted); }
    .dot-success { background: var(--state-success-fg); }
    .dot-danger { background: var(--state-danger-fg); }
    .lineage-trail { display: flex; align-items: center; gap: 9px; flex-wrap: wrap; }
    .lineage-node { padding: 6px 12px; border: 1px solid var(--border-subtle); border-radius: var(--radius-pill); background: var(--surface-raised); color: var(--text-primary); font-size: .8rem; }
    .lineage-node.current { border: 0; background: var(--grad-brand); color: var(--on-gradient); font-weight: 700; }
    .transaction-list { display: flex; flex-direction: column; gap: 10px; }
    .transaction-card { display: flex; align-items: center; gap: 16px; padding: 13px 15px; }
    .transaction-main { display: flex; flex-direction: column; gap: 3px; min-width: 150px; }
    .transaction-main span, .transaction-meta { color: var(--text-secondary); font-size: .78rem; }
    .transaction-meta { display: flex; flex: 1; flex-wrap: wrap; gap: 6px 14px; align-items: center; }
    .transaction-amount { white-space: nowrap; }
    .outcome-banner { display: flex; align-items: center; gap: 8px; margin-bottom: 14px; padding: 11px 14px; font-weight: 700; }
    .outcome-banner.success { background: var(--state-success-bg); color: var(--state-success-fg); }
    .outcome-banner.danger { background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .exception-card { margin-bottom: 14px; padding: 14px 16px; border-radius: var(--radius-md); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .exception-header { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 8px; }
    .exception-text { margin: 0; color: var(--state-danger-fg); font-family: 'JetBrains Mono', monospace; font-size: .8rem; white-space: pre-wrap; word-break: break-word; }
    .state-panel { display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 300px; gap: 10px; padding: 28px; border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); color: var(--text-secondary); text-align: center; }
    .state-panel > i { color: var(--state-warning-fg); font-size: 2rem; }
    .state-panel h1 { margin: 0; color: var(--text-primary); font-size: 1.2rem; }
    .state-panel p { max-width: 520px; margin: 0 0 8px; }
    .state-actions { display: flex; gap: 8px; flex-wrap: wrap; justify-content: center; }
    .detail-skeleton { display: flex; flex-direction: column; gap: 16px; }
    @media (max-width: 720px) { .totals-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } .transaction-card { align-items: flex-start; flex-direction: column; gap: 8px; } .transaction-amount { align-self: flex-end; } }
    @media (max-width: 520px) { .header-actions { justify-content: flex-start; } }
  `]
})
export class OrderRequestDetailsComponent implements OnDestroy {
  store = inject(OrderRequestsStore);
  moduleService = inject(ModuleService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toast = inject(ToastService);
  private api = inject(ApiService);

  invalidId = signal(false);
  showCancel = signal(false);
  cancelSubmitting = signal(false);
  cancelError = signal<CancelErrorState | null>(null);
  showProdCancelConfirm = signal(false);
  pendingCancelResult: CancelDialogResult | null = null;

  showResend = signal(false);
  resendSubmitting = signal(false);
  resendError = signal<string | null>(null);
  showProdResendConfirm = signal(false);
  pendingResendBranch: string | null = null;

  private paramSub: Subscription = this.route.paramMap.subscribe(params => {
    const id = Number(params.get('orderId'));
    if (Number.isInteger(id) && id > 0) {
      this.invalidId.set(false);
      this.store.openDetail(id);
    } else {
      this.invalidId.set(true);
      this.store.closeDetail();
    }
  });

  ngOnDestroy() {
    this.paramSub.unsubscribe();
  }

  onClose() {
    this.store.closeDetail();
    this.router.navigate(['..'], { relativeTo: this.route, queryParamsHandling: 'preserve' });
  }

  retry() {
    const id = Number(this.route.snapshot.paramMap.get('orderId'));
    if (Number.isInteger(id) && id > 0) this.store.openDetail(id);
  }

  viewAttempt(id: number) {
    this.router.navigate(['..', id], { relativeTo: this.route, queryParamsHandling: 'preserve' });
  }

  viewByOrderNumber(orderNumber: string) {
    const key = this.store.moduleKey();
    this.api.get<{ request: OrderRequestDetail }>(`modules/${key}/order-requests/by-order/${encodeURIComponent(orderNumber)}`, { envKey: this.store.envKey() || undefined }).subscribe({
      next: response => this.router.navigate(['..', response.request.id], { relativeTo: this.route, queryParamsHandling: 'preserve' }),
      error: () => this.toast.showError('The related order could not be loaded.')
    });
  }

  materialViews(materialNumber: string | null) { return materialNumberViews(materialNumber); }

  canResend(detail: OrderRequestDetail | null | undefined): boolean {
    const header = detail?.header;
    return !!header && canResendStatus(header.orderStatus ?? header.orderStatusLabel);
  }

  resendActionTitle(detail: OrderRequestDetail | null | undefined): string {
    const header = detail?.header;
    if (!header) return 'Resend unavailable: this request has no order header.';
    return canResendStatus(header.orderStatus ?? header.orderStatusLabel)
      ? 'Resend the stored request using the same order number.'
      : resendBlockedReason(header.orderStatus ?? header.orderStatusLabel);
  }

  openResend(detail: OrderRequestDetail) {
    if (!this.canResend(detail) || this.resendSubmitting()) return;
    this.resendError.set(null);
    this.showResend.set(true);
  }

  totalsConsistent(detail: { request: OrderRequestDetail }): boolean {
    const header = detail.request.header;
    if (!header) return true;
    const invoiceNet = detail.request.invoice?.netAmount;
    const close = (a: number, b: number) => Math.abs(a - b) < 0.05;
    return close(detail.request.netTotal, header.netTotal) && (invoiceNet == null || close(header.netTotal, invoiceNet));
  }

  sumDiscount(detail: { request: OrderRequestDetail }): number {
    return detail.request.details.reduce((sum, line) => sum + (line.totalDiscount || 0), 0);
  }

  sumVat(detail: { request: OrderRequestDetail }): number {
    return detail.request.details.reduce((sum, line) => sum + (line.itemVat || 0), 0);
  }

  sumTotal(detail: { request: OrderRequestDetail }): number {
    return detail.request.details.reduce((sum, line) => sum + (line.totalPrice || 0), 0);
  }

  lineItemsConsistent(detail: { request: OrderRequestDetail }): boolean {
    const header = detail.request.header;
    return !header || Math.abs(this.sumTotal(detail) - header.netTotal) < 0.5;
  }

  sumPayments(detail: { request: OrderRequestDetail }): number {
    return detail.request.transactions.reduce((sum, transaction) => sum + (transaction.paymentAmount || 0), 0);
  }

  paymentsConsistent(detail: { request: OrderRequestDetail }): boolean {
    const header = detail.request.header;
    return !header || detail.request.transactions.length === 0 || Math.abs(this.sumPayments(detail) - header.netTotal) < 0.5;
  }

  onCancelConfirm(result: CancelDialogResult) {
    if (this.moduleService.activeEnvironment()?.environment === 'Production') {
      this.pendingCancelResult = result;
      this.showProdCancelConfirm.set(true);
      return;
    }
    this.performCancel(result);
  }

  onConfirmProdCancel() {
    this.showProdCancelConfirm.set(false);
    if (this.pendingCancelResult) {
      this.performCancel(this.pendingCancelResult);
      this.pendingCancelResult = null;
    }
  }

  private performCancel(result: CancelDialogResult) {
    const detail = this.store.selected();
    if (!detail || this.cancelSubmitting()) return;
    const key = this.store.moduleKey();
    this.cancelSubmitting.set(true);
    this.cancelError.set(null);

    this.api.post<OrderRequestCancelResponse>(
      `modules/${key}/order-requests/${detail.request.id}/cancel`,
      { reason: result.reason, customUrl: result.customUrl },
      { envKey: this.store.envKey() || undefined }
    ).subscribe({
      next: response => {
        this.cancelSubmitting.set(false);
        if (response.success) {
          this.showCancel.set(false);
          this.store.refreshDetailInPlace({ ...detail, request: response.request });
          this.toast.showSuccess(`Order ${detail.request.orderNumber} cancelled successfully.`);
        } else {
          this.cancelError.set({ kind: 'upstream', message: `Upstream returned status ${response.statusCode}.`, rawBody: response.responseText });
        }
      },
      error: (err: ApiError) => {
        this.cancelSubmitting.set(false);
        this.cancelError.set(err?.status === 409
          ? { kind: 'blocked', message: err.message || 'This order can no longer be cancelled.' }
          : { kind: 'network', message: err?.message || 'The cancel request could not be completed.' });
      }
    });
  }

  onResendConfirm(branchCode: string) {
    const detail = this.store.selected()?.request;
    if (!detail || !this.canResend(detail)) {
      this.resendError.set(this.resendActionTitle(detail));
      return;
    }

    if (this.moduleService.activeEnvironment()?.environment === 'Production') {
      this.pendingResendBranch = branchCode;
      this.showProdResendConfirm.set(true);
      return;
    }
    this.performResend(branchCode);
  }

  onConfirmProdResend() {
    this.showProdResendConfirm.set(false);
    if (this.pendingResendBranch) this.performResend(this.pendingResendBranch);
    this.pendingResendBranch = null;
  }

  private performResend(branchCode: string) {
    const detail = this.store.selected()?.request;
    if (!detail || !this.canResend(detail) || this.resendSubmitting()) return;
    const targetBranch = branchCode.trim();
    if (!targetBranch) {
      this.resendError.set('A target branch code is required.');
      return;
    }

    this.resendSubmitting.set(true);
    this.resendError.set(null);
    const key = this.store.moduleKey();
    this.api.post<SendOrderResult>(
      `modules/${key}/order-requests/${detail.id}/resend`,
      { branchCode: targetBranch },
      { envKey: this.store.envKey() || undefined }
    ).subscribe({
      next: response => {
        this.resendSubmitting.set(false);
        if (response.success) {
          this.showResend.set(false);
          this.toast.showSuccess(`Order ${detail.orderNumber} resent with the same number. Upstream status ${response.statusCode}.`);
          this.store.refresh();
        } else {
          this.resendError.set(`Resend failed: upstream returned status ${response.statusCode}.`);
          this.toast.showError(`Resend failed with upstream status ${response.statusCode}.`);
        }
      },
      error: (err: ApiError) => {
        this.resendSubmitting.set(false);
        this.resendError.set(err?.status === 409
          ? err.message || 'This order is no longer eligible for resend.'
          : err?.message || 'The resend request could not be completed.');
      }
    });
  }
}
