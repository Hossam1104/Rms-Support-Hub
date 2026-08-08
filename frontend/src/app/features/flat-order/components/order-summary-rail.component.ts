import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EnvironmentDto, ModuleEndpoint, TotalsSummary } from '../../../core/models';
import { EmptyStateComponent, RiyalComponent, SkeletonComponent, UiButtonComponent } from '../../../shared/ui';

export interface OrderValidationIssue {
  key: string;
  message: string;
  targetId: string | null;
}

export interface OrderValidationSummary {
  totalCount: number;
  globalErrors: string[];
}

/**
 * Server-owned order summary and action surface. It deliberately contains no
 * money arithmetic: every amount is read from TotalsSummary and is kept
 * visible during a refresh or an error by the parent.
 */
@Component({
  selector: 'app-order-summary-rail',
  standalone: true,
  imports: [CommonModule, EmptyStateComponent, RiyalComponent, SkeletonComponent, UiButtonComponent],
  template: `
    <ng-container *ngIf="compact; else fullRail">
      <section class="summary-action-bar" aria-label="Order actions" data-testid="order-summary-action-bar">
        <div class="summary-action-bar__status">
          <span class="summary-action-bar__environment" [class.is-production]="isProduction()">{{ environmentLabel() }}</span>
          <span class="summary-action-bar__total" *ngIf="totals; else compactPending">
            <app-riyal [size]=".86"></app-riyal>{{ totals!.totalOrderAmount | number:'1.2-2' }}
          </span>
          <ng-template #compactPending><span class="summary-action-bar__muted">{{ loading ? 'Refreshing totals…' : validationStatusLabel() }}</span></ng-template>
          <span class="summary-action-bar__balance" *ngIf="totals">Balance {{ totals!.remainingBalance | number:'1.2-2' }}</span>
          <span class="summary-action-bar__cod" *ngIf="isCashOnDelivery" data-testid="summary-action-bar-cod">Cash on Delivery</span>
          <span class="summary-action-bar__validation" *ngIf="hasValidationIssues()">{{ knownIssueCount() }} issue{{ knownIssueCount() === 1 ? '' : 's' }}</span>
        </div>
        <div class="summary-action-bar__actions">
          <ui-button variant="secondary" size="sm" icon="bi bi-check2-circle" [loading]="loading" [disabled]="sending" (pressed)="validate.emit()">
            Validate
          </ui-button>
          <ui-button variant="primary" size="sm" icon="bi bi-send" [loading]="sending" loadingLabel="Sending" [disabled]="sendDisabled()" [ariaLabel]="sendDisabledReason()" (pressed)="onSend()">
            Send
          </ui-button>
        </div>
      </section>
    </ng-container>

    <ng-template #fullRail>
      <aside class="summary-rail" aria-label="Order summary" data-testid="order-summary-rail">
        <div class="summary-rail__header">
          <div>
            <p class="summary-rail__eyebrow">Live order control</p>
            <h2>Order summary</h2>
          </div>
          <span class="summary-rail__environment" [class.is-production]="isProduction()">{{ environmentLabel() }}</span>
        </div>

        <div class="summary-rail__counts">
          <div><span>Items</span><strong>{{ itemCount }}</strong></div>
          <div><span>Quantity</span><strong>{{ totalQuantity }}</strong></div>
        </div>

        <section class="summary-rail__totals" aria-labelledby="summary-totals-heading">
          <h3 id="summary-totals-heading">Server totals</h3>
          <ng-container *ngIf="totals; else totalsState">
            <dl>
              <div><dt>Subtotal</dt><dd><app-riyal></app-riyal>{{ totals!.totalProductAmount | number:'1.2-2' }}</dd></div>
              <div><dt>Discount</dt><dd class="is-discount"><app-riyal></app-riyal>{{ totals!.orderDiscount | number:'1.2-2' }}</dd></div>
              <div><dt>VAT</dt><dd><app-riyal></app-riyal>{{ totals!.totalProductVat | number:'1.2-2' }}</dd></div>
              <div><dt>Delivery</dt><dd><app-riyal></app-riyal>{{ totals!.deliveryCost | number:'1.2-2' }}</dd></div>
              <div class="summary-rail__grand-total"><dt>Net total</dt><dd><app-riyal></app-riyal>{{ totals!.totalOrderAmount | number:'1.2-2' }}</dd></div>
              <div><dt>Paid</dt><dd><app-riyal></app-riyal>{{ totals!.totalPaidAmount | number:'1.2-2' }}</dd></div>
              <div class="summary-rail__balance"><dt>Balance</dt><dd><app-riyal></app-riyal>{{ totals!.remainingBalance | number:'1.2-2' }}</dd></div>
            </dl>
            <p class="summary-rail__cod" *ngIf="isCashOnDelivery" data-testid="summary-cod-note">
              <i class="bi bi-cash-coin" aria-hidden="true"></i>No payment selected — sending as Cash on Delivery.
            </p>
          </ng-container>
          <ng-template #totalsState>
            <div class="summary-rail__loading" *ngIf="loading; else totalsUnavailable">
              <app-skeleton width="76%"></app-skeleton>
              <app-skeleton width="58%"></app-skeleton>
              <app-skeleton width="68%"></app-skeleton>
            </div>
            <ng-template #totalsUnavailable>
              <app-empty-state icon="bi-activity" title="Totals unavailable" description="The server has not returned a totals snapshot yet."></app-empty-state>
            </ng-template>
          </ng-template>
          <p class="summary-rail__error" *ngIf="error" role="status"><i class="bi bi-cloud-slash" aria-hidden="true"></i>{{ error }}</p>
        </section>

        <section class="summary-rail__validation" aria-labelledby="summary-validation-heading">
          <div class="summary-rail__section-heading">
            <h3 id="summary-validation-heading">Validation</h3>
            <span class="validation-pill" [class.is-invalid]="hasValidationIssues()">{{ validationStatusLabel() }}</span>
          </div>
          <p class="summary-rail__hint" *ngIf="!hasValidationIssues()">Server validation is applied before the request leaves this tool.</p>
          <ul class="summary-rail__issues" *ngIf="validationIssues.length || validationSummary?.globalErrors?.length">
            <li *ngFor="let issue of validationIssues">
              <button type="button" (click)="issueSelected.emit(issue)" [attr.aria-label]="'Open ' + issue.message">{{ issue.message }}</button>
            </li>
            <li *ngFor="let message of validationSummary?.globalErrors ?? []" class="is-global">{{ message }}</li>
          </ul>
        </section>

        <section class="summary-rail__environment-panel" aria-labelledby="summary-environment-heading">
          <div class="summary-rail__section-heading">
            <h3 id="summary-environment-heading">Destination</h3>
            <span class="destination-badge" [class.is-production]="isProduction()">{{ isProduction() ? 'PRODUCTION' : 'TESTING' }}</span>
          </div>
          <p>{{ environment?.key || 'Environment unresolved' }}</p>
          <p class="summary-rail__endpoint">{{ endpointSummary() }}</p>
          <p class="summary-rail__custom" *ngIf="customEndpointEnabled">
            <i class="bi bi-link-45deg" aria-hidden="true"></i>
            Custom endpoint {{ customEndpointValid ? 'ready' : 'needs attention' }}
          </p>
        </section>

        <div class="summary-rail__actions">
          <ui-button variant="secondary" icon="bi bi-check2-circle" [loading]="loading" [disabled]="sending" (pressed)="validate.emit()">
            Validate
          </ui-button>
          <ui-button variant="primary" icon="bi bi-send" [loading]="sending" loadingLabel="Sending order" [disabled]="sendDisabled()" [ariaLabel]="sendDisabledReason()" (pressed)="onSend()">
            Send order
          </ui-button>
          <p class="summary-rail__action-hint" *ngIf="sendDisabled()">{{ sendDisabledReason() }}</p>
        </div>
      </aside>
    </ng-template>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .summary-rail { display: flex; flex-direction: column; gap: var(--space-4); padding: var(--panel-padding); border: 1px solid var(--border-subtle); border-radius: var(--panel-radius); background: var(--surface-panel); box-shadow: var(--shadow-md); color: var(--text-primary); }
    .summary-rail__header, .summary-rail__section-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-3); }
    .summary-rail__eyebrow { margin: 0 0 4px; color: var(--accent); font-size: .68rem; font-weight: 850; letter-spacing: .1em; text-transform: uppercase; }
    h2, h3, p { margin: 0; }
    h2 { font-size: 1.25rem; letter-spacing: -.02em; }
    h3 { font-size: .8rem; font-weight: 850; letter-spacing: .06em; text-transform: uppercase; }
    .summary-rail__environment, .destination-badge { display: inline-flex; align-items: center; min-height: 24px; padding: 0 9px; border: 1px solid var(--state-success-border); border-radius: var(--radius-pill); background: var(--state-success-bg); color: var(--state-success-fg); font-size: .66rem; font-weight: 850; letter-spacing: .06em; white-space: nowrap; }
    .summary-rail__environment.is-production, .destination-badge.is-production { border-color: var(--state-danger-border); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .summary-rail__counts { display: grid; grid-template-columns: repeat(2, 1fr); gap: var(--space-2); }
    .summary-rail__counts div { padding: var(--panel-padding-compact); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); }
    .summary-rail__counts span { display: block; color: var(--text-muted); font-size: .72rem; }
    .summary-rail__counts strong { display: block; margin-top: 4px; font-size: 1.3rem; }
    .summary-rail__totals, .summary-rail__validation, .summary-rail__environment-panel { padding-top: var(--space-4); border-top: 1px solid var(--divider); }
    .summary-rail__totals dl { display: flex; flex-direction: column; gap: var(--space-2); margin: var(--space-3) 0 0; }
    .summary-rail__totals dl div { display: flex; justify-content: space-between; color: var(--text-secondary); font-size: .84rem; }
    dt, dd { margin: 0; }
    dd { display: inline-flex; align-items: baseline; gap: 4px; color: var(--text-primary); font-variant-numeric: tabular-nums; }
    .is-discount { color: var(--state-success-fg); }
    .summary-rail__grand-total { margin-top: 5px; padding-top: 12px; border-top: 1px dashed var(--border-subtle); color: var(--text-primary) !important; font-size: .96rem !important; font-weight: 800; }
    .summary-rail__grand-total dd { color: var(--accent); font-size: 1.05rem; }
    .summary-rail__balance { padding-top: 8px; color: var(--text-primary) !important; font-weight: 750; }
    .summary-rail__balance dd { color: var(--state-warning-fg); }
    .summary-rail__loading { display: flex; flex-direction: column; gap: 12px; }
    .summary-rail__totals app-empty-state { display: block; transform: scale(.82); transform-origin: top center; margin-bottom: -34px; }
    .summary-rail__error { display: flex; align-items: flex-start; gap: 7px; margin-top: 12px; color: var(--state-danger-fg); font-size: .76rem; line-height: 1.35; }
    .summary-rail__cod { display: flex; align-items: center; gap: 7px; margin-top: 12px; padding: 8px 10px; border: 1px solid var(--state-info-border); border-radius: var(--radius-md); background: var(--state-info-bg); color: var(--state-info-fg); font-size: .76rem; font-weight: 700; line-height: 1.35; }
    .validation-pill { display: inline-flex; align-items: center; min-height: 22px; padding: 0 8px; border-radius: var(--radius-pill); background: var(--state-success-bg); color: var(--state-success-fg); font-size: .68rem; font-weight: 800; }
    .validation-pill.is-invalid { background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .summary-rail__hint, .summary-rail__environment-panel p { margin-top: 10px; color: var(--text-muted); font-size: .76rem; line-height: 1.4; }
    .summary-rail__issues { display: flex; flex-direction: column; margin: 12px 0 0; padding: 0; list-style: none; }
    .summary-rail__issues li { color: var(--state-danger-fg); font-size: .76rem; line-height: 1.35; }
    .summary-rail__issues button { padding: 0; border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; text-align: left; text-decoration: underline; text-decoration-thickness: 1px; text-underline-offset: 2px; }
    .summary-rail__issues button:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring-danger); }
    .summary-rail__issues .is-global { text-decoration: none; }
    .summary-rail__endpoint { overflow-wrap: anywhere; }
    .summary-rail__custom { color: var(--state-info-fg) !important; }
    .summary-rail__actions { display: grid; gap: 9px; }
    .summary-rail__actions ui-button ::ng-deep .ui-button { width: 100%; }
    .summary-rail__action-hint { color: var(--text-muted); font-size: .72rem; }
    .summary-action-bar { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); padding: var(--panel-padding-compact); border: 1px solid var(--border-strong); border-radius: var(--panel-radius); background: var(--surface-panel); box-shadow: var(--shadow-lg); }
    .summary-action-bar__status, .summary-action-bar__actions { display: flex; align-items: center; gap: var(--space-3); }
    .summary-action-bar__environment { flex: 0 0 auto; padding: 4px 7px; border-radius: var(--radius-pill); background: var(--state-success-bg); color: var(--state-success-fg); font-size: .66rem; font-weight: 850; letter-spacing: .04em; }
    .summary-action-bar__environment.is-production { background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .summary-action-bar__total { display: inline-flex; align-items: baseline; gap: 4px; color: var(--text-primary); font-size: .92rem; font-weight: 850; white-space: nowrap; }
    .summary-action-bar__balance, .summary-action-bar__muted { overflow: hidden; color: var(--text-secondary); font-size: .78rem; text-overflow: ellipsis; white-space: nowrap; }
    .summary-action-bar__validation { flex: 0 0 auto; color: var(--state-danger-fg); font-size: .74rem; font-weight: 750; }
    .summary-action-bar__cod { flex: 0 0 auto; padding: 3px 7px; border-radius: var(--radius-pill); background: var(--state-info-bg); color: var(--state-info-fg); font-size: .7rem; font-weight: 800; white-space: nowrap; }
    .summary-action-bar__actions { flex: 0 0 auto; }
    @media (max-width: 620px) { .summary-action-bar { align-items: stretch; flex-direction: column; } .summary-action-bar__actions ui-button { flex: 1; } .summary-action-bar__actions { width: 100%; } }
  `]
})
export class OrderSummaryRailComponent {
  @Input() totals: TotalsSummary | null = null;
  @Input() itemCount = 0;
  @Input() totalQuantity = 0;
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() validationSummary: OrderValidationSummary | null = null;
  @Input() validationIssues: OrderValidationIssue[] = [];
  @Input() environment: EnvironmentDto | null = null;
  @Input() endpoint: ModuleEndpoint | null = null;
  @Input() sending = false;
  @Input() compact = false;
  @Input() customEndpointEnabled = false;
  @Input() customEndpointValid = true;
  /** True when the draft carries no payment rows. That is a valid,
   * non-blocking state: FlatOrderPayloadBuilder emits the verified Cash on
   * Delivery shape for it (order_payment_method "COD",
   * order_payment_status "not_payment", empty payment list), so the rail
   * states the outcome rather than reporting a missing payment. */
  @Input() isCashOnDelivery = false;

  @Output() validate = new EventEmitter<void>();
  @Output() send = new EventEmitter<void>();
  @Output() issueSelected = new EventEmitter<OrderValidationIssue>();

  isProduction(): boolean { return this.environment?.environment === 'Production'; }

  environmentLabel(): string {
    return this.isProduction() ? 'PRODUCTION' : 'TESTING';
  }

  hasValidationIssues(): boolean {
    return this.knownIssueCount() > 0;
  }

  knownIssueCount(): number {
    return Math.max(this.validationSummary?.totalCount ?? 0, this.validationIssues.length);
  }

  validationStatusLabel(): string {
    if (!this.validationSummary) return 'Awaiting server review';
    return this.hasValidationIssues() ? `${this.knownIssueCount()} issue${this.knownIssueCount() === 1 ? '' : 's'}` : 'No issues recorded';
  }

  endpointSummary(): string {
    if (this.customEndpointEnabled) return this.customEndpointValid ? 'Custom endpoint selected' : 'Custom endpoint needs attention';
    if (this.endpoint?.apiUrl) return this.endpoint.apiUrl;
    return this.endpoint ? 'No endpoint configured' : 'Resolving endpoint…';
  }

  sendDisabled(): boolean {
    return this.sending || this.hasValidationIssues() || !this.environment || (!this.customEndpointEnabled && !this.endpoint?.apiUrl) || (this.customEndpointEnabled && !this.customEndpointValid);
  }

  sendDisabledReason(): string {
    if (this.sending) return 'An order request is already being sent.';
    if (this.hasValidationIssues()) return 'Resolve the known validation issues before sending.';
    if (!this.environment) return 'The active environment is still resolving.';
    if (this.customEndpointEnabled && !this.customEndpointValid) return 'Review the custom endpoint in the payload section.';
    if (!this.customEndpointEnabled && !this.endpoint?.apiUrl) return 'No send endpoint is configured for this environment.';
    return '';
  }

  onSend() {
    if (!this.sendDisabled()) this.send.emit();
  }
}
