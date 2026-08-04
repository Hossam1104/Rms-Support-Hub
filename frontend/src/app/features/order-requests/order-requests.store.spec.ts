import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import { OrderRequestListResponse } from '../../core/models';
import { EMPTY_FILTERS, OrderRequestsStore } from './order-requests.store';

function response(items: OrderRequestListResponse['items'] = []): OrderRequestListResponse {
  return { items, page: 1, pageSize: 25, total: items.length, totalPages: items.length ? 1 : 0, stats: { total: items.length, succeeded: 0, failed: 0, cancelled: 0 } };
}

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

  it('normalizes filters, cancels stale requests, and skips no-op reloads', () => {
    store.init('upc_ecommerce', 'Testing', { ...EMPTY_FILTERS }, 1, 25);
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
    store.init('upc_ecommerce', 'Testing', { ...EMPTY_FILTERS, search: 'old-order' }, 3, 25);

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
    store.init('upc_ecommerce', 'Testing', { ...EMPTY_FILTERS, branchCode: 'P001' }, 1, 25);
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
    store.init('upc_ecommerce', 'Testing', { ...EMPTY_FILTERS }, 1, 25);
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
});
