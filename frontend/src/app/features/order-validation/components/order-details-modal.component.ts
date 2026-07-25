import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JsonViewerComponent } from '../../../shared/components/json-viewer/json-viewer.component';
import { OrderRequestDetailResponse } from '../../../core/models';

@Component({
  selector: 'app-order-details-modal',
  standalone: true,
  imports: [CommonModule, JsonViewerComponent],
  template: `
    <div class="modal-backdrop glass-panel" (click)="close.emit()">
      <div class="modal-dialog glass-card fade-in-up" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h3><i class="bi bi-receipt-cutoff"></i> Order Database Details ({{ orderNumber }})</h3>
          <button type="button" class="btn-close" (click)="close.emit()">&times;</button>
        </div>

        <div class="modal-body" *ngIf="details as d; else loading">
          <div class="details-section mb-4">
            <h4 class="section-heading">Header Summary</h4>
            <div class="info-grid">
              <div><strong>Branch Code:</strong> {{ d.request.header?.branchCode }}</div>
              <div><strong>Address:</strong> {{ d.request.header?.address }}</div>
              <div><strong>Mobile:</strong> {{ d.request.header?.consumerMobile }}</div>
              <div><strong>Payment Method:</strong> {{ d.request.header?.orderPaymentMethod }}</div>
              <div><strong>Order Date:</strong> {{ d.request.header?.orderDate | date:'medium' }}</div>
              <div><strong>Status:</strong> {{ d.request.header?.orderStatusLabel }}</div>
            </div>
          </div>

          <div class="details-section mb-4" *ngIf="d.request.exceptionMessage">
            <h4 class="section-heading">Exception</h4>
            <p class="exception-text">{{ d.request.exceptionMessage }}</p>
          </div>

          <div class="details-section mb-4" *ngIf="d.request.invoice">
            <h4 class="section-heading">Invoice Details</h4>
            <div class="info-grid">
              <div><strong>Barcode:</strong> {{ d.request.invoice.barcode }}</div>
              <div><strong>Net Amount:</strong> {{ d.request.invoice.netAmount | number:'1.2-2' }} SAR</div>
              <div><strong>Invoice Date:</strong> {{ d.request.invoice.closeDateLocalTime | date:'medium' }}</div>
            </div>
          </div>

          <div class="json-sections" *ngIf="d.request.requestJson">
            <app-json-viewer [data]="d.request.requestJson" title="Original Order Request JSON"></app-json-viewer>
            <app-json-viewer *ngIf="d.request.responseJson" [data]="d.request.responseJson" title="Response JSON"></app-json-viewer>
          </div>
        </div>

        <ng-template #loading>
          <div class="loading-state">
            <i class="bi bi-arrow-repeat spin"></i> Loading details from database...
          </div>
        </ng-template>

        <div class="modal-footer">
          <button type="button" class="glass-button" (click)="close.emit()">Close</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 2000; display: flex; align-items: center; justify-content: center; }
    .modal-dialog { width: 100%; max-width: 750px; max-height: 85vh; overflow-y: auto; padding: 24px; border-radius: var(--radius-lg); }
    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .modal-header h3 { margin: 0; font-size: 1.2rem; display: flex; align-items: center; gap: 8px; }
    .btn-close { background: none; border: none; font-size: 1.5rem; color: var(--text-muted); cursor: pointer; }
    .section-heading { font-size: 0.95rem; font-weight: 600; color: var(--text-secondary); margin-bottom: 10px; border-bottom: 1px solid var(--glass-border); padding-bottom: 4px; }
    .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; font-size: 0.85rem; }
    .exception-text { background: var(--danger-bg); color: var(--danger); padding: 12px 16px; border-radius: var(--radius-sm); font-size: 0.85rem; white-space: pre-wrap; }
    .loading-state { text-align: center; padding: 40px; color: var(--text-muted); }
    .spin { animation: spin 1s infinite linear; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    .modal-footer { display: flex; justify-content: flex-end; margin-top: 24px; }
  `]
})
export class OrderDetailsModalComponent {
  @Input() orderNumber: string = '';
  @Input() details: OrderRequestDetailResponse | null = null;
  @Output() close = new EventEmitter<void>();
}
