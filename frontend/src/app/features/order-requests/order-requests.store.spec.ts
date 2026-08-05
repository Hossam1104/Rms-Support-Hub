import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import { OrderRequestListResponse } from '../../core/models';
import { createDefaultOrderRequestFilters, OrderRequestsFilterState, OrderRequestsStore } from './order-requests.store';

function response(items: OrderRequestListResponse['items'] = []): OrderRequestListResponse {
  return { items, page: 1, pageSize: 25, total: items.length, totalPages: items.length ? 1 : 0, stats: { total: items.length, succeeded: 0, failed: 0, cancelled: 0 } };
}

function definedParams(params: unknown): Record<string, unknown> {
  return Object.fromEntries(Object.entries(params as Record<string, unknown>).filter(([, value]) => value !== undefined));
}

const clearAllScenarios: Array<{
  name: string;
  filters: Partial<OrderRequestsFilterState>;
  page?: number;
  mode?: 'normal' | 'manual-refresh' | 'auto-refresh' | 'in-flight' | 'error';
}> = [
  { name: 'order number only', filters: { search: 'ORD_TEST_015' } },
  { name: 'phone only', filters: { phone: '0556028080' } },
  { name: 'branch only', filters: { branchCode: 'P001' } },
  { name: 'outcome only', filters: { outcome: 'failed' } },
  { name: 'one status', filters: { statuses: [3] } },
  { name: 'multiple statuses', filters: { statuses: [3, 7] } },
  { name: 'date from only', filters: { dateFrom: '2026-08-01' } },
  { name: 'date to only', filters: { dateTo: '2026-08-05' } },
  { name: 'full date range', filters: { dateFrom: '2026-08-01', dateTo: '2026-08-05' } },
  { name: 'exact match changed from default', filters: { search: 'ORD_TEST_015', exactMatch: false } },
  { name: 'multiple combined filters', filters: { search: 'ORD_TEST_015', phone: '0556028080', branchCode: 'P001', outcome: 'failed', statuses: [3, 7], dateFrom: '2026-08-01', dateTo: '2026-08-05' } },
  { name: 'active filters restored from URL query parameters', filters: { search: 'ORD_TEST_015', branchCode: 'P001', statuses: [3] } },
  { name: 'active filters followed by manual Refresh', filters: { branchCode: 'P001' }, mode: 'manual-refresh' },
  { name: 'active filters while Auto-refresh is enabled', filters: { statuses: [3] }, mode: 'auto-refresh' },
  { name: 'Clear All during an in-flight request', filters: { branchCode: 'P001' }, mode: 'in-flight' },
  { name: 'Clear All after an API error', filters: { statuses: [3] }, mode: 'error' },
  { name: 'Clear All on page 2 or later', filters: { branchCode: 'P001' }, page: 2 },
  { name: 'Clear All in mobile/collapsed filter layout', filters: { outcome: 'failed' } },
  { name: 'Clear All followed by page reload', filters: { dateFrom: '2026-08-01' } },
  { name: 'Clear All followed by browser back/forward', filters: { dateTo: '2026-08-05' } }
];

class MockApiService {
  requests: Array<{ params: unknown; subject: Subject<OrderRequestListResponse>; cancelled: boolean }> = [];

  get = vi.fn((path: string, params: unknown) => {
    if (!path.endsWith('/order-requests')) return of({});

    const pending = { params, subject: new Subject<OrderRequestListResponse>(), cancelled: false };
    this.requests.push(pending);
    return new Observable<OrderRequestListResponse>(subscriber => {
      const subscription = pending.subject.subscribe(subscriber);
      return () => {
        pending.cancelled = true;
        subscription.unsubscribe();
      };
    });
  });
}

describe('OrderRequestsStore list lifecycle', () => {
  let api: MockApiService;
  let store: OrderRequestsStore;

  beforeEach(() => {
    api = new MockApiService();
    TestBed.configureTestingModule({
      providers: [
        OrderRequestsStore,
        { provide: ApiService, useValue: api },
        { provide: BranchOptionsService, useValue: { list: vi.fn(() => of({ success: true, data: [] })) } }
      ]
    });
    store = TestBed.inject(OrderRequestsStore);
  });

  it('creates independent default filter objects and status arrays', () => {
    const first = createDefaultOrderRequestFilters();
    const second = createDefaultOrderRequestFilters();

    expect(first).not.toBe(second);
    expect(first.statuses).not.toBe(second.statuses);
    first.statuses.push(7);

    expect(second.statuses).toEqual([]);
  });

  it('normalizes filters, cancels stale requests, and skips no-op reloads', () => {
    store.init('upc_ecommerce', 'Testing', createDefaultOrderRequestFilters(), 1, 25);
    const first = api.requests[0];

    store.setFilters({ search: '  UPC-123  ', phone: ' 0556028080 ' });

    expect(first.cancelled).toBe(true);
    expect(api.requests).toHaveLength(2);
    expect(api.requests[1].params).toMatchObject({ orderNumber: 'UPC-123', phone: '0556028080' });

    store.setFilters({ search: 'UPC-123' });
    expect(api.requests).toHaveLength(2);

    api.requests[1].subject.next(response([{
      id: 1,
      orderNumber: 'UPC-123',
      orderDate: '2026-08-04T10:00:00Z',
      netTotal: 10,
      itemCount: 1,
      isSucceeded: true,
      requestBytes: 10,
      hasResponse: true,
      orderHeaderId: null,
      branchCode: null,
      branchName: null,
      orderStatus: null,
      orderStatusLabel: null,
      parentOrderNumber: null,
      invoiceBarcode: null,
      invoiceDate: null
    }]));

    expect(store.status()).toBe('ready');
    expect(store.items()[0].orderNumber).toBe('UPC-123');
  });

  it('sends exact-match state only when an order search is active and resets the page on apply', () => {
    store.init('upc_ecommerce', 'Testing', { ...createDefaultOrderRequestFilters(), search: 'old-order' }, 3, 25);

    store.applyFilters({
      ...store.filters(),
      search: '  ORD-42  ',
      exactMatch: false,
      statuses: [9, 1, 9]
    });

    expect(store.page()).toBe(1);
    expect(api.requests.at(-1)?.params).toMatchObject({
      orderNumber: 'ORD-42',
      exactMatch: false,
      statuses: [1, 9]
    });
  });

  it('ends loading on an error and retries with the same applied filters', () => {
    store.init('upc_ecommerce', 'Testing', { ...createDefaultOrderRequestFilters(), branchCode: 'P001' }, 1, 25);
    api.requests[0].subject.error({ status: 502 });

    expect(store.status()).toBe('error');
    expect(store.errorMessage()).toContain('could not be loaded');

    store.refresh();
    expect(store.status()).toBe('loading');
    expect(api.requests.at(-1)?.params).toMatchObject({ branchCode: 'P001' });
    api.requests.at(-1)?.subject.next(response());
    expect(store.status()).toBe('empty');
  });

  it('keeps the last successful rows visible when a background refresh fails', () => {
    store.init('upc_ecommerce', 'Testing', createDefaultOrderRequestFilters(), 1, 25);
    api.requests[0].subject.next(response([{
      id: 7,
      orderNumber: 'ORD-7',
      orderDate: '2026-08-04T10:00:00Z',
      netTotal: 7,
      itemCount: 1,
      isSucceeded: true,
      requestBytes: 10,
      hasResponse: true,
      orderHeaderId: null,
      branchCode: null,
      branchName: null,
      orderStatus: null,
      orderStatusLabel: null,
      parentOrderNumber: null,
      invoiceBarcode: null,
      invoiceDate: null
    }]));

    store.refresh();
    expect(store.status()).toBe('loading');
    expect(store.items()).toHaveLength(1);
    api.requests.at(-1)?.subject.error({ status: 502 });

    expect(store.status()).toBe('error');
    expect(store.items()[0].orderNumber).toBe('ORD-7');
  });

  it('clears the applied state once, invalidates the old request, and removes stale rows before loading unfiltered data', () => {
    store.init('upc_ecommerce', 'Testing', { ...createDefaultOrderRequestFilters(), branchCode: 'P001', statuses: [3] }, 1, 50);
    api.requests[0].subject.next(response([{
      id: 12,
      orderNumber: 'FILTERED-12',
      orderDate: '2026-08-04T10:00:00Z',
      netTotal: 12,
      itemCount: 1,
      isSucceeded: false,
      requestBytes: 10,
      hasResponse: true,
      orderHeaderId: null,
      branchCode: 'P001',
      branchName: 'Test branch',
      orderStatus: 3,
      orderStatusLabel: 'Failed',
      parentOrderNumber: null,
      invoiceBarcode: null,
      invoiceDate: null
    }]));
    store.refresh();
    const filteredRequest = api.requests[1];

    store.clearFilters();

    expect(filteredRequest.cancelled).toBe(true);
    expect(api.requests).toHaveLength(3);
    expect(store.filters()).toEqual(createDefaultOrderRequestFilters());
    expect(store.page()).toBe(1);
    expect(store.hasActiveFilters()).toBe(false);
    expect(store.activeFilterChips()).toEqual([]);
    expect(store.items()).toEqual([]);
    expect(store.stats()).toBeNull();
    expect(store.total()).toBe(0);
    expect(definedParams(api.requests[2].params)).toEqual({ page: 1, pageSize: 50, envKey: 'Testing' });

    // A failed clear must not resurrect the previous filtered grid.
    api.requests[2].subject.error({ status: 502 });
    expect(store.status()).toBe('error');
    expect(store.items()).toEqual([]);
    expect(store.stats()).toBeNull();
    expect(store.total()).toBe(0);
  });

  it('force-reloads when Clear All only changed the uncommitted draft', () => {
    store.init('upc_ecommerce', 'Testing', createDefaultOrderRequestFilters(), 1, 25);
    expect(api.requests).toHaveLength(1);

    store.clearFilters(true);

    expect(api.requests).toHaveLength(2);
    expect(definedParams(api.requests[1].params)).toEqual({ page: 1, pageSize: 25, envKey: 'Testing' });
    expect(store.filters()).toEqual(createDefaultOrderRequestFilters());
  });

  it.each(clearAllScenarios)('resets the complete state for the $name Clear All scenario', scenario => {
    store.init('upc_ecommerce', 'Testing', {
      ...createDefaultOrderRequestFilters(),
      ...scenario.filters,
      statuses: scenario.filters.statuses ? [...scenario.filters.statuses] : []
    }, scenario.page || 1, 25);

    const initialRequest = api.requests[0];
    if (scenario.mode === 'error') {
      initialRequest.subject.error({ status: 502 });
    } else if (scenario.mode !== 'in-flight') {
      initialRequest.subject.next(response());
    }

    if (scenario.mode === 'manual-refresh') {
      store.refresh();
    } else if (scenario.mode === 'auto-refresh') {
      store.setAutoRefresh(true);
      store.autoRefreshTick();
    }

    const requestBeforeClear = api.requests.at(-1);
    const requestCountBeforeClear = api.requests.length;
    store.clearFilters();
    const clearRequest = api.requests.at(-1);

    expect(api.requests).toHaveLength(requestCountBeforeClear + 1);
    expect(requestBeforeClear?.cancelled).toBe(scenario.mode === 'in-flight' || scenario.mode === 'manual-refresh' || scenario.mode === 'auto-refresh' || scenario.mode === 'error');
    expect(store.filters()).toEqual(createDefaultOrderRequestFilters());
    expect(store.page()).toBe(1);
    expect(store.hasActiveFilters()).toBe(false);
    expect(store.activeFilterChips()).toEqual([]);
    expect(store.status()).toBe('loading');
    expect(definedParams(clearRequest?.params)).toEqual({ page: 1, pageSize: 25, envKey: 'Testing' });
  });

  it('keeps manual and auto-refresh on the cleared filter generation', () => {
    store.init('upc_ecommerce', 'Testing', { ...createDefaultOrderRequestFilters(), branchCode: 'P001' }, 1, 25);
    api.requests[0].subject.next(response());

    store.clearFilters();
    api.requests.at(-1)?.subject.next(response());

    store.refresh();
    expect(definedParams(api.requests.at(-1)?.params)).toEqual({ page: 1, pageSize: 25, envKey: 'Testing' });
    api.requests.at(-1)?.subject.next(response());

    store.setAutoRefresh(true);
    store.autoRefreshTick();
    expect(definedParams(api.requests.at(-1)?.params)).toEqual({ page: 1, pageSize: 25, envKey: 'Testing' });
  });
});
