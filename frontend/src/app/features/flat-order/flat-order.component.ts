import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { FocusService } from '../../core/services/focus.service';
import { ApiError, BranchOption, LookupResult, ModuleEndpoint, OrderDraft, Product, Payment, Consumer, SendOrderResult, OrderRequestDetailResponse, TotalsSummary } from '../../core/models';
import { QuickStatsComponent } from './components/quick-stats.component';
import { OrderInfoComponent } from './components/order-info.component';
import { ClientInfoComponent } from './components/client-info.component';
import { DeliveryInfoComponent } from './components/delivery-info.component';
import { ProductsTableComponent } from './components/products-table.component';
import { PaymentsTableComponent } from './components/payments-table.component';
import { ApiConfigComponent } from './components/api-config.component';
import { AddProductDialogComponent, ItemLookupOutcome } from './components/add-product-dialog.component';
import { AddPaymentDialogComponent } from './components/add-payment-dialog.component';
import { ConfirmDialogComponent } from '../../shared/ui';
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
    QuickStatsComponent,
    OrderInfoComponent,
    ClientInfoComponent,
    DeliveryInfoComponent,
    ProductsTableComponent,
    PaymentsTableComponent,
    ApiConfigComponent,
    AddProductDialogComponent,
    AddPaymentDialogComponent,
    ConfirmDialogComponent
  ],
  providers: [DraftStore],
  template: `
    <div class="flat-order-container">
      <app-quick-stats
        [totals]="totals()"
        [productCount]="draftStore.draft().products.length"
        [totalQuantity]="totalQuantity()"
        [loading]="totalsLoading()"
        [error]="totalsError()">
      </app-quick-stats>

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

      <app-client-info
        [orderData]="draftStore.draft().orderData"
        [fieldErrors]="fieldErrors()"
        (fieldChange)="onFieldChange($event)"
        (lookupConsumer)="onLookupConsumer($event)">
      </app-client-info>

      <app-delivery-info
        *ngIf="hasDeliveryFields()"
        [orderData]="draftStore.draft().orderData"
        (fieldChange)="onFieldChange($event)">
      </app-delivery-info>

      <app-products-table
        [products]="draftStore.draft().products"
        [errors]="fieldErrors()['products'] ?? []"
        (openAddDialog)="openAddProductDialog()"
        (deleteProduct)="onDeleteProduct($event)">
      </app-products-table>

      <app-payments-table
        [payments]="draftStore.draft().payments"
        [errors]="fieldErrors()['payments'] ?? []"
        (openAddDialog)="showAddPaymentDialog.set(true)"
        (deletePayment)="onDeletePayment($event)">
      </app-payments-table>

      <app-api-config
        [compiledJson]="compiledJson()"
        [apiResponse]="apiResponse()"
        [loading]="sending()"
        [endpoint]="activeEndpoint()"
        [environment]="moduleService.activeEnvironment()"
        [validationSummary]="validationSummary()"
        (sendRequest)="onSendOrder($event)">
      </app-api-config>

      <div class="landed-card glass-card" *ngIf="landedRequestId() as id">
        <i class="bi bi-check-circle-fill"></i>
        <span>Order recorded as request #{{ id }} in the OrderRequests table.</span>
        <a [routerLink]="['/modules', moduleKey(), 'requests', id]" class="landed-link">
          Open in Order Requests <i class="bi bi-arrow-right"></i>
        </a>
      </div>
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
      (close)="showAddPaymentDialog.set(false)"
      (add)="onAddPayment($event)">
    </app-add-payment-dialog>
  `,
  styles: [`
    .flat-order-container { display: flex; flex-direction: column; }
    .landed-card {
      display: flex; align-items: center; gap: 12px; padding: 16px 20px;
      background: var(--success-bg); color: var(--success); font-weight: 600;
      animation: fadeInUp var(--d-slow) var(--ease-spring);
    }
    .landed-card i.bi-check-circle-fill { font-size: 1.3rem; }
    .landed-link { margin-left: auto; display: flex; align-items: center; gap: 6px; color: var(--success); text-decoration: underline; font-weight: 700; white-space: nowrap; }
  `]
})
export class FlatOrderComponent implements OnInit {
  private api = inject(ApiService);
  private branchOptions = inject(BranchOptionsService);
  moduleService = inject(ModuleService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);
  private focus = inject(FocusService);
  draftStore = inject(DraftStore);

  moduleKey = signal<string>('ghc_ecommerce');
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
  validationSummary = computed(() => {
    const v = this.validation();
    return v ? { totalCount: v.totalCount, globalErrors: v.globalErrors } : null;
  });

  showAddProductDialog = signal<boolean>(false);
  showAddPaymentDialog = signal<boolean>(false);

  showProdSendConfirm = signal<boolean>(false);
  pendingSendEvent: { url: string } | null = null;

  /** undefined until the first environment is known, so the effect below
   * never re-fetches on the initial load -- only on an actual switch. */
  private lastEnvKey: string | undefined = undefined;
  private branchLoadToken = 0;
  private endpointLoadToken = 0;
  private totalsToken = 0;
  private totalsDebounceHandle: ReturnType<typeof setTimeout> | null = null;

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
    this.route.parent?.paramMap.subscribe(params => {
      const key = params.get('key') || 'ghc_ecommerce';
      this.moduleKey.set(key);
      this.loadState();
    });
  }

  hasDeliveryFields(): boolean {
    return this.moduleService.activeModule()?.capabilities?.hasDeliveryFields ?? false;
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
    this.draftStore.setModuleKey(key);
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.loadBranches();
    this.loadEndpoint();
    this.api.get<OrderDraft>(`modules/${key}/state`, { envKey }).subscribe({
      next: state => {
        if (state) this.draftStore.setDraft(state);
        this.refreshTotals();
        this.refreshCompiledJson();
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => {
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
      this.validation.set({ ...current, fieldErrors, totalCount });
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
      error: () => {}
    });
  }

  onDeleteProduct(index: number) {
    this.draftStore.updateLocal(d => ({ ...d, products: d.products.filter((_, i) => i !== index) }));
    this.refreshCompiledJson();
    this.toast.showInfo('Product removed.');

    const key = this.moduleKey();
    this.api.delete<{ success: boolean, products: Product[], totals: TotalsSummary }>(`modules/${key}/products/${index}`).subscribe({
      next: res => this.applyServerTotals(res.totals),
      error: () => {}
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
      error: () => {}
    });
  }

  onDeletePayment(index: number) {
    this.draftStore.updateLocal(d => ({ ...d, payments: d.payments.filter((_, i) => i !== index) }));
    this.refreshCompiledJson();
    this.toast.showInfo('Payment method removed.');

    const key = this.moduleKey();
    this.api.delete<{ success: boolean, payments: Payment[], totals: TotalsSummary }>(`modules/${key}/payments/${index}`).subscribe({
      next: res => this.applyServerTotals(res.totals),
      error: () => {}
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
            ['Phone', 'client_phone', c.primaryPhoneNumber || phone]
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
      error: () => {}
    });
  }

  /** Gates the send behind a typed confirmation when the active lane is
   * Production (U1, UI_Rework_Plan.md §3 decision 4) -- Testing sends
   * proceed immediately. */
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
            if (mapped.firstTargetId) this.focus.scrollToAndFocus(mapped.firstTargetId);
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
      error: () => {} // Not fatal -- the OrderRequests row may take a moment to land, or this send failed upstream before ever reaching it.
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
      error: () => {}
    });
  }

  openAddProductDialog() {
    this.itemLookupOutcome.set(null);
    this.showAddProductDialog.set(true);
  }
}
