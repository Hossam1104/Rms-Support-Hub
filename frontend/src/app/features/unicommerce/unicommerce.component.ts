import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ProductionUnlockService } from '../../core/services/production-unlock.service';
import { ToastService } from '../../core/services/toast.service';
import { ApiError, OrderDraft, RowItem, Consumer, SendOrderResult, LookupResult, ModuleEndpoint } from '../../core/models';
import { InvoiceSummaryComponent } from './components/invoice-summary.component';
import { OrderFieldsComponent } from './components/order-fields.component';
import { ConsumerSectionComponent } from './components/consumer-section.component';
import { DeliverySectionComponent } from './components/delivery-section.component';
import { RowItemsTableComponent } from './components/row-items-table.component';
import { AddRowItemDialogComponent } from './components/add-row-item-dialog.component';
import { ApiConfigComponent } from '../flat-order/components/api-config.component';

function asNumber(value: unknown, fallback = 0): number {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

class DraftPersistenceError extends Error {
  constructor() {
    super('The latest invoice draft could not be saved.');
  }
}

@Component({
  selector: 'app-unicommerce',
  standalone: true,
  imports: [
    CommonModule,
    InvoiceSummaryComponent,
    OrderFieldsComponent,
    ConsumerSectionComponent,
    DeliverySectionComponent,
    RowItemsTableComponent,
    AddRowItemDialogComponent,
    ApiConfigComponent
  ],
  template: `
    <div class="unicommerce-container">
      <app-invoice-summary
        [grossAmount]="grossAmount()"
        [totalDiscount]="totalDiscount()"
        [totalVat]="totalVat()"
        [netAmount]="netAmount()"
        [customerCreditAmount]="customerCreditAmount()">
      </app-invoice-summary>

      <app-order-fields
        [orderData]="draft().orderData"
        (fieldChange)="onFieldChange($event)">
      </app-order-fields>

      <app-consumer-section
        [consumer]="draft().consumer"
        (fieldChange)="onConsumerFieldChange($event)"
        (lookupConsumer)="onLookupConsumer($event)">
      </app-consumer-section>

      <app-delivery-section
        [delivery]="draft().delivery"
        (fieldChange)="onDeliveryFieldChange($event)">
      </app-delivery-section>

      <app-row-items-table
        [rowItems]="draft().rowItems"
        (openAddDialog)="showAddRowItemDialog.set(true)"
        (deleteRowItem)="onDeleteRowItem($event)">
      </app-row-items-table>

      <app-api-config
        [compiledJson]="compiledJson()"
        [apiResponse]="apiResponse()"
        [loading]="sending()"
        [endpoint]="activeEndpoint()"
        [environment]="moduleService.activeEnvironment()"
        (sendRequest)="onSendOrder()">
      </app-api-config>
    </div>

    <app-add-row-item-dialog
      *ngIf="showAddRowItemDialog()"
      (close)="showAddRowItemDialog.set(false)"
      (add)="onAddRowItem($event)">
    </app-add-row-item-dialog>
  `,
  styles: [`
    .unicommerce-container { display: flex; flex-direction: column; }
  `]
})
export class UnicommerceComponent implements OnInit {
  private api = inject(ApiService);
  moduleService = inject(ModuleService);
  private readonly productionUnlock = inject(ProductionUnlockService);
  private toast = inject(ToastService);

  moduleKey = signal<string>('ghc_unicommerce');
  draft = signal<OrderDraft>({
    orderData: {},
    products: [],
    payments: [],
    consumer: {},
    delivery: { deliveryFees: 0 },
    rowItems: []
  });
  compiledJson = signal<Record<string, unknown> | null>(null);
  apiResponse = signal<SendOrderResult | null>(null);
  /** U4 (D13): the active environment's resolved endpoint, displayed
   * read-only in the shared API configuration component. */
  activeEndpoint = signal<ModuleEndpoint | null>(null);
  /** U4 (D13): real send-request lifecycle state. */
  sending = signal(false);

  /** Complete-draft writes must stay in browser order. `persistenceTail`
   * keeps later edits moving after a failed write, while
   * `latestPersistence` lets Send reject the current draft if its own write
   * failed. Each queued operation also refreshes compiled JSON only after its
   * PUT has completed, so an older export cannot overtake a newer draft. */
  private persistenceTail: Promise<void> = Promise.resolve();
  private latestPersistence: Promise<void> = Promise.resolve();

  showAddRowItemDialog = signal<boolean>(false);

  // Computed summary signals
  grossAmount = signal<number>(0);
  totalDiscount = signal<number>(0);
  totalVat = signal<number>(0);
  netAmount = signal<number>(0);
  customerCreditAmount = signal<number>(0);

  ngOnInit() {
    this.loadState();
  }

  loadState() {
    const key = this.moduleKey();
    this.loadEndpoint();
    this.api.get<OrderDraft>(`modules/${key}/state`).subscribe({
      next: state => {
        this.draft.set(state);
        this.recalculate();
        this.refreshCompiledJson();
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => {}
    });
  }

  /** U4 (D13): resolves the endpoint a send would target for the active
   * environment (same GetEnvironment resolution as send-request). */
  private loadEndpoint() {
    const key = this.moduleKey();
    this.api.get<ModuleEndpoint>(`modules/${key}/endpoint`, {
      envKey: this.moduleService.activeEnvironment()?.key
    }).subscribe({
      next: endpoint => this.activeEndpoint.set(endpoint),
      error: () => this.activeEndpoint.set(null)
    });
  }

  /** Keep the complete draft in the server-side session so send/export sees
   * order, consumer, delivery, and row-item edits together. */
  onFieldChange(event: { fieldName: string, value: unknown }) {
    const next = { ...this.draft(), orderData: { ...this.draft().orderData, [event.fieldName]: event.value } };
    this.draft.set(next);
    this.recalculate();
    this.persistDraft(next);
  }

  onConsumerFieldChange(event: { fieldName: string, value: unknown }) {
    const next = { ...this.draft(), consumer: { ...this.draft().consumer, [event.fieldName]: event.value } };
    this.draft.set(next);
    this.persistDraft(next);
  }

  onDeliveryFieldChange(event: { fieldName: string, value: unknown }) {
    const next = { ...this.draft(), delivery: { ...this.draft().delivery, [event.fieldName]: event.value } };
    this.draft.set(next);
    this.recalculate();
    this.persistDraft(next);
  }

  onAddRowItem(item: RowItem) {
    const next = { ...this.draft(), rowItems: [...this.draft().rowItems, item] };
    this.draft.set(next);
    this.recalculate();
    this.persistDraft(next);
    this.showAddRowItemDialog.set(false);
    this.toast.showSuccess('Row item added to invoice.');
  }

  onDeleteRowItem(index: number) {
    const next = { ...this.draft(), rowItems: this.draft().rowItems.filter((_, i) => i !== index) };
    this.draft.set(next);
    this.recalculate();
    this.persistDraft(next);
    this.toast.showInfo('Row item removed.');
  }

  onLookupConsumer(phone: string) {
    const key = this.moduleKey();
    this.api.get<LookupResult<Consumer>>(`modules/${key}/lookup/consumer`, { phone }).subscribe({
      next: res => {
        const c = res.data;
        if (res.success && c) {
          const next = {
            ...this.draft(),
            consumer: {
              ...this.draft().consumer,
              firstName: c.firstName || '',
              lastName: c.lastName || '',
              primaryPhoneNumber: c.primaryPhoneNumber || phone,
              email: c.email || ''
            }
          };
          this.draft.set(next);
          this.persistDraft(next);
          this.toast.showSuccess('Consumer details populated from DB.');
        } else {
          this.toast.showInfo(`No consumer record found for phone ${phone}.`);
        }
      },
      error: () => {}
    });
  }

  onSendOrder() {
    // A send is already in flight -- block the duplicate (U4, D13).
    if (this.sending()) return;

    const key = this.moduleKey();
    const environment = this.moduleService.activeEnvironment();
    const envKey = environment?.key;
    if (environment?.environment === 'Production' && !this.productionUnlock.isUnlocked(key, environment.key)) {
      this.productionUnlock.open({ moduleKey: key, environmentKey: environment.key, destination: 'unicommerce' });
      return;
    }
    const headers = envKey ? this.productionUnlock.mutationHeaders(key, envKey) : undefined;
    this.sending.set(true);
    this.flushLatestPersistence()
      .then(() => firstValueFrom(headers
        ? this.api.post<SendOrderResult>(`modules/${key}/send-request`, { environmentKey: envKey }, undefined, headers)
        : this.api.post<SendOrderResult>(`modules/${key}/send-request`, { environmentKey: envKey })))
      .then(res => {
        this.apiResponse.set(res);
        if (res.success) {
          this.toast.showSuccess(`Invoice submitted successfully! Status: ${res.statusCode}`);
        } else {
          this.toast.showError(`Invoice submission failed. Status: ${res.statusCode}`);
        }
      })
      .catch((err: ApiError | DraftPersistenceError) => {
        if (err instanceof DraftPersistenceError) {
          const responseText = 'The latest invoice draft could not be saved. The invoice was not sent.';
          this.apiResponse.set({ success: false, statusCode: 0, responseText, urlSent: '' });
          this.toast.showError(responseText);
          return;
        }

        // Contract validation failures (400 { success:false, errors })
        // keep their raw error list visible for diagnostics.
        const responseText = err.code === 'validation_failed' && Array.isArray(err.details)
          ? (err.details as string[]).join('\n')
          : 'The request could not be completed.';
        this.apiResponse.set({ success: false, statusCode: err.status || 0, responseText, urlSent: '' });
      })
      .finally(() => this.sending.set(false));
  }

  recalculate() {
    const items = this.draft().rowItems;
    let gross = 0, disc = 0, vat = 0;

    items.forEach(item => {
      const q = item.quantity || 0;
      const p = item.itemPrice || 0;
      const d = item.itemDiscount || 0;
      const vPct = (item.vatPercentage || 0) / 100;

      gross += q * p;
      disc += d * q;
      vat += (p - d) * vPct * q;
    });

    const deliveryFees = this.draft().delivery.deliveryFees || 0;
    const net = gross - disc + vat + deliveryFees;
    const paidOnline = asNumber(this.draft().orderData['paid_online_amount']);
    const paidPoints = asNumber(this.draft().orderData['paid_with_points_amount']);
    const customerCredit = net - paidOnline - paidPoints;

    this.grossAmount.set(Math.round(gross * 100) / 100);
    this.totalDiscount.set(Math.round(disc * 100) / 100);
    this.totalVat.set(Math.round(vat * 100) / 100);
    this.netAmount.set(Math.round(net * 100) / 100);
    this.customerCreditAmount.set(Math.round(customerCredit * 100) / 100);
  }

  refreshCompiledJson() {
    const key = this.moduleKey();
    this.api.get<Record<string, unknown>>(`modules/${key}/export-json`).subscribe({
      next: json => this.compiledJson.set(json),
      error: () => {}
    });
  }

  private persistDraft(draft: OrderDraft): Promise<void> {
    const previous = this.persistenceTail;
    const operation = previous
      .then(() => firstValueFrom(this.api.put(`modules/${this.moduleKey()}/state`, draft)))
      .then(() => firstValueFrom(this.api.get<Record<string, unknown>>(`modules/${this.moduleKey()}/export-json`)))
      .then(json => this.compiledJson.set(json));

    // Keep the queue alive after a failed edit so a later edit can recover,
    // but retain the actual latest operation for Send to await and reject.
    this.persistenceTail = operation.catch(() => undefined);
    this.latestPersistence = operation;
    operation.catch(() => {
      this.toast.showError('The latest invoice draft could not be saved. Sending is blocked until it is saved.');
    });
    return operation;
  }

  private async flushLatestPersistence(): Promise<void> {
    let operation = this.latestPersistence;
    while (true) {
      try {
        await operation;
      } catch {
        throw new DraftPersistenceError();
      }

      if (operation === this.latestPersistence) return;
      operation = this.latestPersistence;
    }
  }
}
