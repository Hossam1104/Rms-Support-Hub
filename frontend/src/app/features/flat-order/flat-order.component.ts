import { AfterViewInit, Component, DestroyRef, ElementRef, OnDestroy, OnInit, QueryList, ViewChildren, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { FocusService } from '../../core/services/focus.service';
import { normalizeLocalPhone } from '../../core/utils/phone.util';
import { ApiError, BranchOption, LookupResult, ModuleEndpoint, OrderDraft, Product, Payment, Consumer, SendOrderResult, OrderRequestDetailResponse, TotalsSummary } from '../../core/models';
import { OrderInfoComponent } from './components/order-info.component';
import { ClientInfoComponent } from './components/client-info.component';
import { DeliveryInfoComponent } from './components/delivery-info.component';
import { ProductUpdate, ProductsTableComponent } from './components/products-table.component';
import { PaymentUpdate, PaymentsTableComponent } from './components/payments-table.component';
import { ApiConfigComponent } from './components/api-config.component';
import { OrderBuilderSection, OrderSectionNavigationComponent } from './components/order-section-navigation.component';
import { OrderSummaryRailComponent, OrderValidationIssue } from './components/order-summary-rail.component';
import { AddProductDialogComponent, ItemLookupOutcome } from './components/add-product-dialog.component';
import { AddPaymentDialogComponent } from './components/add-payment-dialog.component';
import { ConfirmDialogComponent, EnvBadgeComponent, PageHeaderComponent, SkeletonComponent, UiButtonComponent, UiSectionComponent } from '../../shared/ui';
import { DraftStore } from './draft.store';
import { MappedValidationErrors, mapSendValidationErrors } from './send-validation';

/** Debounce for the server totals refresh -- mirrors the U2 patch debounce
 * so bursts of mutations coalesce into one GET calculate-totals. */
const TOTALS_DEBOUNCE_MS = 300;

/**
 * Rebound (R10, remediation_plan.md B1/B5/B21) to the corrected schema
 * FlatOrderPayloadBuilder.cs actually reads -- see client-info.component.ts
 * and order-info.component.ts for the field-level detail. The Delivery
 * card and the compiled-JSON preview are both driven by real server data
 * (Capabilities.HasDeliveryFields and GET .../export-json respectively)
 * instead of a hand-rolled approximation or a module-key string comparison.
 *
 * U4 (UI_Rework_Plan.md D2/D8/D13): the item lookup result now populates
 * the Add Product dialog via ItemLookupOutcome instead of being discarded;
 * the summary is the server's typed TotalsSummary (GET calculate-totals)
 * with no client money math; send loading is the real request lifecycle;
 * and send validation failures map to inline field errors through
 * send-validation.ts.
 */
@Component({
  selector: 'app-flat-order',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    EnvBadgeComponent,
    PageHeaderComponent,
    SkeletonComponent,
    UiButtonComponent,
    UiSectionComponent,
    OrderInfoComponent,
    ClientInfoComponent,
    DeliveryInfoComponent,
    ProductsTableComponent,
    PaymentsTableComponent,
    ApiConfigComponent,
    OrderSectionNavigationComponent,
    OrderSummaryRailComponent,
    AddProductDialogComponent,
    AddPaymentDialogComponent,
    ConfirmDialogComponent
  ],
  providers: [DraftStore],
  template: `
    <div class="flat-order-container">
      <app-page-header
        [title]="moduleLabel()"
        [compact]="true"
        subtitle="Prepare the draft, review server totals, and send through the active environment.">
        <div class="builder-header-status">
          <span class="workflow-status" [class.is-busy]="sending() || totalsLoading() || draftStore.saving()">
            <span class="workflow-status__dot" aria-hidden="true"></span>{{ workflowStatusLabel() }}
          </span>
          <app-env-badge
            [environment]="moduleService.activeEnvironment()"
            [options]="moduleService.activeModule()?.environments ?? []"
            (select)="moduleService.selectEnvironment($event)">
          </app-env-badge>
        </div>
      </app-page-header>

      <ng-container *ngIf="draftLoading(); else loadedBuilder">
        <div class="builder-skeleton" aria-label="Loading order builder" data-testid="builder-skeleton">
          <div class="builder-skeleton__main">
            <app-skeleton width="100%" height="48px"></app-skeleton>
            <app-skeleton width="100%" height="178px"></app-skeleton>
            <app-skeleton width="100%" height="214px"></app-skeleton>
            <app-skeleton width="100%" height="188px"></app-skeleton>
          </div>
          <div class="builder-skeleton__rail">
            <app-skeleton width="100%" height="420px"></app-skeleton>
          </div>
        </div>
      </ng-container>

      <ng-template #loadedBuilder>
        <ng-container *ngIf="draftLoadError(); else builderWorkspace">
          <section class="builder-load-error" role="alert" data-testid="builder-load-error">
            <i class="bi bi-cloud-slash" aria-hidden="true"></i>
            <div>
              <h2>Draft unavailable</h2>
              <p>{{ draftLoadError() }}</p>
            </div>
            <ui-button variant="secondary" icon="bi bi-arrow-clockwise" (pressed)="loadState()">Retry draft</ui-button>
          </section>
        </ng-container>
      </ng-template>

      <ng-template #builderWorkspace>
        <div class="builder-grid">
          <section class="workflow-column" aria-label="Order workflow">
            <app-order-section-navigation
              [sections]="sections()"
              [activeSectionId]="activeSectionId()"
              (sectionSelected)="scrollToSection($event)">
            </app-order-section-navigation>

            <div class="workflow-sections">
              <ui-section
                id="order-header-section"
                title="Order header"
                [compact]="true"
                description="Branch, order identity, status, notes, and coordinates."
                [completed]="section('order-header').completed"
                [hasIssues]="section('order-header').hasIssues"
                [issueCount]="section('order-header').issueCount">
                <app-order-info
                  [orderData]="draftStore.draft().orderData"
                  [moduleKey]="moduleKey()"
                  [branches]="branches()"
                  [branchesLoading]="branchesLoading()"
                  [branchError]="branchError()"
                  [fieldErrors]="fieldErrors()"
                  (branchRefresh)="loadBranches(true)"
                  (fieldChange)="onFieldChange($event)">
                </app-order-info>
              </ui-section>

              <ui-section
                id="customer-section"
                title="Customer"
                [compact]="true"
                description="Consumer lookup and the payload’s customer/address fields."
                [completed]="section('customer').completed"
                [hasIssues]="section('customer').hasIssues"
                [issueCount]="section('customer').issueCount">
                <app-client-info
                  [orderData]="draftStore.draft().orderData"
                  [fieldErrors]="fieldErrors()"
                  (fieldChange)="onFieldChange($event)"
                  (lookupConsumer)="onLookupConsumer($event)">
                </app-client-info>
              </ui-section>

              <ui-section
                *ngIf="hasDeliveryFields()"
                id="delivery-section"
                title="Delivery"
                [compact]="true"
                description="Schedule and fulfillment details exposed by the active module."
                [completed]="section('delivery').completed"
                [hasIssues]="section('delivery').hasIssues"
                [issueCount]="section('delivery').issueCount">
                <app-delivery-info
                  [orderData]="draftStore.draft().orderData"
                  [fieldErrors]="fieldErrors()"
                  (fieldChange)="onFieldChange($event)">
                </app-delivery-info>
              </ui-section>

              <ui-section
                id="products-section"
                title="Products"
                [compact]="true"
                description="Line items, quantities, discounts, and row totals."
                [completed]="section('products').completed"
                [hasIssues]="section('products').hasIssues"
                [issueCount]="section('products').issueCount">
                <div uiSectionActions class="section-actions">
                  <span class="section-actions__count">{{ draftStore.draft().products.length }} {{ draftStore.draft().products.length === 1 ? 'item' : 'items' }}</span>
                  <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddProductDialog()">Add product</ui-button>
                </div>
                <app-products-table
                  [products]="draftStore.draft().products"
                  [errors]="fieldErrors()['products'] ?? []"
                  (openAddDialog)="openAddProductDialog()"
                  (deleteProduct)="onDeleteProduct($event)"
                  (updateProduct)="onUpdateProduct($event)">
                </app-products-table>
              </ui-section>

              <ui-section
                id="payments-section"
                title="Payments"
                [compact]="true"
                description="Methods, statuses, references, and settled amounts."
                [completed]="section('payments').completed"
                [hasIssues]="section('payments').hasIssues"
                [issueCount]="section('payments').issueCount">
                <div uiSectionActions class="section-actions">
                  <span class="section-actions__count">{{ draftStore.draft().payments.length }} {{ draftStore.draft().payments.length === 1 ? 'payment' : 'payments' }}</span>
                  <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="showAddPaymentDialog.set(true)">Add payment</ui-button>
                </div>
                <app-payments-table
                  [payments]="draftStore.draft().payments"
                  [errors]="fieldErrors()['payments'] ?? []"
                  (openAddDialog)="showAddPaymentDialog.set(true)"
                  (deletePayment)="onDeletePayment($event)"
                  (updatePayment)="onUpdatePayment($event)">
                </app-payments-table>
              </ui-section>

              <ui-section
                id="payload-section"
                title="Payload preview & send"
                [compact]="true"
                description="Server-compiled JSON, resolved endpoint, and response diagnostics."
                [completed]="section('payload').completed"
                [hasIssues]="section('payload').hasIssues"
                [issueCount]="section('payload').issueCount">
                <app-api-config
                  [embedded]="true"
                  [showSend]="false"
                  [compiledJson]="compiledJson()"
                  [apiResponse]="apiResponse()"
                  [loading]="sending()"
                  [endpoint]="activeEndpoint()"
                  [environment]="moduleService.activeEnvironment()"
                  [validationSummary]="validationSummary()"
                  (customEndpointChange)="onCustomEndpointChange($event)">
                </app-api-config>
              </ui-section>

              <div class="global-validation" *ngIf="validationSummary()?.globalErrors?.length" role="alert">
                <div class="global-validation__heading"><i class="bi bi-exclamation-octagon" aria-hidden="true"></i><strong>Server validation needs review</strong></div>
                <p *ngFor="let message of validationSummary()!.globalErrors">{{ message }}</p>
              </div>

              <div class="landed-card" *ngIf="landedRequestId() as id">
                <i class="bi bi-check-circle-fill" aria-hidden="true"></i>
                <span>Order recorded as request #{{ id }} in the OrderRequests table.</span>
                <a [routerLink]="['/tools/online-orders/modules', moduleKey(), 'order-requests', id]" class="landed-link">
                  Open in Order Requests <i class="bi bi-arrow-right" aria-hidden="true"></i>
                </a>
              </div>
            </div>
          </section>

          <aside class="summary-column">
            <app-order-summary-rail
              [totals]="totals()"
              [itemCount]="draftStore.draft().products.length"
              [totalQuantity]="totalQuantity()"
              [loading]="totalsLoading()"
              [error]="totalsError()"
              [validationSummary]="validationSummary()"
              [validationIssues]="validationIssues()"
              [environment]="moduleService.activeEnvironment()"
              [endpoint]="activeEndpoint()"
              [sending]="sending()"
              [isCashOnDelivery]="isCashOnDelivery()"
              [customEndpointEnabled]="customEndpointEnabled()"
              [customEndpointValid]="customEndpointValid()"
              (validate)="onValidate()"
              (send)="onSummarySend()"
              (issueSelected)="onValidationIssue($event)">
            </app-order-summary-rail>
          </aside>
        </div>

        <div class="mobile-summary">
          <app-order-summary-rail
            [compact]="true"
            [totals]="totals()"
            [itemCount]="draftStore.draft().products.length"
            [totalQuantity]="totalQuantity()"
            [loading]="totalsLoading()"
            [error]="totalsError()"
            [validationSummary]="validationSummary()"
            [validationIssues]="validationIssues()"
            [environment]="moduleService.activeEnvironment()"
            [endpoint]="activeEndpoint()"
            [sending]="sending()"
            [isCashOnDelivery]="isCashOnDelivery()"
            [customEndpointEnabled]="customEndpointEnabled()"
            [customEndpointValid]="customEndpointValid()"
            (validate)="onValidate()"
            (send)="onSummarySend()"
            (issueSelected)="onValidationIssue($event)">
          </app-order-summary-rail>
        </div>
      </ng-template>
    </div>

    <app-confirm-dialog
      *ngIf="showProdSendConfirm()"
      variant="danger"
      title="Send to Production?"
      [message]="'This will send a real order to the live RMS API on ' + (moduleService.activeEnvironment()?.key) + '.'"
      [requireReason]="true"
      [requiredTypedValue]="moduleService.activeEnvironment()?.key || ''"
      reasonLabel="Type the environment name to confirm"
      [reasonPlaceholder]="moduleService.activeEnvironment()?.key || ''"
      confirmLabel="Send to Production"
      (cancel)="showProdSendConfirm.set(false); pendingSendEvent = null"
      (confirm)="onConfirmProdSend()">
    </app-confirm-dialog>

    <app-add-product-dialog
      *ngIf="showAddProductDialog()"
      [moduleKey]="moduleKey()"
      [branchCode]="branchCode()"
      [branchName]="selectedBranchName()"
      [lookupOutcome]="itemLookupOutcome()"
      (close)="showAddProductDialog.set(false)"
      (add)="onAddProduct($event)"
      (lookupItem)="onLookupItem($event)">
    </app-add-product-dialog>

    <app-add-payment-dialog
      *ngIf="showAddPaymentDialog()"
      [moduleKey]="moduleKey()"
      [requiredAmount]="paymentRequiredAmount()"
      (close)="showAddPaymentDialog.set(false)"
      (add)="onAddPayment($event)">
    </app-add-payment-dialog>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .flat-order-container {
      min-width: 0;
      max-width: 1680px;
      margin: 0 auto;
      --section-gap: clamp(12px, 1.25vw, 16px);
      --panel-gap: clamp(8px, .95vw, 12px);
      --panel-padding: clamp(12px, 1.15vw, 16px);
      --panel-padding-compact: clamp(8px, .8vw, 11px);
      --form-gap: clamp(8px, .85vw, 12px);
    }
    .builder-header-status { display: flex; align-items: center; justify-content: flex-end; gap: var(--panel-gap); }
    .workflow-status { display: inline-flex; align-items: center; gap: 7px; min-height: 30px; padding: 0 10px; border: 1px solid var(--border-subtle); border-radius: var(--radius-pill); background: var(--surface-panel); color: var(--text-secondary); font-size: .74rem; font-weight: 750; white-space: nowrap; }
    .workflow-status__dot { width: 7px; height: 7px; border-radius: 50%; background: var(--state-success-fg); box-shadow: 0 0 0 4px var(--state-success-bg); }
    .workflow-status.is-busy .workflow-status__dot { background: var(--accent); box-shadow: 0 0 0 4px var(--accent-soft); animation: builderPulse 1.4s ease-in-out infinite; }
    .builder-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(320px, 340px); align-items: start; gap: var(--section-gap); }
    .workflow-column, .summary-column { min-width: 0; }
    app-order-section-navigation { position: sticky; top: 16px; z-index: 8; margin-bottom: var(--panel-gap); }
    .workflow-sections { display: flex; flex-direction: column; gap: var(--section-gap); }
    ui-section { scroll-margin-top: 88px; }
    .section-actions { display: inline-flex; align-items: center; gap: 8px; }
    .section-actions__count { display: inline-flex; align-items: center; height: 22px; padding-inline: 9px; border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: .7rem; font-weight: var(--weight-bold); white-space: nowrap; }
    .summary-column { position: sticky; top: 16px; }
    .mobile-summary { display: none; }
    .builder-skeleton { display: grid; grid-template-columns: minmax(0, 1fr) minmax(320px, 340px); gap: var(--section-gap); }
    .builder-skeleton__main, .builder-skeleton__rail { display: flex; flex-direction: column; gap: var(--section-gap); }
    .builder-skeleton app-skeleton { display: block; min-height: 48px; }
    .builder-load-error { display: flex; align-items: center; gap: var(--panel-gap); padding: var(--panel-padding); border: 1px solid var(--state-danger-border); border-radius: var(--radius-xl); background: var(--state-danger-bg); color: var(--text-primary); }
    .builder-load-error > i { color: var(--state-danger-fg); font-size: 1.6rem; }
    .builder-load-error h2 { margin: 0 0 4px; font-size: 1.05rem; }
    .builder-load-error p { margin: 0; color: var(--text-secondary); font-size: .84rem; }
    .builder-load-error ui-button { margin-left: auto; }
    .global-validation { padding: var(--panel-padding-compact) var(--panel-padding); border: 1px solid var(--state-danger-border); border-radius: var(--panel-radius); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .global-validation__heading { display: flex; align-items: center; gap: 8px; font-size: .86rem; }
    .global-validation p { margin: 8px 0 0 24px; color: var(--text-primary); font-size: .8rem; }
    .landed-card {
      display: flex; align-items: center; gap: var(--panel-gap); padding: var(--panel-padding-compact) var(--panel-padding);
      border: 1px solid var(--state-success-border); border-radius: var(--radius-lg);
      background: var(--state-success-bg); color: var(--state-success-fg); font-weight: 600;
      animation: fadeInUp var(--d-slow) var(--ease-spring);
    }
    .landed-card i.bi-check-circle-fill { font-size: 1.3rem; }
    .landed-link { margin-left: auto; display: flex; align-items: center; gap: 6px; color: var(--state-success-fg); text-decoration: underline; font-weight: 700; white-space: nowrap; }
    @keyframes builderPulse { 50% { opacity: .45; transform: scale(.8); } }
    @media (max-width: 1199px) {
      .builder-grid, .builder-skeleton { display: block; }
      .summary-column, .builder-skeleton__rail { display: none; }
      .mobile-summary { position: sticky; bottom: 14px; z-index: 20; display: block; margin-top: var(--section-gap); padding-bottom: env(safe-area-inset-bottom, 0px); }
      app-order-section-navigation { top: 12px; }
    }
    @media (max-width: 767px) {
      .builder-header-status { align-items: flex-start; flex-direction: column; gap: 8px; }
      .workflow-status { width: 100%; justify-content: center; }
      .builder-load-error { align-items: flex-start; flex-wrap: wrap; padding: 20px; }
      .builder-load-error ui-button { width: 100%; margin-left: 0; }
      .landed-card { align-items: flex-start; flex-wrap: wrap; }
      .landed-link { flex-basis: 100%; margin-left: 0; }
    }
    @media (prefers-reduced-motion: reduce) { .workflow-status__dot, .landed-card { animation: none; } }
  `]
})
export class FlatOrderComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChildren(UiSectionComponent, { read: ElementRef }) private sectionElementRefs!: QueryList<ElementRef<HTMLElement>>;
  @ViewChildren(UiSectionComponent) private sectionComponents!: QueryList<UiSectionComponent>;

  private api = inject(ApiService);
  private branchOptions = inject(BranchOptionsService);
  moduleService = inject(ModuleService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);
  private focus = inject(FocusService);
  private readonly destroyRef = inject(DestroyRef);
  draftStore = inject(DraftStore);

  moduleKey = signal<string>('ghc_ecommerce');
  draftLoading = signal(true);
  draftLoadError = signal<string | null>(null);
  activeSectionId = signal('order-header-section');
  /** U4 (D8): the server-owned summary (GET calculate-totals). null = not
   * loaded yet -- never rendered as fabricated zeros. */
  totals = signal<TotalsSummary | null>(null);
  totalsLoading = signal(false);
  totalsError = signal<string | null>(null);
  compiledJson = signal<Record<string, unknown> | null>(null);
  apiResponse = signal<SendOrderResult | null>(null);
  landedRequestId = signal<number | null>(null);
  branches = signal<BranchOption[]>([]);
  branchesLoading = signal(false);
  branchError = signal<string | null>(null);
  /** U4 (D13): the active environment's resolved send endpoint, displayed
   * read-only in the API configuration area. */
  activeEndpoint = signal<ModuleEndpoint | null>(null);
  /** U4 (D13): real send-request lifecycle state -- set before POST
   * send-request, cleared in finalize; also blocks duplicate sends. */
  sending = signal(false);
  /** U4 (D2): the last item lookup outcome, pushed into the open Add
   * Product dialog so the result populates the form. */
  itemLookupOutcome = signal<ItemLookupOutcome | null>(null);
  /** U4: mapped server send-validation errors (see send-validation.ts). */
  private validation = signal<MappedValidationErrors | null>(null);
  fieldErrors = computed(() => this.validation()?.fieldErrors ?? {});
  validationIssues = computed<OrderValidationIssue[]>(() => this.validation()?.issues.map(issue => ({
    key: issue.key,
    message: issue.message,
    targetId: issue.targetId
  })) ?? []);
  validationSummary = computed(() => {
    const v = this.validation();
    return v ? { totalCount: v.totalCount, globalErrors: v.globalErrors } : null;
  });

  showAddProductDialog = signal<boolean>(false);
  showAddPaymentDialog = signal<boolean>(false);

  showProdSendConfirm = signal<boolean>(false);
  pendingSendEvent: { url: string } | null = null;
  customEndpointEnabled = signal(false);
  customEndpointUrl = signal('');
  customEndpointValid = signal(true);

  /** undefined until the first environment is known, so the effect below
   * never re-fetches on the initial load -- only on an actual switch. */
  private lastEnvKey: string | undefined = undefined;
  private branchLoadToken = 0;
  private endpointLoadToken = 0;
  private totalsToken = 0;
  private totalsDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  private draftLoadToken = 0;
  private sectionObserver: IntersectionObserver | null = null;
  private sectionQuerySubscription: Subscription | null = null;

  constructor() {
    // U1 (UI_Rework_Plan.md D4 step 5): switching environments must clear
    // any environment-scoped cached state and re-fetch the draft, not keep
    // showing whatever was loaded under the previous lane.
    effect(() => {
      const envKey = this.moduleService.activeEnvironment()?.key;
      if (envKey === undefined) return;

      if (this.lastEnvKey !== undefined && this.lastEnvKey !== envKey && this.moduleKey()) {
        this.loadState();
      }
      this.lastEnvKey = envKey;
    });

    // U4 (D8): totals are server-owned, and the server totals are computed
    // from the server draft -- so the refresh must run only after the U2
    // PATCH order-data flush has settled, never in parallel with it (a GET
    // fired mid-flush would read the pre-patch draft and display stale
    // totals with no later correction).
    effect(() => {
      this.draftStore.flushVersion();
      this.scheduleTotalsRefresh();
    });
  }

  ngOnInit() {
    this.route.parent?.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const key = params.get('key') || 'ghc_ecommerce';
        this.moduleKey.set(key);
        this.loadState();
      });
  }

  hasDeliveryFields(): boolean {
    return this.moduleService.activeModule()?.capabilities?.hasDeliveryFields ?? false;
  }

  moduleLabel(): string {
    return this.moduleService.activeModule()?.label || 'Online order builder';
  }

  workflowStatusLabel(): string {
    if (this.draftLoading()) return 'Loading draft';
    if (this.sending()) return 'Sending order';
    if (Object.keys(this.fieldErrors()).length > 0) return 'Needs attention';
    if (this.draftStore.saving()) return 'Saving changes';
    if (this.totalsLoading()) return 'Refreshing totals';
    return 'Draft ready';
  }

  /** Section facts are deliberately lightweight: completion reflects the
   * current draft and the existing mapped server errors, not a second client
   * validation engine. */
  sections = computed<OrderBuilderSection[]>(() => {
    const draft = this.draftStore.draft();
    const errors = this.fieldErrors();
    const orderData = draft.orderData;
    const issueCount = (keys: string[]) => keys.reduce((count, key) => count + (errors[key]?.length ?? 0), 0);
    const orderHeaderIssues = issueCount(['branch_code', 'order_code', 'parent_order_code', 'order_delivery_cost', 'order_status', 'order_notes', 'order_gps']);
    const customerIssues = issueCount(['client_country_code', 'client_phone', 'client_first_name', 'client_middle_name', 'client_last_name', 'client_email', 'client_birthdate', 'client_gender', 'order_address', 'address_code']);
    const deliveryIssues = issueCount(['delivery_date', 'delivery_from_time', 'delivery_to_time', 'fullfilment_plant', 'shipping_address_2']);
    const productIssues = errors['products']?.length ?? 0;
    const paymentIssues = errors['payments']?.length ?? 0;
    const payloadIssues = this.validation()?.globalErrors.length ?? 0;
    const deliveryAvailable = this.hasDeliveryFields();

    return [
      {
        id: 'order-header-section', label: 'Order header', description: 'Branch and order identity',
        completed: Boolean(String(orderData['branch_code'] ?? '').trim() && String(orderData['order_code'] ?? '').trim()),
        hasIssues: orderHeaderIssues > 0, issueCount: orderHeaderIssues
      },
      {
        id: 'customer-section', label: 'Customer', description: 'Consumer and address',
        completed: Boolean(String(orderData['client_phone'] ?? '').trim() && String(orderData['client_first_name'] ?? '').trim() && String(orderData['order_address'] ?? '').trim()),
        hasIssues: customerIssues > 0, issueCount: customerIssues
      },
      {
        id: 'delivery-section', label: 'Delivery', description: 'Schedule and fulfillment',
        completed: !deliveryAvailable || deliveryIssues === 0, hasIssues: deliveryIssues > 0, issueCount: deliveryIssues
      },
      {
        id: 'products-section', label: 'Products', description: 'Items and row edits',
        completed: draft.products.length > 0 && productIssues === 0, hasIssues: productIssues > 0, issueCount: productIssues
      },
      {
        id: 'payments-section', label: 'Payments', description: 'Settlement methods',
        completed: draft.payments.length > 0 && paymentIssues === 0, hasIssues: paymentIssues > 0, issueCount: paymentIssues
      },
      {
        id: 'payload-section', label: 'Payload & send', description: 'Preview and destination',
        completed: this.compiledJson() !== null && payloadIssues === 0, hasIssues: payloadIssues > 0, issueCount: payloadIssues
      }
    ].filter(section => section.id !== 'delivery-section' || deliveryAvailable);
  });

  section(id: string): OrderBuilderSection {
    return this.sections().find(section => section.id === `${id}-section`) ?? {
      id: `${id}-section`, label: id, description: '', completed: false, hasIssues: false, issueCount: 0
    };
  }

  branchCode(): string {
    const v = this.draftStore.draft().orderData['branch_code'];
    return v == null ? '' : String(v);
  }

  selectedBranchName(): string {
    const branch = this.branches().find(option => option.code === this.branchCode());
    return branch ? `${branch.name} (${branch.code})` : '';
  }

  /** Item/quantity counts for the summary header -- counts, not money math. */
  totalQuantity(): number {
    return this.draftStore.draft().products.reduce((sum, p) => sum + (Number(p.quantity) || 0), 0);
  }

  /** An empty payment list is the verified Cash on Delivery state, not a
   * missing-payment error -- FlatOrderValidator accepts it and
   * FlatOrderPayloadBuilder emits the COD shape for it. Surfacing it here
   * keeps the operator informed without fabricating a zero-value payment. */
  isCashOnDelivery(): boolean {
    return this.draftStore.draft().payments.length === 0;
  }

  /** The server-owned balance is the amount a newly selected Visa payment
   * should cover; no client-side total is fabricated when totals are pending. */
  paymentRequiredAmount(): number {
    return this.totals()?.remainingBalance ?? 0;
  }

  loadBranches(refresh = false) {
    const key = this.moduleKey();
    if (!key) return;

    const token = ++this.branchLoadToken;
    this.branchesLoading.set(true);
    this.branchError.set(null);
    this.branchOptions.list(key, this.moduleService.activeEnvironment()?.key, refresh).subscribe({
      next: response => {
        if (token !== this.branchLoadToken) return;
        this.branchesLoading.set(false);
        if (response.success) {
          this.branches.set(response.data || []);
        } else {
          this.branchError.set(response.message || 'Branches could not be loaded.');
        }
      },
      error: () => {
        if (token !== this.branchLoadToken) return;
        this.branchesLoading.set(false);
        this.branchError.set('Branches could not be loaded.');
      }
    });
  }

  loadState() {
    const key = this.moduleKey();
    if (!key) return;

    const token = ++this.draftLoadToken;
    this.draftLoading.set(true);
    this.draftLoadError.set(null);
    this.validation.set(null);
    this.customEndpointEnabled.set(false);
    this.customEndpointUrl.set('');
    this.customEndpointValid.set(true);
    this.activeEndpoint.set(null);
    this.totals.set(null);
    this.totalsError.set(null);
    this.compiledJson.set(null);
    this.apiResponse.set(null);
    this.landedRequestId.set(null);
    this.draftStore.setModuleKey(key);
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.loadBranches();
    this.loadEndpoint();
    this.api.get<OrderDraft>(`modules/${key}/state`, { envKey }).subscribe({
      next: state => {
        if (token !== this.draftLoadToken) return;
        if (state) this.draftStore.setDraft(state);
        this.draftLoading.set(false);
        this.refreshTotals();
        this.refreshCompiledJson();
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => {
        if (token !== this.draftLoadToken) return;
        this.draftLoading.set(false);
        this.draftLoadError.set('The saved draft could not be loaded. Retry to fetch the current order state.');
        this.refreshTotals();
        this.refreshCompiledJson();
      }
    });
  }

  /** U4 (D13): resolves the endpoint the send would target for the active
   * environment (same GetEnvironment resolution as send-request). */
  private loadEndpoint() {
    const key = this.moduleKey();
    if (!key) return;

    const token = ++this.endpointLoadToken;
    this.api.get<ModuleEndpoint>(`modules/${key}/endpoint`, {
      envKey: this.moduleService.activeEnvironment()?.key
    }).subscribe({
      next: endpoint => {
        if (token !== this.endpointLoadToken) return;
        this.activeEndpoint.set(endpoint);
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => {
        if (token !== this.endpointLoadToken) return;
        this.activeEndpoint.set(null);
      }
    });
  }

  /** Applies immediately to local state and debounces the persisted write
   * via DraftStore.patch (U2, UI_Rework_Plan.md D1) -- see draft.store.ts.
   * The totals refresh follows automatically once that PATCH settles. */
  onFieldChange(event: { fieldName: string, value: unknown }) {
    this.draftStore.patch({ [event.fieldName]: event.value });
    this.clearFieldError(event.fieldName);
    this.refreshCompiledJson();
  }

  /** U4: a corrected field sheds its inline server-validation error as soon
   * as the operator edits it; the section/summary counts follow. */
  private clearFieldError(fieldName: string) {
    const current = this.validation();
    if (!current || !(fieldName in current.fieldErrors)) return;

    const cleared = current.fieldErrors[fieldName].length;
    const fieldErrors = { ...current.fieldErrors };
    delete fieldErrors[fieldName];

    const totalCount = current.totalCount - cleared;
    if (totalCount <= 0 && current.globalErrors.length === 0) {
      this.validation.set(null);
    } else {
      this.validation.set({
        ...current,
        fieldErrors,
        totalCount,
        issues: current.issues.filter(issue => issue.key !== fieldName)
      });
    }
  }

  private clearSectionError(section: 'products' | 'payments') {
    this.clearFieldError(section);
  }

  onAddProduct(product: Product) {
    this.draftStore.updateLocal(d => ({ ...d, products: [...d.products, product] }));
    this.refreshCompiledJson();
    this.showAddProductDialog.set(false);
    this.clearSectionError('products');
    this.toast.showSuccess('Product added successfully.');

    const key = this.moduleKey();
    this.api.post<{ success: boolean, products: Product[], totals: TotalsSummary }>(`modules/${key}/products`, product).subscribe({
      next: res => this.applyServerTotals(res.totals),
      error: () => { }
    });
  }

  onDeleteProduct(index: number) {
    this.draftStore.updateLocal(d => ({ ...d, products: d.products.filter((_, i) => i !== index) }));
    this.refreshCompiledJson();
    this.toast.showInfo('Product removed.');

    const key = this.moduleKey();
    this.api.delete<{ success: boolean, products: Product[], totals: TotalsSummary }>(`modules/${key}/products/${index}`).subscribe({
      next: res => this.applyServerTotals(res.totals),
      error: () => { }
    });
  }

  /** Table editors commit on change/blur rather than on every raw keypress.
   * The local draft stays responsive while the existing dedicated endpoint
   * persists the complete row and returns authoritative totals. */
  onUpdateProduct(event: ProductUpdate) {
    const current = this.draftStore.draft().products[event.index];
    if (!current) return;

    const product = { ...current, ...event.patch };
    this.draftStore.updateLocal(draft => ({
      ...draft,
      products: draft.products.map((row, index) => index === event.index ? product : row)
    }));
    this.clearSectionError('products');
    this.refreshCompiledJson();

    const key = this.moduleKey();
    this.api.put<{ success: boolean, products: Product[], totals: TotalsSummary }>(`modules/${key}/products/${event.index}`, product).subscribe({
      next: response => {
        if (response.products) this.draftStore.updateLocal(draft => ({ ...draft, products: response.products }));
        if (response.totals) this.applyServerTotals(response.totals);
      },
      error: () => this.toast.showError('Product changes could not be saved.')
    });
  }

  onAddPayment(payment: Payment) {
    this.draftStore.updateLocal(d => ({ ...d, payments: [...d.payments, payment] }));
    this.refreshCompiledJson();
    this.showAddPaymentDialog.set(false);
    this.clearSectionError('payments');
    this.toast.showSuccess('Payment method added.');

    const key = this.moduleKey();
    this.api.post<{ success: boolean, payments: Payment[], totals: TotalsSummary }>(`modules/${key}/payments`, payment).subscribe({
      next: res => this.applyServerTotals(res.totals),
      error: () => { }
    });
  }

  onDeletePayment(index: number) {
    this.draftStore.updateLocal(d => ({ ...d, payments: d.payments.filter((_, i) => i !== index) }));
    this.refreshCompiledJson();
    this.toast.showInfo('Payment method removed.');

    const key = this.moduleKey();
    this.api.delete<{ success: boolean, payments: Payment[], totals: TotalsSummary }>(`modules/${key}/payments/${index}`).subscribe({
      next: res => this.applyServerTotals(res.totals),
      error: () => { }
    });
  }

  onUpdatePayment(event: PaymentUpdate) {
    const current = this.draftStore.draft().payments[event.index];
    if (!current) return;

    const payment = { ...current, ...event.patch };
    this.draftStore.updateLocal(draft => ({
      ...draft,
      payments: draft.payments.map((row, index) => index === event.index ? payment : row)
    }));
    this.clearSectionError('payments');
    this.refreshCompiledJson();

    const key = this.moduleKey();
    this.api.put<{ success: boolean, payments: Payment[], totals: TotalsSummary }>(`modules/${key}/payments/${event.index}`, payment).subscribe({
      next: response => {
        if (response.payments) this.draftStore.updateLocal(draft => ({ ...draft, payments: response.payments }));
        if (response.totals) this.applyServerTotals(response.totals);
      },
      error: () => this.toast.showError('Payment changes could not be saved.')
    });
  }

  /** U4 (D2): the lookup result is pushed back into the dialog as a typed
   * outcome so it populates the form. A missing branch blocks the lookup
   * with a picker-directed message (defense in depth -- the dialog also
   * disables the search controls without a branch). Not-found (200,
   * success:false) and infrastructure failure (HTTP error) stay distinct:
   * only the former reports "not found". */
  onLookupItem(event: { code: string, branchCode: string }) {
    const branchCode = event.branchCode.trim();
    if (!branchCode) {
      this.itemLookupOutcome.set({ status: 'missing-branch' });
      return;
    }

    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<LookupResult<Product>>(`modules/${key}/lookup/item`, { code: event.code, branchCode, envKey }).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.itemLookupOutcome.set({ status: 'found', product: res.data });
          this.toast.showSuccess(`Found item: ${res.data.itemName}`);
        } else {
          this.itemLookupOutcome.set({ status: 'not-found', code: event.code });
        }
      },
      // A genuine HTTP failure (DB unreachable, etc.) is already toasted by
      // errorEnvelopeInterceptor -- the dialog shows a neutral failure note
      // via the outcome and keeps the operator's entered values.
      error: () => this.itemLookupOutcome.set({ status: 'error' })
    });
  }

  /** Prefills name AND address from the lookup (R10 step 2) -- order_address/
   * address_code come from Consumer.Address/AddressCode, resolved server-side
   * from LoyaltyConsumerAddresses (UPC) with a preference for the row
   * flagged IsMaster -- see UpcConsumerRepository.
   *
   * U2 (UI_Rework_Plan.md D1): every prefilled field is sent as ONE
   * DraftStore.patch call instead of the previous nine sequential
   * per-field calls -- that was the exact race in the reported screenshot,
   * where late responses built from stale reads clobbered fields a later
   * request had already written. The toast now separately names which
   * fields the lookup actually returned and which came back empty, so an
   * empty Last Name reads as the data's fault, not a tool bug. */
  onLookupConsumer(phone: string) {
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<LookupResult<Consumer>>(`modules/${key}/lookup/consumer`, { phone, envKey }).subscribe({
      next: res => {
        const c = res.data;
        if (res.success && c) {
          // First/middle/last/phone always reflect the lookup result (even
          // blank), matching pre-U2 behaviour; the rest only overwrite the
          // draft when the lookup actually returned a value.
          const alwaysApplied: [string, string, unknown][] = [
            ['First Name', 'client_first_name', c.firstName || ''],
            ['Middle Name', 'client_middle_name', c.middleName || ''],
            ['Last Name', 'client_last_name', c.lastName || ''],
            // The lookup row may carry the country code inline; the draft
            // keeps it in client_country_code, so only the local part lands
            // in the number field.
            ['Phone', 'client_phone', normalizeLocalPhone(c.primaryPhoneNumber || phone)]
          ];
          const conditional: [string, string, unknown][] = [
            ['Email', 'client_email', c.email],
            ['Birthdate', 'client_birthdate', c.birthDate],
            ['Gender', 'client_gender', c.gender],
            ['Address', 'order_address', c.address],
            ['Address Code', 'address_code', c.addressCode]
          ];

          const fields: Record<string, unknown> = {};
          const prefilled: string[] = [];
          const empty: string[] = [];

          for (const [label, fieldName, value] of alwaysApplied) {
            fields[fieldName] = value;
            (value ? prefilled : empty).push(label);
          }
          for (const [label, fieldName, value] of conditional) {
            if (value) {
              fields[fieldName] = value;
              prefilled.push(label);
            } else {
              empty.push(label);
            }
          }

          this.draftStore.patch(fields);
          this.refreshCompiledJson();

          const name = `${c.firstName || ''} ${c.lastName || ''}`.trim();
          const message = empty.length === 0
            ? `Found consumer: ${name}. All fields prefilled.`
            : `Found consumer: ${name}. Prefilled: ${prefilled.join(', ')}. Empty from lookup: ${empty.join(', ')}.`;
          this.toast.showSuccess(message);
        } else {
          this.toast.showInfo(`No consumer record found for phone ${phone}.`);
        }
      },
      error: () => { }
    });
  }

  /** Gates the send behind a typed confirmation when the active lane is
   * Production (U1, UI_Rework_Plan.md §3 decision 4) -- Testing sends
   * proceed immediately. */
  onCustomEndpointChange(state: { enabled: boolean, url: string, valid: boolean }) {
    this.customEndpointEnabled.set(state.enabled);
    this.customEndpointUrl.set(state.url);
    this.customEndpointValid.set(state.valid);
  }

  /** There is intentionally no standalone validate endpoint in the current
   * API contract. This action flushes pending draft edits, refreshes the
   * server-built preview/totals when safe, and opens any already-known server
   * issue without invoking send-request. */
  onValidate() {
    if (this.sending()) return;

    const wasSaving = this.draftStore.saving();
    this.draftStore.flushNow();
    this.refreshCompiledJson();
    if (!wasSaving && !this.draftStore.saving()) this.refreshTotals();

    const firstTargetId = this.validation()?.firstTargetId;
    if (firstTargetId) {
      const issue = this.validationIssues().find(item => item.targetId === firstTargetId);
      if (issue) this.onValidationIssue(issue);
      else this.focus.scrollToAndFocus(firstTargetId);
      return;
    }
    this.toast.showInfo('Draft refreshed. Server validation runs immediately before a request is sent.');
  }

  onSummarySend() {
    this.onSendOrder({ url: this.customEndpointEnabled() ? this.customEndpointUrl() : '' });
  }

  onSendOrder(event: { url: string }) {
    if (this.moduleService.activeEnvironment()?.environment === 'Production') {
      this.pendingSendEvent = event;
      this.showProdSendConfirm.set(true);
      return;
    }
    this.performSend(event);
  }

  onConfirmProdSend() {
    this.showProdSendConfirm.set(false);
    if (this.pendingSendEvent) {
      this.performSend(this.pendingSendEvent);
      this.pendingSendEvent = null;
    }
  }

  private performSend(event: { url: string }) {
    // A send is already in flight -- block the duplicate (U4, D13).
    if (this.sending()) return;

    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.landedRequestId.set(null);
    this.validation.set(null);
    // Any edit still sitting in the debounce window must reach the server
    // draft before it is compiled into the payload.
    this.draftStore.flushNow();
    this.sending.set(true);

    this.api.post<SendOrderResult>(`modules/${key}/send-request`, { environmentKey: envKey, customApiUrl: event.url || undefined })
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: res => {
          this.apiResponse.set(res);
          if (res.success) {
            this.toast.showSuccess(`Order sent successfully! Status: ${res.statusCode}`);
            this.lookupLandedRequest();
          } else {
            this.toast.showError(`Order request failed. Status: ${res.statusCode}`);
          }
        },
        error: (err: ApiError) => {
          // U4: contract validation failures (400 { success:false,
          // errors:[...] }) map to inline field errors through the
          // centralized mapper -- the interceptor already toasted a single
          // summary, so no second toast here.
          if (err.code === 'validation_failed' && Array.isArray(err.details)) {
            const errors = err.details as string[];
            const mapped = mapSendValidationErrors(errors);
            this.validation.set(mapped);
            // The raw error list stays visible as the API response body for
            // diagnostics.
            this.apiResponse.set({
              success: false,
              statusCode: err.status,
              responseText: errors.join('\n'),
              urlSent: ''
            });
            if (mapped.firstTargetId) {
              const firstIssue = mapped.issues.find(issue => issue.targetId === mapped.firstTargetId);
              if (firstIssue) this.onValidationIssue(firstIssue);
              else this.focus.scrollToAndFocus(mapped.firstTargetId);
            }
            return;
          }
          this.apiResponse.set({ success: false, statusCode: err.status || 0, responseText: 'The request could not be completed.', urlSent: event.url });
        }
      });
  }

  /** OrderRequests is written by the upstream API itself (see
   * OrderController.SendRequest), not this call -- so the landed row is
   * looked up by order number just after send, only for modules with
   * Capabilities.OrderRequests (UPC today; GHC 501s here pending db-creds). */
  private lookupLandedRequest() {
    if (!this.moduleService.activeModule()?.capabilities?.orderRequests) return;

    const key = this.moduleKey();
    const orderNumber = String(this.draftStore.draft().orderData['order_code'] ?? '');
    if (!orderNumber) return;

    this.api.get<OrderRequestDetailResponse>(`modules/${key}/order-requests/by-order/${orderNumber}`, {
      envKey: this.moduleService.activeEnvironment()?.key
    }).subscribe({
      next: res => this.landedRequestId.set(res.request.id),
      error: () => { } // Not fatal -- the OrderRequests row may take a moment to land, or this send failed upstream before ever reaching it.
    });
  }

  /** U4 (D8): debounced server totals refresh. Coalesces bursts of draft
   * mutations into one GET instead of one HTTP request per keystroke. */
  scheduleTotalsRefresh() {
    if (this.totalsDebounceHandle) clearTimeout(this.totalsDebounceHandle);
    this.totalsDebounceHandle = setTimeout(() => {
      this.totalsDebounceHandle = null;
      this.refreshTotals();
    }, TOTALS_DEBOUNCE_MS);
  }

  /** Reads the authoritative server summary (TotalsCalculator via GET
   * calculate-totals). A response token prevents an older in-flight
   * response from overwriting newer totals. The last valid summary stays
   * on screen across refreshes and failures -- error state never
   * fabricates totals. */
  refreshTotals() {
    const key = this.moduleKey();
    if (!key) return;

    const token = ++this.totalsToken;
    this.totalsLoading.set(true);
    this.api.get<TotalsSummary>(`modules/${key}/calculate-totals`).subscribe({
      next: totals => {
        if (token !== this.totalsToken) return;
        this.totalsLoading.set(false);
        this.totals.set(totals);
        this.totalsError.set(null);
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast;
      // the last valid totals (if any) remain displayed.
      error: () => {
        if (token !== this.totalsToken) return;
        this.totalsLoading.set(false);
        this.totalsError.set('Totals could not be refreshed from the server.');
      }
    });
  }

  /** Product/payment mutation responses already carry the post-mutation
   * server totals -- adopting them directly saves a follow-up GET. Any
   * in-flight GET predates this snapshot, so it is invalidated. */
  private applyServerTotals(totals: TotalsSummary) {
    this.totalsToken++;
    this.totals.set(totals);
    this.totalsError.set(null);
  }

  /** The compiled-payload preview is the real server-built payload
   * (module.BuildPayload(draft) via GET export-json) -- not a hand-rolled
   * approximation, so it always matches what Send actually posts. */
  refreshCompiledJson() {
    const key = this.moduleKey();
    this.api.get<Record<string, unknown>>(`modules/${key}/export-json`).subscribe({
      next: json => this.compiledJson.set(json),
      error: () => { }
    });
  }

  openAddProductDialog() {
    this.itemLookupOutcome.set(null);
    this.showAddProductDialog.set(true);
  }

  ngAfterViewInit() {
    this.sectionQuerySubscription = this.sectionElementRefs.changes.subscribe(() => this.setupSectionObserver());
    this.setupSectionObserver();
  }

  private setupSectionObserver() {
    this.sectionObserver?.disconnect();
    this.sectionObserver = null;
    if (!this.sectionElementRefs?.length || typeof IntersectionObserver === 'undefined') return;

    this.sectionObserver = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((left, right) => left.boundingClientRect.top - right.boundingClientRect.top)[0];
      const id = visible?.target instanceof HTMLElement ? visible.target.id : '';
      if (id && this.sections().some(section => section.id === id)) this.activeSectionId.set(id);
    }, { rootMargin: '-16% 0px -68% 0px', threshold: [0, .2, .6] });

    this.sectionElementRefs.forEach(reference => this.sectionObserver?.observe(reference.nativeElement));
  }

  scrollToSection(sectionId: string) {
    const references = this.sectionElementRefs?.toArray() ?? [];
    const index = references.findIndex(item => item.nativeElement.id === sectionId);
    const element = references[index]?.nativeElement;
    if (!element) return;

    this.sectionComponents.get(index)?.expand();
    this.activeSectionId.set(sectionId);
    if (typeof element.scrollIntoView === 'function') {
      element.scrollIntoView({ behavior: this.prefersReducedMotion() ? 'auto' : 'smooth', block: 'start' });
    }
  }

  onValidationIssue(issue: OrderValidationIssue) {
    if (!issue.targetId) return;
    const sectionId = this.sectionIdForValidationKey(issue.key);
    if (sectionId) this.scrollToSection(sectionId);

    const focusTarget = () => this.focus.scrollToAndFocus(issue.targetId!);
    if (typeof document !== 'undefined' && document.getElementById(issue.targetId)) focusTarget();
    else if (typeof queueMicrotask === 'function') queueMicrotask(focusTarget);
    else focusTarget();
  }

  private sectionIdForValidationKey(key: string): string | null {
    if (['branch_code', 'order_code', 'parent_order_code', 'order_delivery_cost', 'order_status', 'order_notes', 'order_gps'].includes(key)) return 'order-header-section';
    if (['client_country_code', 'client_phone', 'client_first_name', 'client_middle_name', 'client_last_name', 'client_email', 'client_birthdate', 'client_gender', 'order_address', 'address_code'].includes(key)) return 'customer-section';
    if (['delivery_date', 'delivery_from_time', 'delivery_to_time', 'fullfilment_plant', 'shipping_address_2'].includes(key)) return 'delivery-section';
    if (key === 'products') return 'products-section';
    if (key === 'payments') return 'payments-section';
    return null;
  }

  private prefersReducedMotion(): boolean {
    try {
      return typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    } catch {
      return false;
    }
  }

  ngOnDestroy() {
    this.sectionObserver?.disconnect();
    this.sectionQuerySubscription?.unsubscribe();
    if (this.totalsDebounceHandle) clearTimeout(this.totalsDebounceHandle);
  }
}
