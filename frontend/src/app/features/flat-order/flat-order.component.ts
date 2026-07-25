import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { OrderDraft, Product, Payment, Consumer, SendOrderResult, LookupResult } from '../../core/models';
import { QuickStatsComponent } from './components/quick-stats.component';
import { OrderInfoComponent } from './components/order-info.component';
import { ClientInfoComponent } from './components/client-info.component';
import { DeliveryInfoComponent } from './components/delivery-info.component';
import { ProductsTableComponent } from './components/products-table.component';
import { PaymentsTableComponent } from './components/payments-table.component';
import { ApiConfigComponent } from './components/api-config.component';
import { AddProductDialogComponent } from './components/add-product-dialog.component';
import { AddPaymentDialogComponent } from './components/add-payment-dialog.component';

/** Client-side preview total -- NOT the backend's full TotalsSummary (which
 * also reports totalProductAmount/totalProductVat/orderDiscount). Kept
 * local rather than reusing TotalsSummary because this is a UI-only preview
 * computed ahead of any server round trip; it is never sent anywhere. */
interface FlatOrderPreviewTotals {
  totalOrderAmount: number;
  totalPaidAmount: number;
  remainingBalance: number;
}

function asString(value: unknown, fallback = ''): string {
  return value == null ? fallback : String(value);
}

function asNumber(value: unknown, fallback = 0): number {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

@Component({
  selector: 'app-flat-order',
  standalone: true,
  imports: [
    CommonModule,
    QuickStatsComponent,
    OrderInfoComponent,
    ClientInfoComponent,
    DeliveryInfoComponent,
    ProductsTableComponent,
    PaymentsTableComponent,
    ApiConfigComponent,
    AddProductDialogComponent,
    AddPaymentDialogComponent
  ],
  template: `
    <div class="flat-order-container">
      <app-quick-stats
        [totalAmount]="totals().totalOrderAmount"
        [paidAmount]="totals().totalPaidAmount"
        [remainingBalance]="totals().remainingBalance">
      </app-quick-stats>

      <app-order-info
        [orderData]="draft().orderData"
        [moduleKey]="moduleKey()"
        (fieldChange)="onFieldChange($event)">
      </app-order-info>

      <app-client-info
        [orderData]="draft().orderData"
        (fieldChange)="onFieldChange($event)"
        (lookupConsumer)="onLookupConsumer($event)">
      </app-client-info>

      <app-delivery-info
        *ngIf="moduleKey() === 'ghc_ecommerce'"
        [orderData]="draft().orderData"
        (fieldChange)="onFieldChange($event)">
      </app-delivery-info>

      <app-products-table
        [products]="draft().products"
        (openAddDialog)="showAddProductDialog.set(true)"
        (deleteProduct)="onDeleteProduct($event)">
      </app-products-table>

      <app-payments-table
        [payments]="draft().payments"
        (openAddDialog)="showAddPaymentDialog.set(true)"
        (deletePayment)="onDeletePayment($event)">
      </app-payments-table>

      <app-api-config
        [compiledJson]="compiledJson()"
        [apiResponse]="apiResponse()"
        (sendRequest)="onSendOrder($event)">
      </app-api-config>
    </div>

    <app-add-product-dialog
      *ngIf="showAddProductDialog()"
      [moduleKey]="moduleKey()"
      [branchCode]="branchCode()"
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
  `]
})
export class FlatOrderComponent implements OnInit {
  private api = inject(ApiService);
  moduleService = inject(ModuleService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);

  moduleKey = signal<string>('ghc_ecommerce');
  draft = signal<OrderDraft>({
    orderData: {
      branch_code: '101',
      order_code: 'ORD-1002',
      client_name: 'Walk-in Customer',
      client_code: 'CUST-01',
      client_mobile: '0501234567',
      shipping_address: 'Riyadh Main Street 1',
      order_status: '1',
      order_delivery_cost: 15
    },
    products: [],
    payments: [],
    consumer: {},
    delivery: { deliveryFees: 0 },
    rowItems: []
  });
  totals = signal<FlatOrderPreviewTotals>({ totalOrderAmount: 0, totalPaidAmount: 0, remainingBalance: 0 });
  compiledJson = signal<Record<string, unknown> | null>(null);
  apiResponse = signal<SendOrderResult | null>(null);

  showAddProductDialog = signal<boolean>(false);
  showAddPaymentDialog = signal<boolean>(false);

  ngOnInit() {
    this.route.parent?.paramMap.subscribe(params => {
      const key = params.get('key') || 'ghc_ecommerce';
      this.moduleKey.set(key);
      this.loadState();
    });
  }

  branchCode(): string {
    return asString(this.draft().orderData['branch_code']);
  }

  loadState() {
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<OrderDraft>(`modules/${key}/state`, { envKey }).subscribe({
      next: state => {
        if (state) this.draft.set(state);
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

  onFieldChange(event: { fieldName: string, value: unknown }) {
    this.draft.update(d => ({ ...d, orderData: { ...d.orderData, [event.fieldName]: event.value } }));
    this.recalculate();
    this.refreshCompiledJson();

    const key = this.moduleKey();
    this.api.put<{ success: boolean, state: OrderDraft }>(`modules/${key}/order-field`, event).subscribe({
      next: res => { if (res?.state) this.draft.set(res.state); },
      error: () => {}
    });
  }

  onAddProduct(product: Product) {
    this.draft.update(d => ({ ...d, products: [...d.products, product] }));
    this.recalculate();
    this.refreshCompiledJson();
    this.showAddProductDialog.set(false);
    this.toast.showSuccess('Product added successfully.');

    const key = this.moduleKey();
    this.api.post(`modules/${key}/products`, product).subscribe({ error: () => {} });
  }

  onDeleteProduct(index: number) {
    this.draft.update(d => ({ ...d, products: d.products.filter((_, i) => i !== index) }));
    this.recalculate();
    this.refreshCompiledJson();
    this.toast.showInfo('Product removed.');

    const key = this.moduleKey();
    this.api.delete(`modules/${key}/products/${index}`).subscribe({ error: () => {} });
  }

  onAddPayment(payment: Payment) {
    this.draft.update(d => ({ ...d, payments: [...d.payments, payment] }));
    this.recalculate();
    this.refreshCompiledJson();
    this.showAddPaymentDialog.set(false);
    this.toast.showSuccess('Payment method added.');

    const key = this.moduleKey();
    this.api.post(`modules/${key}/payments`, payment).subscribe({ error: () => {} });
  }

  onDeletePayment(index: number) {
    this.draft.update(d => ({ ...d, payments: d.payments.filter((_, i) => i !== index) }));
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

  onLookupConsumer(phone: string) {
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<LookupResult<Consumer>>(`modules/${key}/lookup/consumer`, { phone, envKey }).subscribe({
      next: res => {
        const c = res.data;
        if (res.success && c) {
          this.onFieldChange({ fieldName: 'client_name', value: c.firstName || '' });
          this.onFieldChange({ fieldName: 'client_code', value: c.consumerCode || '' });
          this.onFieldChange({ fieldName: 'client_mobile', value: c.primaryPhoneNumber || phone });
          if (c.nationalId) this.onFieldChange({ fieldName: 'shipping_address', value: c.nationalId });
          if (c.nationality) this.onFieldChange({ fieldName: 'district_name', value: c.nationality });
          if (c.gender) this.onFieldChange({ fieldName: 'city_name', value: c.gender });
          this.toast.showSuccess(`Found consumer: ${c.firstName} (${c.consumerCode || phone})`);
        } else {
          this.toast.showInfo(`No consumer record found for phone ${phone}.`);
        }
      },
      error: () => {}
    });
  }

  onSendOrder(event: { url: string }) {
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.post<SendOrderResult>(`modules/${key}/send-request`, { environmentKey: envKey, customApiUrl: event.url || undefined }).subscribe({
      next: res => {
        this.apiResponse.set(res);
        if (res.success) {
          this.toast.showSuccess(`Order sent successfully! Status: ${res.statusCode}`);
        } else {
          this.toast.showError(`Order request failed. Status: ${res.statusCode}`);
        }
      },
      error: () => {
        this.apiResponse.set({ success: false, statusCode: 0, responseText: 'The request could not be completed.', urlSent: event.url });
      }
    });
  }

  recalculate() {
    const products = this.draft().products;
    let prodTotal = 0;
    products.forEach(p => {
      const price = p.unitPrice || 0;
      const disc = p.discount || 0;
      const vat = (p.vatPercentage || 0) / 100;
      prodTotal += (price - disc + (price - disc) * vat) * (p.quantity || 0);
    });

    const deliveryCost = asNumber(this.draft().orderData['order_delivery_cost']);
    const totalOrderAmount = prodTotal + deliveryCost;

    const payments = this.draft().payments;
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

  refreshCompiledJson() {
    const data = this.draft().orderData;
    const prods = this.draft().products;
    const pays = this.draft().payments;

    this.compiledJson.set({
      Header: {
        BranchCode: asString(data['branch_code'], '101'),
        OrderCode: asString(data['order_code']),
        ClientName: asString(data['client_name']),
        ClientMobile: asString(data['client_mobile']),
        ShippingAddress: asString(data['shipping_address']),
        TotalAmount: this.totals().totalOrderAmount
      },
      Items: prods.map(p => ({
        ItemCode: p.itemCode,
        ItemName: p.itemName,
        Quantity: p.quantity,
        UnitPrice: p.unitPrice,
        VatPercentage: p.vatPercentage,
        Discount: p.discount
      })),
      Payments: pays.map(py => ({
        PaymentMethod: py.paymentMethod,
        PaymentStatus: py.paymentStatus,
        Amount: py.paymentAmount
      }))
    });
  }
}
