import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JsonViewerComponent } from '../../../shared/components/json-viewer/json-viewer.component';
import { ApiService } from '../../../core/services/api.service';
import { OrderRequestListItem, OrderRequestDetailResponse } from '../../../core/models';

/**
 * The list endpoint deliberately never returns RequestJson/ResponseJson (see
 * docs/api-spec.md §5 -- two blob columns x 200 rows would make the list
 * unusable). Expanding a card lazy-loads the real detail
 * (GET .../order-requests/{id}) the first time it is opened, instead of the
 * pre-R5 shape this card used to assume (requestPayloadJson/responseBodyJson
 * already present on the list row, which the deleted OrderHistoryEntry used
 * to provide).
 */
@Component({
  selector: 'app-order-card',
  standalone: true,
  imports: [CommonModule, JsonViewerComponent],
  template: `
    <div class="order-accordion glass-card" [class.is-cancelled]="isCancelled()">
      <div class="accordion-header" (click)="toggleExpand()">
        <div class="left-meta">
          <span class="status-dot" [class.dot-success]="entry.isSucceeded === true" [class.dot-danger]="entry.isSucceeded === false" [class.dot-warning]="isCancelled()"></span>
          <div class="order-ident">
            <span class="order-code">{{ entry.orderNumber }}</span>
            <span class="order-time">{{ getRelativeTime(entry.orderDate) }}</span>
          </div>
        </div>

        <div class="right-meta">
          <span class="env-pill" *ngIf="entry.branchCode">{{ entry.branchCode }}</span>
          <span class="status-code-pill" [class.code-success]="entry.isSucceeded === true" [class.code-danger]="entry.isSucceeded === false">
            {{ entry.orderStatusLabel || (entry.isSucceeded === false ? 'Failed' : 'Unknown') }}
          </span>
          <i class="bi expand-icon" [class.bi-chevron-down]="!expanded()" [class.bi-chevron-up]="expanded()"></i>
        </div>
      </div>

      <div class="accordion-body" *ngIf="expanded()">
        <div class="meta-grid">
          <div class="meta-item"><span class="meta-label">Net Total:</span> <span>{{ entry.netTotal | number:'1.2-2' }} SAR</span></div>
          <div class="meta-item"><span class="meta-label">Items:</span> <span>{{ entry.itemCount }}</span></div>
          <div class="meta-item" *ngIf="entry.invoiceBarcode"><span class="meta-label">Invoice:</span> <code>{{ entry.invoiceBarcode }}</code></div>
          <div class="meta-item"><span class="meta-label">Exact Timestamp:</span> <span>{{ entry.orderDate | date:'medium' }}</span></div>
        </div>

        @if (loadingDetail()) {
          <p class="detail-loading">Loading request/response detail...</p>
        } @else if (detail(); as d) {
          <div class="danger-banner" *ngIf="d.request.exceptionMessage">
            <strong>Exception:</strong> {{ d.request.exceptionMessage }}
          </div>
          <div class="json-sections">
            <app-json-viewer [data]="d.request.requestJson" title="Request Payload JSON"></app-json-viewer>
            <app-json-viewer *ngIf="d.request.responseJson" [data]="d.request.responseJson" title="Response Body JSON"></app-json-viewer>
          </div>

          <div class="card-actions">
            <button type="button" class="btn-action glass-card" [disabled]="!d.request.header?.canResend" (click)="resend.emit(entry)">
              <i class="bi bi-arrow-repeat"></i> Resend Order
            </button>
            <button type="button" class="btn-action glass-card danger" [disabled]="!d.request.header?.canCancel" (click)="openCancelModal.emit(entry)">
              <i class="bi bi-x-circle"></i> Cancel Order
            </button>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .order-accordion { margin-bottom: 16px; border-left: 4px solid var(--primary); transition: all var(--transition-fast); }
    .order-accordion.is-cancelled { border-left-color: var(--warning); opacity: 0.85; }
    .accordion-header { display: flex; justify-content: space-between; align-items: center; padding: 18px 24px; cursor: pointer; }
    .left-meta { display: flex; align-items: center; gap: 16px; }
    .status-dot { width: 10px; height: 10px; border-radius: 50%; background: var(--text-muted); }
    .dot-success { background: var(--success); box-shadow: 0 0 10px var(--success); }
    .dot-danger { background: var(--danger); box-shadow: 0 0 10px var(--danger); }
    .dot-warning { background: var(--warning); box-shadow: 0 0 10px var(--warning); }
    .order-ident { display: flex; flex-direction: column; }
    .order-code { font-weight: 700; font-size: 1rem; color: var(--text-primary); }
    .order-time { font-size: 0.75rem; color: var(--text-muted); }
    .right-meta { display: flex; align-items: center; gap: 12px; }
    .env-pill { font-size: 0.75rem; padding: 4px 10px; background: var(--bg-tertiary); border-radius: var(--radius-pill); color: var(--text-secondary); }
    .status-code-pill { font-size: 0.8rem; font-weight: 700; padding: 4px 10px; border-radius: var(--radius-sm); }
    .code-success { background: var(--success-bg); color: var(--success); }
    .code-danger { background: var(--danger-bg); color: var(--danger); }
    .expand-icon { color: var(--text-muted); font-size: 1rem; }
    .accordion-body { padding: 0 24px 24px; border-top: 1px solid var(--glass-border); margin-top: 12px; }
    .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; padding: 16px 0; font-size: 0.85rem; }
    .meta-label { font-weight: 600; color: var(--text-muted); margin-right: 6px; }
    .detail-loading { color: var(--text-muted); font-size: 0.85rem; }
    .danger-banner { background: var(--danger-bg); color: var(--danger); padding: 12px 16px; border-radius: var(--radius-sm); margin-bottom: 12px; font-size: 0.85rem; }
    .card-actions { display: flex; gap: 12px; margin-top: 16px; justify-content: flex-end; }
    .btn-action { display: flex; align-items: center; gap: 8px; padding: 8px 16px; font-size: 0.85rem; cursor: pointer; border-radius: var(--radius-md); }
    .btn-action.danger { color: var(--danger); border-color: var(--danger); }
    .btn-action:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class OrderCardComponent implements OnChanges {
  private api = inject(ApiService);

  @Input({ required: true }) entry!: OrderRequestListItem;
  @Input({ required: true }) moduleKey!: string;
  @Output() resend = new EventEmitter<OrderRequestListItem>();
  @Output() openCancelModal = new EventEmitter<OrderRequestListItem>();

  expanded = signal<boolean>(false);
  loadingDetail = signal<boolean>(false);
  detail = signal<OrderRequestDetailResponse | null>(null);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['entry'] && !changes['entry'].firstChange) {
      // A different row (or a refreshed copy of the same row) was bound in;
      // any cached detail is now stale.
      this.detail.set(null);
    }
  }

  isCancelled(): boolean {
    return this.entry.orderStatus === 6 || this.entry.orderStatus === 7;
  }

  toggleExpand() {
    this.expanded.update(e => !e);
    if (this.expanded() && !this.detail() && !this.loadingDetail()) {
      this.loadDetail();
    }
  }

  private loadDetail() {
    this.loadingDetail.set(true);
    this.api.get<OrderRequestDetailResponse>(`modules/${this.moduleKey}/order-requests/${this.entry.id}`).subscribe({
      next: res => {
        this.detail.set(res);
        this.loadingDetail.set(false);
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => this.loadingDetail.set(false)
    });
  }

  getRelativeTime(timestampStr: string): string {
    if (!timestampStr) return '';
    const date = new Date(timestampStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / (1000 * 60));

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} min ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours} hours ago`;
    return date.toLocaleDateString();
  }
}
