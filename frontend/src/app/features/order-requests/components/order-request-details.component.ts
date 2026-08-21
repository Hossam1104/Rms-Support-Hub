import { Component, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { OrderRequestsStore } from '../order-requests.store';
import { OrderRequestDetail, OrderRequestCancelResponse, SendOrderResult, ApiError } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import { ApiService } from '../../../core/services/api.service';
import { ModuleService } from '../../../core/services/module.service';
import { ProductionUnlockService } from '../../../core/services/production-unlock.service';
import { APP_ASSETS, AssetPath, paymentAssetForMethod } from '../../../core/config/app-assets';
import {
  StatusPillComponent, JsonTreeComponent, RiyalComponent, UiSectionComponent,
  CopyButtonComponent, EmptyStateComponent, SkeletonComponent, ConfirmDialogComponent
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
    ResendRequestDialogComponent, ConfirmDialogComponent
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
              <app-riyal [size]="0.9"></app-riyal>{{ detail.request.netTotal | number:'1.2-2' }}
            </span>
            <button
              *ngIf="canResend(detail.request)"
              type="button"
              class="action-btn brand"
              [disabled]="!canResend(detail.request) || resendSubmitting()"
              [class.action-btn--locked]="productionLocked() && canResend(detail.request)"
              [title]="productionLocked() && canResend(detail.request) ? 'Unlock Production to resend this request' : resendActionTitle(detail.request)"
              (click)="openResend(detail.request)">
              <i class="bi" [class.bi-lock-fill]="productionLocked() && canResend(detail.request)" [class.bi-arrow-repeat]="!productionLocked() || !canResend(detail.request)" aria-hidden="true"></i>
              {{ resendSubmitting() ? 'Resending...' : 'Resend' }}
            </button>
            <button
              *ngIf="canCancel(detail.request)"
              type="button"
              class="action-btn danger"
              [disabled]="!canCancel(detail.request) || cancelSubmitting()"
              [class.action-btn--locked]="productionLocked() && canCancel(detail.request)"
              [title]="productionLocked() && canCancel(detail.request) ? 'Unlock Production to cancel this order' : canCancel(detail.request) ? '' : 'Blocked: order status ' + (detail.request.header?.orderStatusLabel || 'unknown')"
              (click)="openCancel()">
              <i class="bi" [class.bi-lock-fill]="productionLocked() && canCancel(detail.request)" [class.bi-x-circle]="!productionLocked() || !canCancel(detail.request)" aria-hidden="true"></i> Cancel order
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
              <div class="items-shell">
                <div class="items-layout">
                  <div class="items-grid" role="table" aria-label="Order items and Riyal amounts">
                    <div class="ihead" role="row">
                      <span role="columnheader">Item / Material</span>
                      <span role="columnheader">Qty</span>
                      <span role="columnheader">Unit<span class="sr-only"> price, Saudi Riyal</span></span>
                      <span role="columnheader">Discount<span class="sr-only">, Saudi Riyal</span></span>
                      <span role="columnheader">VAT<span class="sr-only">, Saudi Riyal</span></span>
                      <span role="columnheader">Total<span class="sr-only">, Saudi Riyal</span></span>
                    </div>

                    <div class="ibody" role="rowgroup">
                      @for (line of detail.request.details; track $index) {
                        <div class="irow" role="row">
                          <div class="irow__id" role="rowheader">
                            <span class="item-identity">{{ line.itemName || '—' }}</span>
                            <div class="item-sub-row">
                              <span class="material-num mono" *ngIf="line.materialNumber" [title]="'18-digit: ' + materialViews(line.materialNumber).full">Material {{ materialViews(line.materialNumber).short || '—' }}</span>
                              <span class="commerce-offer-mark" *ngIf="line.offerMessage">
                                <img [src]="assets.commerce.offer" alt="" aria-hidden="true">
                                <span>{{ line.offerCode || 'Offer' }}: {{ line.offerMessage }}</span>
                              </span>
                            </div>
                          </div>

                          <div class="icell icell--qty" role="cell">
                            <span class="icell__label">Qty</span>
                            <span class="icell__value icell__value--qty">{{ line.quantity }}</span>
                          </div>

                          <div class="icell icell--unit" role="cell">
                            <span class="icell__label">Unit</span>
                            <span class="icell__value amount"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ line.unitPrice | number:'1.2-2' }}</span>
                          </div>

                          <div class="icell icell--disc" role="cell">
                            <span class="icell__label">Discount</span>
                            <span class="icell__value amount" [class.is-zero]="!line.totalDiscount"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ line.totalDiscount | number:'1.2-2' }}</span>
                          </div>

                          <div class="icell icell--vat" role="cell">
                            <span class="icell__label">VAT</span>
                            <span class="icell__value amount" [class.is-zero]="!line.itemVat"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ line.itemVat | number:'1.2-2' }}</span>
                            <span class="icell__rate">{{ line.itemVatPercentage }}%</span>
                          </div>

                          <div class="icell icell--total" role="cell">
                            <span class="icell__label">Total</span>
                            <span class="icell__value amount total-amount"><app-riyal [decorative]="true" [size]="0.8"></app-riyal>{{ line.totalPrice | number:'1.2-2' }}</span>
                          </div>
                        </div>
                      }
                    </div>
                  </div>

                  <div class="items-summary" role="group" aria-label="Items totals">
                    <div class="isum-row">
                      <span class="isum isum--disc">
                        <span class="isum__label">Discount</span>
                        <span class="isum__value amount"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ sumDiscount(detail) | number:'1.2-2' }}</span>
                      </span>
                      <span class="isum isum--vat">
                        <span class="isum__label">VAT</span>
                        <span class="isum__value amount"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ sumVat(detail) | number:'1.2-2' }}</span>
                      </span>
                      <span class="isum isum--total">
                        <span class="isum__label">Items total</span>
                        <span class="isum__value amount"><app-riyal [size]="0.88"></app-riyal>{{ sumTotal(detail) | number:'1.2-2' }}</span>
                      </span>
                    </div>

                    <p class="mismatch-callout" [class.is-ok]="lineItemsConsistent(detail)" role="status">
                      <i class="bi" [class.bi-check-circle-fill]="lineItemsConsistent(detail)" [class.bi-exclamation-triangle-fill]="!lineItemsConsistent(detail)" aria-hidden="true"></i>
                      @if (lineItemsConsistent(detail)) {
                        <span>Items total matches header total</span>
                      } @else {
                        <span>
                          <strong>Mismatch</strong>
                          Items <app-riyal [decorative]="true" [size]="0.75"></app-riyal>{{ sumTotal(detail) | number:'1.2-2' }}
                          · Header <app-riyal [decorative]="true" [size]="0.75"></app-riyal>{{ detail.request.header?.netTotal ?? 0 | number:'1.2-2' }}
                        </span>
                      }
                    </p>
                  </div>
                </div>
              </div>
            } @else {
              <app-empty-state icon="bi-box" title="No line items recorded"></app-empty-state>
            }
          </ui-section>

          <ui-section title="Order Info" [collapsible]="false">
            @if (detail.request.header; as h) {
              <div class="oi-primary" aria-label="Customer and fulfillment">
                <div class="oi-fact">
                  <span class="oi-fact__label">Branch</span>
                  <span class="oi-fact__value">{{ h.branchCode || '—' }}</span>
                  <span class="oi-fact__sub" *ngIf="h.branchName">{{ h.branchName }}</span>
                </div>
                <div class="oi-fact">
                  <span class="oi-fact__label">Customer mobile</span>
                  <span class="oi-fact__value mono">{{ h.consumerMobile || '—' }}</span>
                </div>
                <div class="oi-fact oi-fact--wide">
                  <span class="oi-fact__label">Delivery address</span>
                  <span class="oi-fact__value oi-fact__value--prose">{{ h.address || '—' }}</span>
                </div>
                <div class="oi-fact">
                  <span class="oi-fact__label">Payment method</span>
                  <span class="oi-fact__value">{{ h.orderPaymentMethod || '—' }}</span>
                </div>
              </div>
            }

            <dl class="oi-meta">
              <div><dt>Request ID</dt><dd class="mono">{{ detail.request.id }}</dd></div>
              <div><dt>Order number</dt><dd class="mono">{{ detail.request.orderNumber }}</dd></div>
              @if (detail.request.header; as h) {
                <div><dt>Status</dt><dd>{{ h.orderStatusLabel }}</dd></div>
                <div><dt>Order date</dt><dd>{{ h.orderDate | date:'MMM d, y, h:mm a' }}</dd></div>
              }
              <div><dt>Received</dt><dd>{{ detail.request.orderDate | date:'MMM d, y, h:mm a' }}</dd></div>
              <div><dt>Module</dt><dd>{{ moduleService.activeModule()?.label || store.moduleKey() }}</dd></div>
              <div><dt>Environment</dt><dd>{{ store.envKey() || 'Default environment' }}</dd></div>
            </dl>

            @if (detail.request.header; as h) {
              <div class="oi-notes" *ngIf="h.orderNote || h.parentOrderNumber">
                <p *ngIf="h.orderNote"><span class="oi-notes__label">Order note</span>{{ h.orderNote }}</p>
                <p *ngIf="h.parentOrderNumber"><span class="oi-notes__label">Parent order</span><button type="button" class="link-node mono" (click)="viewByOrderNumber(h.parentOrderNumber!)">{{ h.parentOrderNumber }}</button></p>
              </div>

              <div class="amber-callout" *ngIf="h.rejectionMessage" role="note">
                <i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>
                <div><strong>Rejection message:</strong> {{ h.rejectionMessage }}</div>
              </div>

              <h2 class="subsection-title">Financial summary</h2>
              <div class="fin-bar" aria-label="Order totals">
                <div class="fin"><span class="fin__label">Gross</span><span class="fin__value"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ h.grossTotal | number:'1.2-2' }}</span></div>
                <div class="fin"><span class="fin__label">Discount</span><span class="fin__value" [class.is-zero]="!h.totalDiscount"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ h.totalDiscount | number:'1.2-2' }}</span></div>
                <div class="fin"><span class="fin__label">VAT</span><span class="fin__value" [class.is-zero]="!h.totalVat"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ h.totalVat | number:'1.2-2' }}</span></div>
                <div class="fin fin--net"><span class="fin__label">Net</span><span class="fin__value"><app-riyal [size]="0.9"></app-riyal>{{ h.netTotal | number:'1.2-2' }}</span></div>
              </div>

              <p class="consistency-check" [class.mismatch]="!totalsConsistent(detail)">
                <i class="bi" [class.bi-check-circle-fill]="totalsConsistent(detail)" [class.bi-exclamation-triangle-fill]="!totalsConsistent(detail)" aria-hidden="true"></i>
                <span>
                  Request <app-riyal [decorative]="true" [size]="0.72"></app-riyal>{{ detail.request.netTotal | number:'1.2-2' }}
                  · Header <app-riyal [decorative]="true" [size]="0.72"></app-riyal>{{ h.netTotal | number:'1.2-2' }}
                  · Invoice <span *ngIf="detail.request.invoice?.netAmount != null; else noInvoiceNet"><app-riyal [decorative]="true" [size]="0.72"></app-riyal>{{ detail.request.invoice!.netAmount | number:'1.2-2' }}</span>
                </span>
              </p>
            } @else {
              <app-empty-state icon="bi-file-earmark-x" title="No order header recorded" description="This request has no RequestOrderHeaders row. The stored request and response remain available below."></app-empty-state>
            }

            <h2 class="subsection-title">Invoice</h2>
            <div class="inv" *ngIf="detail.request.invoice as invoice; else noInvoice">
              <div class="inv__identity">
                <span class="inv__label">Barcode</span>
                <span class="inv__barcode mono">{{ invoice.barcode || '—' }}</span>
                <app-copy-button *ngIf="invoice.barcode" [value]="invoice.barcode" label="Copy invoice barcode"></app-copy-button>
              </div>
              <div class="inv__facts">
                <div class="inv__fact"><span class="inv__label">Closed</span><span class="inv__value">{{ invoice.closeDateLocalTime ? (invoice.closeDateLocalTime | date:'MMM d, y, h:mm a') : '—' }}</span></div>
                <div class="inv__fact"><span class="inv__label">Net</span><span class="inv__value inv__value--num" *ngIf="invoice.netAmount != null; else noInvoiceNet"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ invoice.netAmount | number:'1.2-2' }}</span></div>
                <div class="inv__fact"><span class="inv__label">Paid</span><span class="inv__value inv__value--num" *ngIf="invoice.paidAmount != null; else noPaidAmount"><app-riyal [decorative]="true" [size]="0.78"></app-riyal>{{ invoice.paidAmount | number:'1.2-2' }}</span></div>
              </div>
            </div>
            <ng-template #noInvoice><p class="quiet-note"><i class="bi bi-receipt" aria-hidden="true"></i> Not invoiced yet.</p></ng-template>
            <ng-template #noInvoiceNet>—</ng-template>
            <ng-template #noPaidAmount>—</ng-template>

            <h2 class="subsection-title">Attempts</h2>
            @if (detail.attempts.length > 0) {
              <ol class="tl">
                @for (attempt of detail.attempts; track attempt.id) {
                  <li class="tl__item">
                    <button type="button" class="tl__btn" [class.is-current]="attempt.id === detail.request.id" (click)="viewAttempt(attempt.id)">
                      <span class="tl__dot" [class.dot-success]="attempt.isSucceeded === true" [class.dot-danger]="attempt.isSucceeded === false" aria-hidden="true"></span>
                      <span class="tl__when">{{ attempt.orderDate | date:'MMM d, y, h:mm a' }}</span>
                      <span class="tl__what">{{ attempt.id === detail.request.id ? 'This attempt' : 'Request #' + attempt.id }}</span>
                      <span class="tl__flag" *ngIf="attempt.hasException">exception</span>
                    </button>
                  </li>
                }
              </ol>
            } @else {
              <p class="quiet-note">No other attempts are recorded for this order number.</p>
            }

            <h2 class="subsection-title">Order lineage</h2>
            <div class="lineage-trail">
              <button type="button" class="lineage-node" *ngIf="detail.lineage.parent as parent" (click)="viewByOrderNumber(parent.orderNumber)">{{ parent.orderNumber }} <span class="muted">parent</span></button>
              <span class="lineage-arrow" *ngIf="detail.lineage.parent" aria-hidden="true">→</span>
              <span class="lineage-node current">{{ detail.request.orderNumber }}</span>
              @if (detail.lineage.children.length > 0) {
                <span class="lineage-arrow" aria-hidden="true">→</span>
                @for (child of detail.lineage.children; track child.orderNumber) {
                  <button type="button" class="lineage-node" (click)="viewByOrderNumber(child.orderNumber)">{{ child.orderNumber }}</button>
                }
              } @else if (!detail.lineage.parent) {
                <span class="quiet-note">No related orders.</span>
              }
            </div>
          </ui-section>

          <ui-section title="Transactions" [collapsible]="false">
            @if (detail.request.transactions.length > 0) {
              <div class="transaction-list">
                @for (transaction of detail.request.transactions; track $index) {
                  <article class="transaction-card">
                    <div class="transaction-main">
                      <span class="commerce-payment-method">
                        @if (paymentAsset(transaction.eCommercePaymentMethod); as logo) {
                          <img [src]="logo" [alt]="(transaction.eCommercePaymentMethod || 'Payment') + ' payment logo'">
                        } @else {
                          <i class="bi bi-credit-card-2-front" aria-hidden="true"></i>
                        }
                        <strong>{{ transaction.eCommercePaymentMethod || 'Unknown method' }}</strong>
                      </span>
                      <span>{{ transaction.eCommercePaymentOption || '—' }}</span>
                    </div>
                    <div class="transaction-meta">
                      <span *ngIf="transaction.paymentStatus">Status: {{ transaction.paymentStatus }}</span>
                      <span *ngIf="transaction.transactionCode">Reference: <span class="mono">{{ transaction.transactionCode }}</span> <app-copy-button [value]="transaction.transactionCode" label="Copy transaction reference"></app-copy-button></span>
                      <span *ngIf="transaction.bankCode">Bank: {{ transaction.bankCode }}</span>
                      <span *ngIf="transaction.cardName">Card: {{ transaction.cardName }}</span>
                      <span *ngIf="transaction.optionCommission">Commission: {{ transaction.optionCommission | number:'1.2-2' }} <app-riyal [decorative]="true" [size]="0.75"></app-riyal></span>
                    </div>
                    <strong class="transaction-amount"><app-riyal [size]="0.8"></app-riyal>{{ transaction.paymentAmount | number:'1.2-2' }}</strong>
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
  `]
})
export class OrderRequestDetailsComponent implements OnDestroy {
  readonly assets = APP_ASSETS;
  store = inject(OrderRequestsStore);
  moduleService = inject(ModuleService);
  private readonly productionUnlock = inject(ProductionUnlockService);
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

  paymentAsset(method: string | null | undefined): AssetPath | null {
    return paymentAssetForMethod(method);
  }

  canResend(detail: OrderRequestDetail | null | undefined): boolean {
    const header = detail?.header;
    const capability = this.moduleService.activeModule()?.capabilities?.resend;
    return capability !== false && !!header && canResendStatus(header.orderStatus ?? header.orderStatusLabel);
  }

  canCancel(detail: OrderRequestDetail | null | undefined): boolean {
    const capability = this.moduleService.activeModule()?.capabilities?.cancel;
    return capability !== false && !!detail?.header?.canCancel;
  }

  productionLocked(): boolean {
    const environment = this.moduleService.activeEnvironment();
    return environment?.environment === 'Production'
      && !this.productionUnlock.isUnlocked(this.store.moduleKey(), environment.key);
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
    if (this.productionLocked()) {
      this.productionUnlock.open({
        moduleKey: this.store.moduleKey(),
        environmentKey: this.moduleService.activeEnvironment()?.key || this.store.envKey() || '',
        destination: 'order-requests'
      });
      return;
    }
    this.resendError.set(null);
    this.showResend.set(true);
  }

  openCancel(): void {
    const detail = this.store.selected()?.request;
    if (!this.canCancel(detail) || this.cancelSubmitting()) return;
    if (this.productionLocked()) {
      this.productionUnlock.open({
        moduleKey: this.store.moduleKey(),
        environmentKey: this.moduleService.activeEnvironment()?.key || this.store.envKey() || '',
        destination: 'order-requests'
      });
      return;
    }
    this.showCancel.set(true);
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
    if (this.productionLocked()) {
      this.productionUnlock.open({
        moduleKey: this.store.moduleKey(),
        environmentKey: this.moduleService.activeEnvironment()?.key || this.store.envKey() || '',
        destination: 'order-requests'
      });
      return;
    }
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
    const envKey = this.store.envKey() || '';
    const headers = this.productionUnlock.mutationHeaders(key, envKey);
    this.cancelSubmitting.set(true);
    this.cancelError.set(null);

    const request$ = headers
      ? this.api.post<OrderRequestCancelResponse>(
        `modules/${key}/order-requests/${detail.request.id}/cancel`,
        { reason: result.reason },
        { envKey: envKey || undefined },
        headers)
      : this.api.post<OrderRequestCancelResponse>(
        `modules/${key}/order-requests/${detail.request.id}/cancel`,
        { reason: result.reason },
        { envKey: envKey || undefined });

    request$.subscribe({
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

    if (this.productionLocked()) {
      this.productionUnlock.open({
        moduleKey: this.store.moduleKey(),
        environmentKey: this.moduleService.activeEnvironment()?.key || this.store.envKey() || '',
        destination: 'order-requests'
      });
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
    const envKey = this.store.envKey() || '';
    const headers = this.productionUnlock.mutationHeaders(key, envKey);
    const request$ = headers
      ? this.api.post<SendOrderResult>(
        `modules/${key}/order-requests/${detail.id}/resend`,
        { branchCode: targetBranch },
        { envKey: envKey || undefined },
        headers)
      : this.api.post<SendOrderResult>(
        `modules/${key}/order-requests/${detail.id}/resend`,
        { branchCode: targetBranch },
        { envKey: envKey || undefined });

    request$.subscribe({
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
