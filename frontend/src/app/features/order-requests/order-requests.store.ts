import { Injectable, inject, signal, computed } from '@angular/core';
import { ApiService, ApiParams } from '../../core/services/api.service';
import {
  OrderRequestListItem, OrderRequestListResponse, OrderRequestStats,
  BranchSummary, OrderRequestDetailResponse
} from '../../core/models';

export type LoadStatus = 'idle' | 'loading' | 'ready' | 'empty' | 'error';
export type OutcomeFilter = 'all' | 'succeeded' | 'failed';
export type QuickRange = 'today' | '7d' | '30d';

export interface OrderRequestsFilterState {
  search: string;
  phone: string;
  branchCode: string | null;
  statuses: number[];
  outcome: OutcomeFilter;
  dateFrom: string | null; // yyyy-MM-dd
  dateTo: string | null;
}

export const EMPTY_FILTERS: OrderRequestsFilterState = {
  search: '', phone: '', branchCode: null, statuses: [], outcome: 'all', dateFrom: null, dateTo: null
};

/**
 * Route-scoped signal store for the Order Requests page (R9,
 * remediation_plan.md B14). Provided on OrderRequestsComponent (not root),
 * so the routed detail-drawer child (features/order-requests/components/
 * order-request-drawer.component.ts) shares the same instance via normal
 * Angular DI inheritance, and state resets on navigation away.
 */
@Injectable()
export class OrderRequestsStore {
  private api = inject(ApiService);

  moduleKey = signal('');
  envKey = signal<string | null>(null);

  filters = signal<OrderRequestsFilterState>(EMPTY_FILTERS);
  page = signal(1);
  pageSize = signal(25);
  sort = signal<string | null>(null);

  items = signal<OrderRequestListItem[]>([]);
  stats = signal<OrderRequestStats | null>(null);
  total = signal(0);
  status = signal<LoadStatus>('idle');
  branches = signal<BranchSummary[]>([]);

  selectedId = signal<number | null>(null);
  selected = signal<OrderRequestDetailResponse | null>(null);
  detailStatus = signal<LoadStatus>('idle');

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / (this.pageSize() || 1))));

  hasActiveFilters = computed(() => {
    const f = this.filters();
    return !!f.search || !!f.phone || !!f.branchCode || f.statuses.length > 0 || f.outcome !== 'all' || !!f.dateFrom || !!f.dateTo;
  });

  /** Chips describing every active filter, for the removable-chip row. */
  activeFilterChips = computed(() => {
    const f = this.filters();
    const chips: { key: string; label: string }[] = [];
    if (f.search) chips.push({ key: 'search', label: `Search: ${f.search}` });
    if (f.phone) chips.push({ key: 'phone', label: `Phone: ${f.phone}` });
    if (f.branchCode) chips.push({ key: 'branchCode', label: `Branch: ${f.branchCode}` });
    for (const s of f.statuses) chips.push({ key: `status:${s}`, label: `Status: ${s}` });
    if (f.outcome !== 'all') chips.push({ key: 'outcome', label: f.outcome === 'succeeded' ? 'Succeeded' : 'Failed' });
    if (f.dateFrom) chips.push({ key: 'dateFrom', label: `From: ${f.dateFrom}` });
    if (f.dateTo) chips.push({ key: 'dateTo', label: `To: ${f.dateTo}` });
    return chips;
  });

  private requestToken = 0;
  private detailToken = 0;

  init(moduleKey: string, envKey: string | null, filters: OrderRequestsFilterState, page: number, pageSize: number) {
    this.moduleKey.set(moduleKey);
    this.envKey.set(envKey);
    this.filters.set(filters);
    this.page.set(page);
    this.pageSize.set(pageSize);
    this.loadBranches();
    this.load();
  }

  setFilters(patch: Partial<OrderRequestsFilterState>) {
    this.filters.update(f => ({ ...f, ...patch }));
    this.page.set(1);
    this.load();
  }

  clearFilters() {
    this.filters.set(EMPTY_FILTERS);
    this.page.set(1);
    this.load();
  }

  removeFilterChip(key: string) {
    const f = this.filters();
    if (key.startsWith('status:')) {
      const status = Number(key.split(':')[1]);
      this.setFilters({ statuses: f.statuses.filter(s => s !== status) });
      return;
    }
    switch (key) {
      case 'search': this.setFilters({ search: '' }); break;
      case 'phone': this.setFilters({ phone: '' }); break;
      case 'branchCode': this.setFilters({ branchCode: null }); break;
      case 'outcome': this.setFilters({ outcome: 'all' }); break;
      case 'dateFrom': this.setFilters({ dateFrom: null }); break;
      case 'dateTo': this.setFilters({ dateTo: null }); break;
    }
  }

  applyQuickRange(range: QuickRange) {
    const today = new Date();
    const to = today.toISOString().slice(0, 10);
    const from = new Date(today);
    if (range === '7d') from.setDate(from.getDate() - 6);
    else if (range === '30d') from.setDate(from.getDate() - 29);
    this.setFilters({ dateFrom: from.toISOString().slice(0, 10), dateTo: to });
  }

  /** Applied when a stat tile is clicked -- same base filter as the list,
   * per R9 step 3. */
  applyStatTile(kind: 'requests' | 'succeeded' | 'failed' | 'cancelled') {
    if (kind === 'requests') { this.setFilters({ outcome: 'all', statuses: [] }); return; }
    if (kind === 'succeeded') { this.setFilters({ outcome: 'succeeded', statuses: [] }); return; }
    if (kind === 'failed') { this.setFilters({ outcome: 'failed', statuses: [] }); return; }
    this.setFilters({ outcome: 'all', statuses: [6, 7] });
  }

  setPage(page: number) { this.page.set(page); this.load(); }
  setPageSize(size: number) { this.pageSize.set(size); this.page.set(1); this.load(); }
  setEnvKey(envKey: string | null) { this.envKey.set(envKey); this.load(); this.loadBranches(); }

  refresh() { this.load(); }

  load() {
    const key = this.moduleKey();
    if (!key) return;

    const token = ++this.requestToken;
    this.status.set('loading');

    const f = this.filters();
    const params: ApiParams = {
      orderNumber: f.search || undefined,
      phone: f.phone || undefined,
      branchCode: f.branchCode || undefined,
      statuses: f.statuses.length ? f.statuses : undefined,
      succeeded: f.outcome === 'succeeded' ? true : f.outcome === 'failed' ? false : undefined,
      dateFrom: f.dateFrom || undefined,
      dateTo: f.dateTo || undefined,
      page: this.page(),
      pageSize: this.pageSize(),
      sort: this.sort() || undefined,
      envKey: this.envKey() || undefined
    };

    this.api.get<OrderRequestListResponse>(`modules/${key}/order-requests`, params).subscribe({
      next: res => {
        if (token !== this.requestToken) return; // a newer request already superseded this one
        this.items.set(res.items);
        this.stats.set(res.stats);
        this.total.set(res.total);
        this.status.set(res.items.length === 0 ? 'empty' : 'ready');
      },
      error: () => {
        if (token !== this.requestToken) return;
        this.status.set('error');
      }
    });
  }

  loadBranches() {
    const key = this.moduleKey();
    if (!key) return;
    this.api.get<BranchSummary[]>(`modules/${key}/order-requests/branches`, { envKey: this.envKey() || undefined }).subscribe({
      next: res => this.branches.set(res),
      error: () => {}
    });
  }

  openDetail(id: number) {
    this.selectedId.set(id);
    const key = this.moduleKey();
    const token = ++this.detailToken;
    this.detailStatus.set('loading');
    this.selected.set(null);

    this.api.get<OrderRequestDetailResponse>(`modules/${key}/order-requests/${id}`, { envKey: this.envKey() || undefined }).subscribe({
      next: res => {
        if (token !== this.detailToken) return;
        this.selected.set(res);
        this.detailStatus.set('ready');
      },
      error: () => {
        if (token !== this.detailToken) return;
        this.detailStatus.set('error');
      }
    });
  }

  closeDetail() {
    this.selectedId.set(null);
    this.selected.set(null);
    this.detailStatus.set('idle');
  }

  /** Refreshes just the open detail in place (post-cancel/resend) without a
   * full list refetch, and patches the matching list row so the table
   * reflects the new status immediately. */
  refreshDetailInPlace(detail: OrderRequestDetailResponse) {
    this.selected.set(detail);
    this.items.update(items => items.map(item => item.id === detail.request.id ? {
      ...item,
      isSucceeded: detail.request.isSucceeded,
      orderStatus: detail.request.header?.orderStatus ?? item.orderStatus,
      orderStatusLabel: detail.request.header?.orderStatusLabel ?? item.orderStatusLabel
    } : item));
  }
}
