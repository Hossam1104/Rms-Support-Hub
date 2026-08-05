import { Injectable, inject, signal, computed } from '@angular/core';
import { Subscription, timeout } from 'rxjs';
import { ApiService, ApiParams } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import {
  OrderRequestListItem, OrderRequestListResponse, OrderRequestStats,
  BranchOption, LookupResult, OrderRequestDetailResponse
} from '../../core/models';

export type LoadStatus = 'idle' | 'loading' | 'ready' | 'empty' | 'error';
export type OutcomeFilter = 'all' | 'succeeded' | 'failed';
export type QuickRange = 'today' | '7d' | '30d';

export interface OrderRequestsFilterState {
  search: string;
  exactMatch: boolean;
  phone: string;
  branchCode: string | null;
  statuses: number[];
  outcome: OutcomeFilter;
  dateFrom: string | null; // yyyy-MM-dd
  dateTo: string | null;
}

/**
 * The only source of default filter state. A fresh object and status array
 * are returned for every caller so a draft/reset can never share mutable
 * state with the applied filters or another component instance.
 */
export function createDefaultOrderRequestFilters(): OrderRequestsFilterState {
  return {
    search: '',
    exactMatch: true,
    phone: '',
    branchCode: null,
    statuses: [],
    outcome: 'all',
    dateFrom: null,
    dateTo: null
  };
}

/**
 * Route-scoped signal store for the Order Requests page (R9,
 * remediation_plan.md B14). Provided on OrderRequestsComponent (not root),
 * so the routed :orderId detail page shares the same instance via normal
 * Angular DI inheritance, and state resets on navigation away.
 */
@Injectable()
export class OrderRequestsStore {
  private static readonly requestTimeoutMs = 15000;
  private api = inject(ApiService);
  private branchOptions = inject(BranchOptionsService);

  moduleKey = signal('');
  envKey = signal<string | null>(null);

  filters = signal<OrderRequestsFilterState>(createDefaultOrderRequestFilters());
  page = signal(1);
  pageSize = signal(25);
  sort = signal<string | null>(null);

  items = signal<OrderRequestListItem[]>([]);
  stats = signal<OrderRequestStats | null>(null);
  total = signal(0);
  status = signal<LoadStatus>('idle');
  errorMessage = signal<string | null>(null);
  lastUpdatedAt = signal<Date | null>(null);
  autoRefresh = signal(false);
  branches = signal<BranchOption[]>([]);
  branchStatus = signal<LoadStatus>('idle');
  branchError = signal<string | null>(null);

  selectedId = signal<number | null>(null);
  selected = signal<OrderRequestDetailResponse | null>(null);
  detailStatus = signal<LoadStatus>('idle');
  detailError = signal<string | null>(null);

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / (this.pageSize() || 1))));

  hasActiveFilters = computed(() => {
    const f = this.filters();
    return !!f.search || !!f.phone || !!f.branchCode || f.statuses.length > 0 || f.outcome !== 'all' || !!f.dateFrom || !!f.dateTo;
  });

  /** Chips describing every active filter, for the removable-chip row. */
  activeFilterChips = computed(() => {
    const f = this.filters();
    const chips: { key: string; label: string }[] = [];
    if (f.search) chips.push({ key: 'search', label: `${f.exactMatch ? 'Order' : 'Contains'}: ${f.search}` });
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
  private branchLoadToken = 0;
  private listSubscription: Subscription | null = null;
  private detailSubscription: Subscription | null = null;
  private branchSubscription: Subscription | null = null;

  init(moduleKey: string, envKey: string | null, filters: OrderRequestsFilterState, page: number, pageSize: number) {
    this.moduleKey.set(moduleKey);
    this.envKey.set(envKey);
    this.filters.set(this.normalizeFilters(filters));
    this.page.set(page);
    this.pageSize.set(pageSize);
    this.loadBranches();
    this.load();
  }

  setFilters(patch: Partial<OrderRequestsFilterState>) {
    const next = this.normalizeFilters({ ...this.filters(), ...patch });
    if (this.sameFilters(this.filters(), next)) return;

    this.filters.set(next);
    this.page.set(1);
    this.load();
  }

  /** Commits a complete draft from the explicit-apply filter workbench. */
  applyFilters(next: OrderRequestsFilterState) {
    const normalized = this.normalizeFilters(next);
    const changed = !this.sameFilters(this.filters(), normalized) || this.page() !== 1;
    if (!changed) {
      this.refresh();
      return;
    }

    this.filters.set(normalized);
    this.page.set(1);
    this.load();
  }

  /**
   * Resets the applied state and starts one unfiltered request. The explicit
   * force flag is used by the filter-bar Clear All action so a dirty draft is
   * still committed even when the applied store is already on page one with
   * no filters.
   */
  clearFilters(forceReload = false) {
    const defaults = createDefaultOrderRequestFilters();
    const changed = !this.sameFilters(this.filters(), defaults) || this.page() !== 1;
    if (!changed && !forceReload) return;

    // A clear is a new result generation. Drop the old rows/stats before the
    // request starts so a failed reset cannot present filtered data as the
    // current unfiltered result.
    this.items.set([]);
    this.stats.set(null);
    this.total.set(0);
    this.lastUpdatedAt.set(null);
    this.filters.set(defaults);
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

  setPage(page: number) {
    const nextPage = Math.max(1, Math.min(Math.trunc(page) || 1, this.totalPages()));
    if (nextPage === this.page()) return;
    this.page.set(nextPage);
    this.load();
  }

  setPageSize(size: number) {
    const nextSize = [25, 50, 100, 200].includes(Number(size)) ? Number(size) : 25;
    if (nextSize === this.pageSize() && this.page() === 1) return;
    this.pageSize.set(nextSize);
    this.page.set(1);
    this.load();
  }
  setEnvKey(envKey: string | null) { this.envKey.set(envKey); this.load(); this.loadBranches(); }
  setAutoRefresh(enabled: boolean) { this.autoRefresh.set(enabled); }

  refresh() { this.load(); }
  autoRefreshTick() {
    if (this.status() === 'loading') return;
    this.load({ auto: true });
  }

  load(options: { auto?: boolean } = {}) {
    const key = this.moduleKey();
    if (!key) return;
    if (options.auto && this.status() === 'loading') return;

    this.listSubscription?.unsubscribe();
    const token = ++this.requestToken;
    this.status.set('loading');
    this.errorMessage.set(null);

    const f = this.filters();
    const params: ApiParams = {
      orderNumber: f.search || undefined,
      exactMatch: f.search ? f.exactMatch : undefined,
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

    this.listSubscription = this.api.get<OrderRequestListResponse>(`modules/${key}/order-requests`, params).pipe(
      timeout({ each: OrderRequestsStore.requestTimeoutMs })
    ).subscribe({
      next: res => {
        if (token !== this.requestToken) return; // a newer request already superseded this one
        this.items.set(res.items);
        this.stats.set(res.stats);
        this.total.set(res.total);
        if (res.page !== this.page()) this.page.set(res.page);
        this.lastUpdatedAt.set(new Date());
        this.status.set(res.items.length === 0 ? 'empty' : 'ready');
        this.listSubscription = null;
      },
      error: (err) => {
        if (token !== this.requestToken) return;
        this.status.set('error');
        this.errorMessage.set(err?.name === 'TimeoutError'
          ? 'The database took too long to respond. Retry the search or narrow the filters.'
          : err?.status === 400
            ? err?.message || 'Check the search filters and try again.'
            : 'The order requests could not be loaded. Retry the search.');
        this.listSubscription = null;
      }
    });
  }

  loadBranches(refresh = false) {
    const key = this.moduleKey();
    if (!key) return;

    this.branchSubscription?.unsubscribe();
    const token = ++this.branchLoadToken;
    this.branchStatus.set('loading');
    this.branchError.set(null);
    this.branchSubscription = this.branchOptions.list(key, this.envKey(), refresh).pipe(
      timeout({ each: OrderRequestsStore.requestTimeoutMs })
    ).subscribe({
      next: response => {
        if (token !== this.branchLoadToken) return;
        if (response.success) {
          const branches = response.data || [];
          this.branches.set(branches);
          this.branchStatus.set(branches.length ? 'ready' : 'empty');
        } else {
          this.branchStatus.set('error');
          this.branchError.set(response.message || 'Branches could not be loaded.');
        }
        this.branchSubscription = null;
      },
      error: () => {
        if (token !== this.branchLoadToken) return;
        this.branchStatus.set('error');
        this.branchError.set('Branches could not be loaded.');
        this.branchSubscription = null;
      }
    });
  }

  openDetail(id: number) {
    this.selectedId.set(id);
    const key = this.moduleKey();
    const token = ++this.detailToken;
    this.detailSubscription?.unsubscribe();
    this.detailStatus.set('loading');
    this.detailError.set(null);
    this.selected.set(null);

    this.detailSubscription = this.api.get<OrderRequestDetailResponse>(`modules/${key}/order-requests/${id}`, { envKey: this.envKey() || undefined }).pipe(
      timeout({ each: OrderRequestsStore.requestTimeoutMs })
    ).subscribe({
      next: res => {
        if (token !== this.detailToken) return;
        this.selected.set(res);
        this.detailStatus.set('ready');
        this.detailSubscription = null;
      },
      error: (err) => {
        if (token !== this.detailToken) return;
        this.detailStatus.set('error');
        this.detailError.set(err?.status === 404
          ? 'This request was not found in the selected environment.'
          : err?.message || 'The order request could not be loaded.');
        this.detailSubscription = null;
      }
    });
  }

  closeDetail() {
    this.selectedId.set(null);
    this.selected.set(null);
    this.detailStatus.set('idle');
    this.detailError.set(null);
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

  private sameFilters(left: OrderRequestsFilterState, right: OrderRequestsFilterState): boolean {
    return left.search === right.search
      && left.exactMatch === right.exactMatch
      && left.phone === right.phone
      && left.branchCode === right.branchCode
      && left.outcome === right.outcome
      && left.dateFrom === right.dateFrom
      && left.dateTo === right.dateTo
      && left.statuses.length === right.statuses.length
      && left.statuses.every((status, index) => status === right.statuses[index]);
  }

  private normalizeFilters(filters: OrderRequestsFilterState): OrderRequestsFilterState {
    return {
      ...filters,
      search: filters.search.trim(),
      phone: filters.phone.trim(),
      branchCode: filters.branchCode?.trim() || null,
      statuses: [...new Set(filters.statuses)].sort((left, right) => left - right)
    };
  }
}
