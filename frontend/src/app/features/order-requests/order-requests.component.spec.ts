import { createDefaultOrderRequestFilters } from './order-requests.store';
import { buildOrderRequestsQueryParams } from './order-requests.component';

describe('Order Requests URL state', () => {
  it('removes legacy filter aliases while retaining the selected page size', () => {
    const params = buildOrderRequestsQueryParams(createDefaultOrderRequestFilters(), 1, 100);

    expect(params).toMatchObject({
      search: null,
      orderNumber: null,
      q: null,
      request: null,
      exactMatch: null,
      branchCode: null,
      branch: null,
      status: null,
      statuses: null,
      succeeded: null,
      hasException: null,
      page: null,
      pageSize: '100'
    });
  });

  it('serializes only the canonical active filters', () => {
    const params = buildOrderRequestsQueryParams({
      ...createDefaultOrderRequestFilters(),
      search: 'ORD-42',
      exactMatch: false,
      branchCode: 'P001',
      statuses: [3, 7],
      outcome: 'failed',
      dateFrom: '2026-08-01',
      dateTo: '2026-08-05'
    }, 3, 25);

    expect(params).toMatchObject({
      search: 'ORD-42',
      exactMatch: 'false',
      branchCode: 'P001',
      status: ['3', '7'],
      outcome: 'failed',
      dateFrom: '2026-08-01',
      dateTo: '2026-08-05',
      page: '3',
      pageSize: null
    });
    expect(params['orderNumber']).toBeNull();
    expect(params['statuses']).toBeNull();
  });
});
