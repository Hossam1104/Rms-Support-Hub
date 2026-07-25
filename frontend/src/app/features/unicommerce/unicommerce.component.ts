import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { InvoiceSummaryComponent } from './components/invoice-summary.component';
import { OrderFieldsComponent } from './components/order-fields.component';
import { ConsumerSectionComponent } from './components/consumer-section.component';
import { DeliverySectionComponent } from './components/delivery-section.component';
import { RowItemsTableComponent } from './components/row-items-table.component';
import { AddRowItemDialogComponent } from './components/add-row-item-dialog.component';
import { ApiConfigComponent } from '../flat-order/components/api-config.component';

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
        [targetUrl]="moduleService.activeEnvironment()?.apiUrl || ''"
        [compiledJson]="compiledJson()"
        [apiResponse]="apiResponse()"
        (sendRequest)="onSendOrder($event)">
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
  private toast = inject(ToastService);

  moduleKey = signal<string>('ghc_unicommerce');
  draft = signal<any>({ orderData: {}, consumer: {}, delivery: {}, rowItems: [] });
  compiledJson = signal<any>(null);
  apiResponse = signal<any>(null);

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
    this.api.get<any>(`modules/${key}/state`).subscribe({
      next: state => {
        this.draft.set(state);
        this.recalculate();
        this.refreshCompiledJson();
      },
      error: () => this.toast.showError('Failed to load Uni-Commerce invoice draft state.')
    });
  }

  onFieldChange(event: { fieldName: string, value: any }) {
    const key = this.moduleKey();
    this.api.put<any>(`modules/${key}/order-field`, event).subscribe({
      next: res => {
        this.draft.set(res.state);
        this.recalculate();
        this.refreshCompiledJson();
      }
    });
  }

  onConsumerFieldChange(event: { fieldName: string, value: any }) {
    this.draft.update(d => {
      d.consumer[event.fieldName] = event.value;
      return { ...d };
    });
    this.refreshCompiledJson();
  }

  onDeliveryFieldChange(event: { fieldName: string, value: any }) {
    this.draft.update(d => {
      d.delivery[event.fieldName] = event.value;
      return { ...d };
    });
    this.recalculate();
    this.refreshCompiledJson();
  }

  onAddRowItem(item: any) {
    this.draft.update(d => {
      d.rowItems.push(item);
      return { ...d };
    });
    this.recalculate();
    this.refreshCompiledJson();
    this.showAddRowItemDialog.set(false);
    this.toast.showSuccess('Row item added to invoice.');
  }

  onDeleteRowItem(index: number) {
    this.draft.update(d => {
      d.rowItems.splice(index, 1);
      return { ...d };
    });
    this.recalculate();
    this.refreshCompiledJson();
    this.toast.showInfo('Row item removed.');
  }

  onLookupConsumer(phone: string) {
    const key = this.moduleKey();
    this.api.get<any>(`modules/${key}/lookup/consumer`, { phone }).subscribe({
      next: res => {
        if (res.success && res.data) {
          const c = res.data;
          this.draft.update(d => ({
            ...d,
            consumer: {
              ...d.consumer,
              firstName: c.firstName || '',
              lastName: c.lastName || '',
              primaryPhoneNumber: c.primaryPhoneNumber || phone,
              email: c.email || ''
            }
          }));
          this.toast.showSuccess('Consumer details populated from DB.');
          this.refreshCompiledJson();
        }
      },
      error: () => this.toast.showError('Consumer not found.')
    });
  }

  onSendOrder(event: { url: string }) {
    const key = this.moduleKey();
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.post<any>(`modules/${key}/send-request`, { environmentKey: envKey, customApiUrl: event.url }).subscribe({
      next: res => {
        this.apiResponse.set(res);
        if (res.success) {
          this.toast.showSuccess(`Invoice submitted successfully! Status: ${res.statusCode}`);
        } else {
          this.toast.showError(`Invoice submission failed. Status: ${res.statusCode}`);
        }
      },
      error: () => this.toast.showError('Invoice request execution failed.')
    });
  }

  recalculate() {
    const items = this.draft().rowItems || [];
    let gross = 0, disc = 0, vat = 0;

    items.forEach((item: any) => {
      const q = item.quantity || 0;
      const p = item.itemPrice || 0;
      const d = item.itemDiscount || 0;
      const vPct = (item.vatPercentage || 0) / 100;

      gross += q * p;
      disc += d * q;
      vat += (p - d) * vPct * q;
    });

    const deliveryFees = this.draft().delivery?.deliveryFees || 0;
    const net = gross - disc + vat + deliveryFees;
    const paidOnline = this.draft().orderData?.paid_online_amount || 0;
    const paidPoints = this.draft().orderData?.paid_with_points_amount || 0;
    const customerCredit = net - paidOnline - paidPoints;

    this.grossAmount.set(Math.round(gross * 100) / 100);
    this.totalDiscount.set(Math.round(disc * 100) / 100);
    this.totalVat.set(Math.round(vat * 100) / 100);
    this.netAmount.set(Math.round(net * 100) / 100);
    this.customerCreditAmount.set(Math.round(customerCredit * 100) / 100);
  }

  refreshCompiledJson() {
    const key = this.moduleKey();
    this.api.get<any>(`modules/${key}/export-json`).subscribe({
      next: json => this.compiledJson.set(json)
    });
  }
}
