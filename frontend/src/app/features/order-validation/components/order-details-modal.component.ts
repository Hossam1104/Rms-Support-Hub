import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JsonViewerComponent } from '../../../shared/components/json-viewer/json-viewer.component';
import { OrderRequestDetailResponse } from '../../../core/models';
import { RiyalComponent, UiButtonComponent, UiCardComponent } from '../../../shared/ui';

@Component({
  selector: 'app-order-details-modal',
  standalone: true,
  imports: [CommonModule, JsonViewerComponent, RiyalComponent, UiButtonComponent, UiCardComponent],
  template: `
    <div class="modal-backdrop" (click)="close.emit()">
      <ui-card variant="raised" class="modal-dialog" (click)="$event.stopPropagation()">
        <div uiCardHeader class="modal-header">
          <h3><i class="bi bi-receipt-cutoff" aria-hidden="true"></i> Order Database Details ({{ orderNumber }})</h3>
          <ui-button variant="ghost" size="sm" icon="bi-x-lg" ariaLabel="Close" (pressed)="close.emit()"></ui-button>
        </div>

        <div class="modal-body" *ngIf="details as d; else loading">
          <section class="details-section">
            <h4 class="section-heading">Header Summary</h4>
            <div class="info-grid">
              <div><strong>Branch Code:</strong> {{ d.request.header?.branchCode }}</div><div><strong>Address:</strong> {{ d.request.header?.address }}</div>
              <div><strong>Mobile:</strong> {{ d.request.header?.consumerMobile }}</div><div><strong>Payment Method:</strong> {{ d.request.header?.orderPaymentMethod }}</div>
              <div><strong>Order Date:</strong> {{ d.request.header?.orderDate | date:'medium' }}</div><div><strong>Status:</strong> {{ d.request.header?.orderStatusLabel }}</div>
            </div>
          </section>
          <section class="details-section" *ngIf="d.request.exceptionMessage"><h4 class="section-heading">Exception</h4><p class="exception-text">{{ d.request.exceptionMessage }}</p></section>
          <section class="details-section" *ngIf="d.request.invoice">
            <h4 class="section-heading">Invoice Details</h4>
            <div class="info-grid"><div><strong>Barcode:</strong> {{ d.request.invoice.barcode }}</div><div><strong>Net Amount:</strong> <app-riyal [size]=".9"></app-riyal>{{ d.request.invoice.netAmount | number:'1.2-2' }}</div><div><strong>Invoice Date:</strong> {{ d.request.invoice.closeDateLocalTime | date:'medium' }}</div></div>
          </section>
          <div class="json-sections" *ngIf="d.request.requestJson"><app-json-viewer [data]="d.request.requestJson" title="Original Order Request JSON"></app-json-viewer><app-json-viewer *ngIf="d.request.responseJson" [data]="d.request.responseJson" title="Response JSON"></app-json-viewer></div>
        </div>

        <ng-template #loading><div class="loading-state"><i class="bi bi-arrow-repeat spin" aria-hidden="true"></i> Loading details from database...</div></ng-template>
        <div uiCardFooter class="modal-footer"><ui-button variant="secondary" (pressed)="close.emit()">Close</ui-button></div>
      </ui-card>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; inset: 0; z-index: 2000; display: grid; place-items: center; padding: 16px; background: var(--backdrop); backdrop-filter: blur(3px); }
    .modal-dialog { width: min(750px, 100%); max-height: min(850px, calc(100vh - 32px)); overflow: auto; }
    .modal-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
    .modal-header h3 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 1.1rem; }
    .modal-header h3 i { color: var(--accent); }
    .details-section { margin-bottom: 22px; }
    .section-heading { margin: 0 0 10px; padding-bottom: 6px; border-bottom: 1px solid var(--divider); color: var(--text-secondary); font-size: .95rem; }
    .info-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; color: var(--text-primary); font-size: .85rem; }
    .exception-text { margin: 0; padding: 12px 16px; border-radius: var(--radius-sm); background: var(--state-danger-bg); color: var(--state-danger-fg); white-space: pre-wrap; font-size: .85rem; }
    .loading-state { padding: 40px; color: var(--text-muted); text-align: center; }
    .spin { animation: spin 1s infinite linear; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    .modal-footer { display: flex; justify-content: flex-end; }
    @media (max-width: 560px) { .info-grid { grid-template-columns: 1fr; } }
  `]
})
export class OrderDetailsModalComponent {
  @Input() orderNumber = '';
  @Input() details: OrderRequestDetailResponse | null = null;
  @Output() close = new EventEmitter<void>();
}
