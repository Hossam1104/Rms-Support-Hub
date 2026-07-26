import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { OrderRequestsStore } from '../order-requests.store';
import { OrderRequestListItem } from '../../../core/models';
import { StatusPillComponent, RiyalComponent, SkeletonComponent, EmptyStateComponent } from '../../../shared/ui';

const CANCELLED_STATUSES = new Set([6, 7]);

@Component({
  selector: 'app-requests-table',
  standalone: true,
  imports: [CommonModule, ScrollingModule, StatusPillComponent, RiyalComponent, SkeletonComponent, EmptyStateComponent],
  template: `
    <div class="table-shell">
      <div class="table-head">
        <span></span>
        <span>Order #</span>
        <span>Date</span>
        <span>Branch</span>
        <span>Status</span>
        <span>Items</span>
        <span class="align-right">Net total</span>
        <span>Invoice</span>
        <span>Payload</span>
        <span></span>
      </div>

      @if (store.status() === 'loading' && store.items().length === 0) {
        <div class="skeleton-rows">
          @for (i of [1,2,3,4,5,6]; track i) {
            <div class="table-row skeleton-row">
              <app-skeleton width="90%" height="12px"></app-skeleton>
            </div>
          }
        </div>
      } @else if (store.status() === 'error') {
        <app-empty-state icon="bi-exclamation-octagon" title="Couldn't load order requests" description="The database may be unreachable. Try again.">
          <button type="button" class="retry-btn" (click)="store.refresh()">Retry</button>
        </app-empty-state>
      } @else if (store.status() === 'empty') {
        <app-empty-state
          [icon]="store.hasActiveFilters() ? 'bi-funnel' : 'bi-inbox'"
          [title]="store.hasActiveFilters() ? 'No requests match these filters' : 'No order requests recorded yet'"
          [description]="store.hasActiveFilters() ? '' : 'Orders sent from this module will appear here automatically.'">
          <button type="button" class="retry-btn" *ngIf="store.hasActiveFilters()" (click)="store.clearFilters()">Clear filters</button>
        </app-empty-state>
      } @else {
        <cdk-virtual-scroll-viewport itemSize="56" class="table-body" [class.dimmed]="store.status() === 'loading'">
          <div
            *cdkVirtualFor="let row of store.items(); let i = index"
            class="table-row"
            [class.row-failed]="row.isSucceeded === false"
            [class.row-cancelled]="isCancelled(row)"
            [style.animationDelay.ms]="Math.min(i, 20) * 30"
            (click)="openDetail(row)">
            <span class="outcome-dot" [class.dot-success]="row.isSucceeded === true" [class.dot-danger]="row.isSucceeded === false"></span>
            <span class="order-number" [class.struck]="isCancelled(row)">{{ row.orderNumber }}</span>
            <span class="cell-date">
              <span class="date-abs">{{ row.orderDate | date:'short' }}</span>
              <span class="date-rel">{{ relativeTime(row.orderDate) }}</span>
            </span>
            <span class="cell-branch">
              <span>{{ row.branchCode || '—' }}</span>
              <span class="branch-name" *ngIf="row.branchName">{{ row.branchName }}</span>
            </span>
            <span>
              <app-status-pill *ngIf="row.orderStatus as s" [status]="s" [label]="row.orderStatusLabel || undefined"></app-status-pill>
              <span *ngIf="!row.orderStatus" class="no-header">No header</span>
            </span>
            <span>{{ row.itemCount }}</span>
            <span class="align-right net-total">{{ row.netTotal | number:'1.2-2' }} <app-riyal [size]="0.85"></app-riyal></span>
            <span class="cell-invoice">{{ row.invoiceBarcode || '—' }}</span>
            <span class="payload-badges">
              <span class="badge">REQ</span>
              <span class="badge" [class.badge-lit]="row.hasResponse">RES</span>
            </span>
            <span class="row-action"><i class="bi bi-chevron-right"></i></span>
          </div>
        </cdk-virtual-scroll-viewport>
      }
    </div>
  `,
  styles: [`
    .table-shell { border: 1px solid var(--glass-border); border-radius: var(--radius-lg); overflow: hidden; background: var(--bg-secondary); }
    .table-head, .table-row {
      display: grid;
      grid-template-columns: 20px 1.1fr 1.1fr 1fr 1.1fr 0.6fr 1fr 1fr 0.9fr 24px;
      align-items: center;
      gap: 10px;
      padding: 0 16px;
    }
    .table-head { height: 42px; background: var(--bg-tertiary); font-size: 0.72rem; font-weight: 700; color: var(--text-secondary); text-transform: uppercase; letter-spacing: .02em; }
    .table-body { height: 560px; transition: opacity var(--transition-normal); }
    .table-body.dimmed { opacity: 0.55; }
    .table-row {
      height: 56px;
      border-bottom: 1px solid var(--glass-border);
      cursor: pointer;
      font-size: 0.85rem;
      color: var(--text-primary);
      transition: background var(--transition-fast);
      animation: rowStaggerIn var(--d-slow) var(--ease-spring) backwards;
    }
    .table-row:hover { background: var(--glass-hover-bg); }
    .table-row.row-failed { border-left: 3px solid transparent; background-image: linear-gradient(90deg, rgba(220,38,38,.12), transparent 12%); }
    .table-row.row-cancelled { opacity: 0.65; }
    @keyframes rowStaggerIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
    .skeleton-row { display: flex; align-items: center; }
    .outcome-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--text-muted); }
    .dot-success { background: var(--success); box-shadow: 0 0 8px var(--success); }
    .dot-danger { background: var(--danger); box-shadow: 0 0 8px var(--danger); }
    .order-number { font-family: 'JetBrains Mono', monospace; font-weight: 600; }
    .order-number.struck { text-decoration: line-through; color: var(--text-muted); }
    .cell-date { display: flex; flex-direction: column; }
    .date-abs { font-size: 0.82rem; }
    .date-rel { font-size: 0.72rem; color: var(--text-muted); }
    .cell-branch { display: flex; flex-direction: column; }
    .branch-name { font-size: 0.72rem; color: var(--text-muted); }
    .no-header { font-size: 0.75rem; color: var(--text-muted); font-style: italic; }
    .align-right { text-align: right; }
    .net-total { display: flex; align-items: center; justify-content: flex-end; gap: 4px; font-weight: 600; }
    .cell-invoice { font-size: 0.78rem; color: var(--text-secondary); }
    .payload-badges { display: flex; gap: 4px; }
    .badge { font-size: 0.65rem; font-weight: 700; padding: 2px 6px; border-radius: var(--radius-sm); background: var(--bg-tertiary); color: var(--text-muted); }
    .badge-lit { background: var(--grad-info); color: var(--on-gradient); }
    .row-action { color: var(--text-muted); }
    .retry-btn { background: var(--grad-brand); color: var(--on-gradient); border: none; border-radius: var(--radius-md); padding: 8px 18px; cursor: pointer; font-weight: 600; }
  `]
})
export class RequestsTableComponent {
  store = inject(OrderRequestsStore);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  readonly Math = Math;

  isCancelled(row: OrderRequestListItem): boolean {
    return row.orderStatus != null && CANCELLED_STATUSES.has(row.orderStatus);
  }

  openDetail(row: OrderRequestListItem) {
    this.router.navigate([row.id], { relativeTo: this.route });
  }

  relativeTime(dateStr: string): string {
    const date = new Date(dateStr);
    const diffMins = Math.floor((Date.now() - date.getTime()) / 60000);
    if (diffMins < 1) return 'just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    return `${Math.floor(diffHours / 24)}d ago`;
  }
}
