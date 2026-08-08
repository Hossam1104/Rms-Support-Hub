import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { OrderRequestsStore } from '../order-requests.store';
import { OrderRequestListItem } from '../../../core/models';
import { StatusPillComponent, RiyalComponent, SkeletonComponent, EmptyStateComponent, UiCardComponent } from '../../../shared/ui';

const CANCELLED_STATUSES = new Set([6, 7]);

@Component({
  selector: 'app-requests-table',
  standalone: true,
  imports: [CommonModule, ScrollingModule, StatusPillComponent, RiyalComponent, SkeletonComponent, EmptyStateComponent, UiCardComponent],
  template: `
    <ui-card variant="raised" class="table-card">
      <div class="table-shell" [class.is-refreshing]="store.status() === 'loading'" [style.--table-row-height.px]="rowHeight">
      <div class="table-head">
        <span class="col-outcome"></span>
        <span class="col-order">Order #</span>
        <span class="col-date">Date</span>
        <span class="col-branch">Branch</span>
        <span class="col-status">Status</span>
        <span class="col-items">Items</span>
        <span class="col-total align-right">Net total</span>
        <span class="col-invoice">Invoice</span>
        <span class="col-payload">Payload</span>
        <span class="col-action"></span>
      </div>

      @if (store.status() === 'loading' && store.items().length === 0) {
        <div class="skeleton-rows">
          @for (i of [1,2,3,4,5,6]; track i) {
            <div class="table-row skeleton-row">
              <app-skeleton width="90%" height="12px"></app-skeleton>
            </div>
          }
        </div>
      } @else if (store.status() === 'error' && store.items().length === 0) {
        <app-empty-state icon="bi-exclamation-octagon" title="Couldn't load order requests" [description]="store.errorMessage() || 'The database may be unreachable. Try again.'">
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
        @if (store.status() === 'error') {
          <div class="stale-error" role="alert">
            <i class="bi bi-exclamation-triangle" aria-hidden="true"></i>
            <span>{{ store.errorMessage() || 'The refresh failed. Showing the last successful results.' }}</span>
            <button type="button" class="retry-btn" (click)="store.refresh()">Retry</button>
          </div>
        }
        <cdk-virtual-scroll-viewport [itemSize]="rowHeight" class="table-body" [style.height.px]="tableHeight()" [class.dimmed]="store.status() === 'loading'">
          <button
            type="button"
            *cdkVirtualFor="let row of store.items(); let i = index"
            class="table-row"
            [attr.aria-label]="'Open order request ' + row.orderNumber + ' (' + row.id + ')'"
            [class.row-failed]="row.isSucceeded === false"
            [class.row-cancelled]="isCancelled(row)"
            [style.animationDelay.ms]="Math.min(i, 20) * 30"
            (click)="openDetail(row)">
            <span class="outcome-dot col-outcome" [class.dot-success]="row.isSucceeded === true" [class.dot-danger]="row.isSucceeded === false"></span>
            <span class="order-number col-order" [class.struck]="isCancelled(row)">{{ row.orderNumber }}</span>
            <span class="cell-date col-date">
              <span class="date-abs">{{ row.orderDate | date:'short' }}</span>
              <span class="date-rel">{{ relativeTime(row.orderDate) }}</span>
            </span>
            <span class="cell-branch col-branch">
              <span>{{ row.branchCode || '—' }}</span>
              <span class="branch-name" *ngIf="row.branchName">{{ row.branchName }}</span>
            </span>
            <span class="col-status">
              <app-status-pill *ngIf="row.orderStatus as s" [status]="s" [label]="row.orderStatusLabel || undefined"></app-status-pill>
              <span *ngIf="!row.orderStatus" class="no-header">No header</span>
            </span>
            <span class="col-items">{{ row.itemCount }}</span>
            <span class="align-right net-total col-total"><app-riyal [size]="0.85"></app-riyal>{{ row.netTotal | number:'1.2-2' }}</span>
            <span class="cell-invoice col-invoice">{{ row.invoiceBarcode || '—' }}</span>
            <span class="payload-badges col-payload">
              <span class="badge">REQ</span>
              <span class="badge" [class.badge-lit]="row.hasResponse">RES</span>
            </span>
            <span class="row-action col-action"><i class="bi bi-chevron-right"></i></span>
          </button>
        </cdk-virtual-scroll-viewport>
      }
      </div>
    </ui-card>
  `,
  styles: [`
    .table-shell { overflow-x: auto; overflow-y: hidden; margin-top: var(--table-inset-block); border: 1px solid var(--table-border); border-radius: var(--radius-md); background: var(--surface-panel); scrollbar-color: var(--border-strong) transparent; }
    .table-shell.is-refreshing { cursor: progress; }
    .table-head, .table-row {
      display: grid;
      grid-template-columns: 18px minmax(150px, 1.25fr) minmax(135px, 1.05fr) minmax(120px, 1fr) minmax(132px, 1.1fr) 58px minmax(125px, 1fr) minmax(120px, 1fr) 82px 28px;
      align-items: center;
      gap: var(--panel-gap);
      padding: 0 var(--table-cell-padding-x);
      min-width: 1090px;
    }
    .table-head { height: var(--table-header-height); border-bottom: 1px solid var(--table-border); background: var(--table-header-surface); font-size: var(--text-xs); font-weight: 800; color: var(--text-secondary); text-transform: uppercase; letter-spacing: .06em; }
    .table-body { min-width: 1090px; transition: opacity var(--transition-normal); }
    .table-body.dimmed { opacity: 0.55; }
    .stale-error { display: flex; align-items: center; flex-wrap: wrap; gap: 9px; padding: 10px 14px; border-bottom: 1px solid var(--state-danger-border); background: var(--state-danger-bg); color: var(--state-danger-fg); font-size: .76rem; }
    .stale-error span { flex: 1 1 260px; }
    .table-row {
      height: var(--table-row-height);
      width: 100%;
      box-sizing: border-box;
      border-bottom: 1px solid var(--table-row-border);
      border-top: 0;
      border-left: 0;
      border-right: 0;
      background: transparent;
      cursor: pointer;
      font-size: 0.85rem;
      color: var(--text-primary);
      font-family: inherit;
      text-align: left;
      transition: background var(--transition-fast);
      animation: rowStaggerIn var(--d-slow) var(--ease-spring) backwards;
    }
    .table-row > span { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .table-row:hover { background: var(--table-row-hover); }
    .table-row:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .table-row.row-failed { border-left: 3px solid var(--state-danger-border); background: var(--state-danger-bg); }
    .table-row.row-cancelled { opacity: 0.65; }
    @keyframes rowStaggerIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
    .skeleton-row { display: flex; align-items: center; }
    .skeleton-row app-skeleton { flex: 1; }
    .outcome-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--text-muted); }
    .dot-success { background: var(--state-success-fg); box-shadow: 0 0 8px var(--state-success-fg); }
    .dot-danger { background: var(--state-danger-fg); box-shadow: 0 0 8px var(--state-danger-fg); }
    .order-number { font-family: 'JetBrains Mono', monospace; font-weight: 600; }
    .order-number.struck { text-decoration: line-through; color: var(--text-muted); }
    .cell-date { display: flex; flex-direction: column; }
    .date-abs { font-size: 0.82rem; }
    .date-rel { font-size: 0.72rem; color: var(--text-muted); }
    .cell-branch { display: flex; flex-direction: column; }
    .branch-name { font-size: 0.72rem; color: var(--text-muted); }
    .no-header { font-size: 0.75rem; color: var(--text-muted); font-style: italic; }
    .align-right, .col-items { text-align: right; font-variant-numeric: tabular-nums; }
    .net-total { display: flex; align-items: center; justify-content: flex-end; gap: 4px; font-weight: 600; }
    .cell-invoice { font-size: 0.78rem; color: var(--text-secondary); }
    .payload-badges { display: flex; gap: 4px; }
    .badge { font-size: 0.65rem; font-weight: 700; padding: 2px 6px; border-radius: var(--radius-sm); background: var(--surface-raised); color: var(--text-muted); }
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
  readonly rowHeight = 44;

  tableHeight(): number {
    // Keep short result sets compact while retaining a stable viewport for
    // the normal 25-row page. The virtual scroller still caps the table at
    // the existing 560px rail height for larger pages.
    return Math.min(560, Math.max(176, Math.max(this.store.items().length, 4) * this.rowHeight));
  }

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
