import { Component, OnInit, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { BranchOption, LookupResult, OrderDraft, Product, Payment, Consumer, SendOrderResult, OrderRequestDetailResponse } from '../../core/models';
import { QuickStatsComponent } from './components/quick-stats.component';
import { OrderInfoComponent } from './components/order-info.component';
import { ClientInfoComponent } from './components/client-info.component';
import { DeliveryInfoComponent } from './components/delivery-info.component';
import { ProductsTableComponent } from './components/products-table.component';
import { PaymentsTableComponent } from './components/payments-table.component';
import { ApiConfigComponent } from './components/api-config.component';
import { AddProductDialogComponent } from './components/add-product-dialog.component';
import { AddPaymentDialogComponent } from './components/add-payment-dialog.component';
import { ConfirmDialogComponent } from '../../shared/ui';
import { DraftStore } from './draft.store';

/** Client-side preview total -- NOT the backend's full TotalsSummary (which
 * also reports totalProductAmount/totalProductVat/orderDiscount). Kept
 * local rather than reusing TotalsSummary because this is a UI-only preview
 * computed ahead of any server round trip; it is never sent anywhere. */
interface FlatOrderPreviewTotals {
  totalOrderAmount: number;
  totalPaidAmount: number;
  remainingBalance: number;
}

function asNumber(value: unknown, fallback = 0): number {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

/**
 * Rebound (R10, remediation_plan.md B1/B5/B21) to the corrected schema
 * FlatOrderPayloadBuilder.cs actually reads -- see client-info.component.ts
 * and order-info.component.ts for the field-level detail. The Delivery
 * card and the compiled-JSON preview are both driven by real server data
 * (Capabilities.HasDeliveryFields and GET .../export-json respectively)
 * instead of a hand-rolled approximation or a module-key string comparison.
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
        [totalAmount]="totals().totalOrderAmount"
        [paidAmount]="totals().totalPaidAmount"
        [remainingBalance]="totals().remainingBalance">
      </app-quick-stats>

      <app-order-info
        [orderData]="draftStore.draft().orderData"
        [moduleKey]="moduleKey()"
        [branches]="branches()"
        [branchesLoading]="branchesLoading()"
        [branchError]="branchError()"
        (branchRefresh)="loadBranches(true)"
        (fieldChange)="onFieldChange($event)">
      </app-order-info>

      <app-client-info
        [orderData]="draftStore.draft().orderData"
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
        (openAddDialog)="showAddProductDialog.set(true)"
        (deleteProduct)="onDeleteProduct($event)">
      </app-products-table>

      <app-payments-table
        [payments]="draftStore.draft().payments"
        (openAddDialog)="showAddPaymentDialog.set(true)"
        (deletePayment)="onDeletePayment($event)">
      </app-payments-table>

      <app-api-config
        [compiledJson]="compiledJson()"
        [apiResponse]="apiResponse()"
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
  draftStore = inject(DraftStore);

  moduleKey = signal<string>('ghc_ecommerce');
  totals = signal<FlatOrderPreviewTotals>({ totalOrderAmount: 0, totalPaidAmount: 0, remainingBalance: 0 });
  compiledJson = signal<Record<string, unknown> | null>(null);
  apiResponse = signal<SendOrderResult | null>(null);
  landedRequestId = signal<number | null>(null);
  branches = signal<BranchOption[]>([]);
  branchesLoading = signal(false);
  branchError = signal<string | null>(null);


  showAddProductDialog = signal<boolean>(false);
  showAddPaymentDialog = signal<boolean>(false);

  showProdSendConfirm = signal<boolean>(false);
  pendingSendEvent: { url: string } | null = null;

  /** undefined until the first environment is known, so the effect below
   * never re-fetches on the initial load -- only on an actual switch. */
  private lastEnvKey: string | undefined = undefined;
  private branchLoadToken = 0;

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
    this.api.get<OrderDraft>(`modules/${key}/state`, { envKey }).subscribe({
      next: state => {
        if (state) this.draftStore.setDraft(state);
        this.recalculate();
        this.refreshCompiledJson();
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => {
        this.recalculate();
        this.refreshCompiledJson();
      }
    });
  }

  /** Applies immediately to local state and debounces the persisted write
   * via DraftStore.patch (U2, UI_Rework_Plan.md D1) -- see draft.store.ts. */
  onFieldChange(event: { fieldName: string, value: unknown }) {
    this.draftStore.patch({ [event.fieldName]: event.value });
    this.recalculate();
    this.refreshCompiledJson();
  }

  onAddProduct(product: Product) {
    this.draftStore.updateLocal(d => ({ ...d, products: [...d.products, product] }));
    this.recalculate();
    this.refreshCompiledJson();
    this.showAddProductDialog.set(false);
    this.toast.showSuccess('Product added successfully.');

    const key = this.moduleKey();
    this.api.post(`modules/${key}/products`, product).subscribe({ error: () => {} });
  }

  onDeleteProduct(index: number) {
    this.draftStore.updateLocal(d => ({ ...d, products: d.products.filter((_, i) => i !== index) }));
    this.recalculate();
    this.refreshCompiledJson();
    this.toast.showInfo('Product removed.');

    const key = this.moduleKey();
    this.api.delete(`modules/${key}/products/${index}`).subscribe({ error: () => {} });
  }

  onAddPayment(payment: Payment) {
    this.draftStore.updateLocal(d => ({ ...d, payments: [...d.payments, payment] }));
    this.recalculate();
    this.refreshCompiledJson();
    this.showAddPaymentDialog.set(false);
    this.toast.showSuccess('Payment method added.');

    const key = this.moduleKey();
    this.api.post(`modules/${key}/payments`, payment).subscribe({ error: () => {} });
  }

  onDeletePayment(index: number) {
    this.draftStore.updateLocal(d => ({ ...d, payments: d.payments.filter((_, i) => i !== index) }));
    this.recalculate();
    this.refreshCompiledJson();
    this.toast.showInfo('Payment method removed.');

    const key = this.moduleKey();
    this.api.delete(`modules/${key}/payments/${index}`).subscribe({ error: () => {} });
  }

  onLookupItem(event: { code: string, branchCode: string }) {
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<LookupResult<Product>>(`modules/${key}/lookup/item`, { code: event.code, branchCode: event.branchCode, envKey }).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.toast.showSuccess(`Found item: ${res.data.itemName}`);
        } else {
          this.toast.showInfo(`Item ${event.code} not found in database.`);
        }
      },
      // A genuine HTTP failure (DB unreachable, etc.) is already toasted by
      // errorEnvelopeInterceptor -- this only covers the 200 "not found" path above.
      error: () => {}
    });
  }

  /** Prefills name AND address from the lookup (R10 step 2) -- order_address/
   * address_code come from Consumer.Address/AddressCode, resolved server-side
   * from LoyaltyConsumerAddresses (UPC) with a preference for the row
   * flagged IsMaster -- see UpcConsumerRepository.
   *
   * U2 (UI_Rework_Plan.md D1): every prefilled field is sent as ONE
   * DraftStore.patch call instead of the previous nine sequential
   * onFieldChange/PUT order-field calls -- that was the exact race in the
   * reported screenshot, where late responses built from stale reads
   * clobbered fields a later request had already written. The toast now
   * separately names which fields the lookup actually returned and which
   * came back empty, so an empty Last Name reads as the data's fault, not
   * a tool bug. */
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
          this.recalculate();
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
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.landedRequestId.set(null);
    // Any edit still sitting in the debounce window must reach the server
    // draft before it is compiled into the payload.
    this.draftStore.flushNow();

    this.api.post<SendOrderResult>(`modules/${key}/send-request`, { environmentKey: envKey, customApiUrl: event.url || undefined }).subscribe({
      next: res => {
        this.apiResponse.set(res);
        if (res.success) {
          this.toast.showSuccess(`Order sent successfully! Status: ${res.statusCode}`);
          this.lookupLandedRequest();
        } else {
          this.toast.showError(`Order request failed. Status: ${res.statusCode}`);
        }
      },
      error: () => {
        this.apiResponse.set({ success: false, statusCode: 0, responseText: 'The request could not be completed.', urlSent: event.url });
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

  recalculate() {
    const products = this.draftStore.draft().products;
    let prodTotal = 0;
    products.forEach(p => {
      const price = p.unitPrice || 0;
      const disc = p.discount || 0;
      const vat = (p.vatPercentage || 0) / 100;
      prodTotal += (price - disc + (price - disc) * vat) * (p.quantity || 0);
    });

    const deliveryCost = asNumber(this.draftStore.draft().orderData['order_delivery_cost']);
    const totalOrderAmount = prodTotal + deliveryCost;

    const payments = this.draftStore.draft().payments;
    let paid = 0;
    payments.forEach(py => {
      if (py.paymentStatus === 'done_payment' || py.paymentMethod === 'CashOnDelivery') {
        paid += py.paymentAmount || 0;
      }
    });

    this.totals.set({
      totalOrderAmount: Math.round(totalOrderAmount * 100) / 100,
      totalPaidAmount: Math.round(paid * 100) / 100,
      remainingBalance: Math.round((totalOrderAmount - paid) * 100) / 100
    });
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
}
