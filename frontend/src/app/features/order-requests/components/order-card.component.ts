import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JsonViewerComponent } from '../../../shared/components/json-viewer/json-viewer.component';

@Component({
  selector: 'app-order-card',
  standalone: true,
  imports: [CommonModule, JsonViewerComponent],
  template: `
    <div class="order-accordion glass-card" [class.is-cancelled]="entry.isCancelled">
      <div class="accordion-header" (click)="toggleExpand()">
        <div class="left-meta">
          <span class="status-dot" [class.dot-success]="isSuccess()" [class.dot-danger]="isFailed()" [class.dot-warning]="entry.isCancelled"></span>
          <div class="order-ident">
            <span class="order-code">{{ entry.orderCode }}</span>
            <span class="order-time">{{ getRelativeTime(entry.timestamp) }}</span>
          </div>
        </div>

        <div class="right-meta">
          <span class="env-pill">{{ entry.environmentKey }}</span>
          <span class="status-code-pill" [class.code-success]="isSuccess()" [class.code-danger]="isFailed()">
            {{ entry.responseStatusCode || 'ERR' }}
          </span>
          <i class="bi expand-icon" [class.bi-chevron-down]="!expanded()" [class.bi-chevron-up]="expanded()"></i>
        </div>
      </div>

      <div class="accordion-body" *ngIf="expanded()">
        <div class="meta-grid">
          <div class="meta-item"><span class="meta-label">API Endpoint:</span> <code>{{ entry.apiUrl }}</code></div>
          <div class="meta-item"><span class="meta-label">Exact Timestamp:</span> <span>{{ entry.timestamp | date:'medium' }}</span></div>
        </div>

        <div class="json-sections">
          <app-json-viewer [data]="entry.requestPayloadJson" title="Request Payload JSON"></app-json-viewer>
          <app-json-viewer [data]="entry.responseBodyJson" title="Response Body JSON"></app-json-viewer>
          <app-json-viewer *ngIf="entry.cancelResponseJson" [data]="entry.cancelResponseJson" title="Cancellation Response JSON"></app-json-viewer>
        </div>

        <div class="card-actions">
          <button type="button" class="btn-action glass-card" (click)="resend.emit(entry)">
            <i class="bi bi-arrow-repeat"></i> Resend Order
          </button>
          <button type="button" class="btn-action glass-card danger" *ngIf="!entry.isCancelled" (click)="openCancelModal.emit(entry)">
            <i class="bi bi-x-circle"></i> Cancel Order
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .order-accordion { margin-bottom: 16px; border-left: 4px solid var(--primary); transition: all var(--transition-fast); }
    .order-accordion.is-cancelled { border-left-color: var(--warning); opacity: 0.85; }
    .accordion-header { display: flex; justify-content: space-between; align-items: center; padding: 18px 24px; cursor: pointer; }
    .left-meta { display: flex; align-items: center; gap: 16px; }
    .status-dot { width: 10px; height: 10px; border-radius: 50%; }
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
    .card-actions { display: flex; gap: 12px; margin-top: 16px; justify-content: flex-end; }
    .btn-action { display: flex; align-items: center; gap: 8px; padding: 8px 16px; font-size: 0.85rem; cursor: pointer; border-radius: var(--radius-md); }
    .btn-action.danger { color: var(--danger); border-color: var(--danger); }
  `]
})
export class OrderCardComponent {
  @Input() entry: any;
  @Output() resend = new EventEmitter<any>();
  @Output() openCancelModal = new EventEmitter<any>();

  expanded = signal<boolean>(false);

  toggleExpand() {
    this.expanded.update(e => !e);
  }

  isSuccess(): boolean {
    const code = this.entry?.responseStatusCode;
    return code >= 200 && code < 300;
  }

  isFailed(): boolean {
    return !this.isSuccess();
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
