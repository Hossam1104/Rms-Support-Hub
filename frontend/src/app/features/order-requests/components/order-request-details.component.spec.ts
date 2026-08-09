import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { OrderRequestDetailsComponent } from './order-request-details.component';
import { OrderRequestsStore } from '../order-requests.store';
import { ModuleService } from '../../../core/services/module.service';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { OrderRequestDetail, OrderRequestDetailResponse, OrderRequestHeader } from '../../../core/models';

function header(overrides: Partial<OrderRequestHeader> = {}): OrderRequestHeader {
  return {
    orderHeaderId: 1,
    orderNumber: 'ORD-1',
    branchCode: '101',
    branchName: 'Main branch',
    orderStatus: 6,
    orderStatusLabel: 'Succeeded',
    orderDate: '2026-01-01T00:00:00Z',
    consumerMobile: '0500000000',
    address: '123 Street',
    grossTotal: 115,
    netTotal: 103.5,
    totalVat: 13.5,
    totalDiscount: 10,
    orderPaymentMethod: 'Card',
    orderNote: null,
    parentOrderNumber: null,
    rejectionMessage: null,
    canResend: true,
    canCancel: true,
    ...overrides
  };
}

function detail(overrides: Partial<OrderRequestDetail> = {}): OrderRequestDetail {
  return {
    id: 501,
    orderNumber: 'ORD-1',
    orderDate: '2026-01-01T00:00:00Z',
    netTotal: 103.5,
    itemCount: 1,
    isSucceeded: true,
    exceptionMessage: null,
    requestJson: null,
    responseJson: null,
    header: header(),
    details: [
      { itemName: 'Widget', materialNumber: '000123', quantity: 2, unitPrice: 50, totalPrice: 103.5, totalDiscount: 10, itemVat: 13.5, itemVatPercentage: 15, offerCode: null, offerMessage: null }
    ],
    transactions: [],
    invoice: null,
    ...overrides
  };
}

function detailResponse(overrides: Partial<OrderRequestDetail> = {}): OrderRequestDetailResponse {
  return {
    request: detail(overrides),
    attempts: [],
    lineage: { parent: null, children: [] }
  };
}

describe('OrderRequestDetailsComponent', () => {
  let mockStore: {
    moduleKey: ReturnType<typeof signal<string>>;
    envKey: ReturnType<typeof signal<string | null>>;
    selected: ReturnType<typeof signal<OrderRequestDetailResponse | null>>;
    detailStatus: ReturnType<typeof signal<string>>;
    detailError: ReturnType<typeof signal<string | null>>;
    branches: ReturnType<typeof signal<unknown[]>>;
    branchStatus: ReturnType<typeof signal<string>>;
    branchError: ReturnType<typeof signal<string | null>>;
    openDetail: ReturnType<typeof vi.fn>;
    closeDetail: ReturnType<typeof vi.fn>;
    loadBranches: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
    refreshDetailInPlace: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    mockStore = {
      moduleKey: signal('upc_ecommerce'),
      envKey: signal<string | null>('UPC Testing'),
      selected: signal<OrderRequestDetailResponse | null>(null),
      detailStatus: signal('ready'),
      detailError: signal<string | null>(null),
      branches: signal<unknown[]>([]),
      branchStatus: signal('idle'),
      branchError: signal<string | null>(null),
      openDetail: vi.fn(),
      closeDetail: vi.fn(),
      loadBranches: vi.fn(),
      refresh: vi.fn(),
      refreshDetailInPlace: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [OrderRequestDetailsComponent],
      providers: [
        { provide: OrderRequestsStore, useValue: mockStore },
        { provide: ModuleService, useValue: { activeModule: signal({ label: 'UPC Ecommerce', key: 'upc_ecommerce' }), activeEnvironment: signal({ key: 'UPC Testing', environment: 'Testing' }) } },
        { provide: ApiService, useValue: { get: vi.fn(() => of({})), post: vi.fn(() => of({})) } },
        { provide: ToastService, useValue: { showError: vi.fn(), showSuccess: vi.fn() } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ orderId: '501' })), snapshot: { paramMap: convertToParamMap({ orderId: '501' }) } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    }).compileComponents();
  });

  function createFixture() {
    const fixture = TestBed.createComponent(OrderRequestDetailsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('keeps the items summary sums (discount/VAT/total) and shows the matching mismatch-callout state when totals agree', () => {
    mockStore.selected.set(detailResponse());
    const fixture = createFixture();

    const footerText = fixture.nativeElement.textContent as string;
    expect(footerText).toContain('10.00'); // discount total
    expect(footerText).toContain('13.50'); // VAT total
    expect(footerText).toContain('103.50'); // items total, matches header.netTotal

    const callout = fixture.nativeElement.querySelector('.mismatch-callout');
    expect(callout.classList.contains('is-ok')).toBe(true);
    expect(callout.textContent).toContain('Items total matches header total');
  });

  it('flags the mismatch state when the summed line items disagree with the header net total', () => {
    mockStore.selected.set(detailResponse({ header: header({ netTotal: 999 }) }));
    const fixture = createFixture();

    const callout = fixture.nativeElement.querySelector('.mismatch-callout');
    expect(callout.classList.contains('is-ok')).toBe(false);
    expect(callout.textContent).toContain('does not match header net');
  });

  it('renders the Order Info section as two grouped blocks with the expected field values', () => {
    mockStore.selected.set(detailResponse());
    const fixture = createFixture();

    const groups = fixture.nativeElement.querySelectorAll('.info-group');
    expect(groups.length).toBe(2);

    const titles = Array.from(groups as NodeListOf<HTMLElement>).map(g => g.querySelector('.info-group__title')?.textContent?.trim());
    expect(titles).toEqual(['Order identity', 'Customer & fulfillment']);

    const identityText = (groups[0] as HTMLElement).textContent as string;
    expect(identityText).toContain('ORD-1');
    expect(identityText).toContain('Succeeded');

    const fulfillmentText = (groups[1] as HTMLElement).textContent as string;
    expect(fulfillmentText).toContain('101');
    expect(fulfillmentText).toContain('Main branch');
    expect(fulfillmentText).toContain('0500000000');
    expect(fulfillmentText).toContain('123 Street');
  });

  it('omits the Customer & fulfillment block and shows the no-header empty state when there is no order header', () => {
    mockStore.selected.set(detailResponse({ header: null }));
    const fixture = createFixture();

    const groups = fixture.nativeElement.querySelectorAll('.info-group');
    expect(groups.length).toBe(1);
    expect((fixture.nativeElement.textContent as string)).toContain('No order header recorded');
  });
});
