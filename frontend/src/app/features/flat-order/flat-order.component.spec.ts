import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { signal } from '@angular/core';
import { Subject, of, throwError } from 'rxjs';
import { FlatOrderComponent } from './flat-order.component';
import { ApiService } from '../../core/services/api.service';
import { BranchOptionsService } from '../../core/services/branch-options.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { FocusService } from '../../core/services/focus.service';
import { EnvironmentDto, ModuleDto, OrderDraft, Product, TotalsSummary } from '../../core/models';

function emptyDraft(): OrderDraft {
  return { orderData: {}, products: [], payments: [], consumer: {}, delivery: { deliveryFees: 0 }, rowItems: [] };
}

function totalsOf(overrides: Partial<TotalsSummary> = {}): TotalsSummary {
  return {
    totalProductAmount: 0,
    totalProductVat: 0,
    orderDiscount: 0,
    deliveryCost: 0,
    totalOrderAmount: 0,
    totalPaidAmount: 0,
    remainingBalance: 0,
    ...overrides
  };
}

class MockApiService {
  totalsCalls: Subject<TotalsSummary>[] = [];
  sendCalls: Subject<unknown>[] = [];
  patchCalls: Subject<unknown>[] = [];
  sentBodies: unknown[] = [];
  itemLookupResponse = of({ success: false });

  get = vi.fn((path: string) => {
    if (path.endsWith('/state')) return of(emptyDraft());
    if (path.endsWith('/endpoint')) return of({ environmentKey: 'UPC Testing', environment: 'Testing', apiUrl: 'http://10.10.10.181:8080/api' });
    if (path.endsWith('/calculate-totals')) {
      const s = new Subject<TotalsSummary>();
      this.totalsCalls.push(s);
      return s;
    }
    if (path.includes('/lookup/item')) return this.itemLookupResponse;
    if (path.endsWith('/export-json')) return of({});
    return of({});
  });

  post = vi.fn((path: string, body: unknown) => {
    if (path.endsWith('/send-request')) {
      this.sentBodies.push(body);
      const s = new Subject<unknown>();
      this.sendCalls.push(s);
      return s;
    }
    return of({ success: true, products: [], payments: [], totals: totalsOf({ totalOrderAmount: 11.5 }) });
  });

  patch = vi.fn(() => {
    const s = new Subject<unknown>();
    this.patchCalls.push(s);
    return s;
  });

  delete = vi.fn(() => of({ success: true, totals: totalsOf() }));
  put = vi.fn((_path: string, body: unknown) => of({ success: true, products: body ? [body] : [], payments: body ? [body] : [], totals: totalsOf() }));
}

describe('FlatOrderComponent', () => {
  let api: MockApiService;
  let activeModuleSignal = signal({} as ModuleDto);
  let toast: { showSuccess: ReturnType<typeof vi.fn>, showInfo: ReturnType<typeof vi.fn>, showError: ReturnType<typeof vi.fn> };
  let focus: { scrollToAndFocus: ReturnType<typeof vi.fn> };
  let fixtures: ComponentFixture<FlatOrderComponent>[] = [];

  beforeEach(async () => {
    api = new MockApiService();
    toast = { showSuccess: vi.fn(), showInfo: vi.fn(), showError: vi.fn() };
    focus = { scrollToAndFocus: vi.fn(() => true) };

    const activeEnvironment = signal({ key: 'UPC Testing', environment: 'Testing' } as EnvironmentDto);
    activeModuleSignal = signal({
      key: 'upc_ecommerce',
      capabilities: { hasDeliveryFields: false, orderRequests: false }
    } as ModuleDto);

    await TestBed.configureTestingModule({
      imports: [FlatOrderComponent],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: BranchOptionsService, useValue: { list: vi.fn(() => of({ success: true, data: [] })) } },
        { provide: ModuleService, useValue: { activeEnvironment, activeModule: activeModuleSignal, selectEnvironment: vi.fn() } },
        { provide: ToastService, useValue: toast },
        { provide: FocusService, useValue: focus },
        { provide: ActivatedRoute, useValue: { parent: { paramMap: of(convertToParamMap({ key: 'upc_ecommerce' })) } } }
      ]
    }).compileComponents();

    vi.useFakeTimers();
  });

  afterEach(() => {
    for (const fixture of fixtures) fixture.destroy();
    fixtures = [];
    vi.clearAllTimers();
    vi.useRealTimers();
  });

  function createFixture() {
    const fixture = TestBed.createComponent(FlatOrderComponent);
    fixtures.push(fixture);
    fixture.detectChanges();
    return fixture;
  }

  it('refreshes server totals only after the batched PATCH settles', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;
    const callsBefore = api.totalsCalls.length;

    component.onFieldChange({ fieldName: 'order_delivery_cost', value: 5 });
    vi.advanceTimersByTime(300); // DraftStore patch debounce
    expect(api.patch).toHaveBeenCalledWith('modules/upc_ecommerce/order-data', { fields: { order_delivery_cost: 5 } });

    api.patchCalls[0].next({ success: true });
    fixture.detectChanges(); // runs the flushVersion effect
    vi.advanceTimersByTime(300); // totals debounce

    expect(api.totalsCalls.length).toBeGreaterThan(callsBefore);
  });

  it('populates the summary from the server, preserving genuine zero values', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.refreshTotals();
    api.totalsCalls.at(-1)!.next(totalsOf());

    expect(component.totals()).toEqual(totalsOf());
    expect(component.totalsLoading()).toBe(false);
    expect(component.totalsError()).toBeNull();
  });

  it('never lets an older totals response overwrite a newer one', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.refreshTotals();
    component.refreshTotals();
    const [older, newer] = api.totalsCalls.slice(-2);

    newer.next(totalsOf({ totalOrderAmount: 200 }));
    expect(component.totals()?.totalOrderAmount).toBe(200);

    older.next(totalsOf({ totalOrderAmount: 100 }));
    expect(component.totals()?.totalOrderAmount).toBe(200);
  });

  it('adopts the totals carried by a product mutation response', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;
    const product = { itemCode: 'A', itemName: 'A', quantity: 1, unitPrice: 10, vatPercentage: 15, discount: 0 };

    component.onAddProduct(product);

    expect(api.post).toHaveBeenCalledWith('modules/upc_ecommerce/products', product);
    expect(component.totals()?.totalOrderAmount).toBe(11.5);
  });

  it('does not fabricate totals when the server refresh fails', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.refreshTotals();
    api.totalsCalls.at(-1)!.error(new Error('boom'));

    expect(component.totals()).toBeNull();
    expect(component.totalsError()).toBeTruthy();
    expect(component.totalsLoading()).toBe(false);
  });

  it('blocks the item lookup without a branch and points to the picker', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onLookupItem({ code: '123', branchCode: '  ' });

    expect(component.itemLookupOutcome()).toEqual({ status: 'missing-branch' });
    expect(api.get).not.toHaveBeenCalledWith('modules/upc_ecommerce/lookup/item', expect.anything());
  });

  it('pushes a found item into the dialog outcome', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;
    const product = { itemCode: 'P1', itemName: 'Found Item', itemNameAr: 'عنصر', quantity: 1, unitPrice: 10, vatPercentage: 15, discount: 0 };
    api.itemLookupResponse = of({ success: true, data: product });

    component.onLookupItem({ code: 'P1', branchCode: '101' });

    expect(component.itemLookupOutcome()).toEqual({ status: 'found', product });
    expect(toast.showSuccess).toHaveBeenCalled();
  });

  it('keeps not-found distinct from infrastructure failure', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    api.itemLookupResponse = of({ success: false, message: 'No item found.' });
    component.onLookupItem({ code: 'X', branchCode: '101' });
    expect(component.itemLookupOutcome()).toEqual({ status: 'not-found', code: 'X' });

    api.itemLookupResponse = throwError(() => ({ status: 502, code: 'upstream_error', message: 'DB down' }));
    component.onLookupItem({ code: 'X', branchCode: '101' });
    expect(component.itemLookupOutcome()).toEqual({ status: 'error' });
  });

  it('runs send loading through the request lifecycle with only the environment key', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onSendOrder();
    expect(component.sending()).toBe(true);
    expect(api.sentBodies[0]).toEqual({ environmentKey: 'UPC Testing' });

    api.sendCalls[0].next({ success: true, statusCode: 200, responseText: '{}', urlSent: 'http://10.10.10.181:8080/api' });
    api.sendCalls[0].complete(); // HTTP observables terminate -- finalize clears loading
    expect(component.sending()).toBe(false);
  });

  it('ends loading on send error', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onSendOrder();
    api.sendCalls[0].error({ status: 500, code: 'unknown_error', message: 'boom' });

    expect(component.sending()).toBe(false);
    expect(component.apiResponse()?.success).toBe(false);
  });

  it('blocks a duplicate send while one is in flight', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onSendOrder();
    component.onSendOrder();

    expect(api.sendCalls.length).toBe(1);
  });

  it('maps send validation failures to inline field errors and focuses the first', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onSendOrder();
    api.sendCalls[0].error({
      status: 400,
      code: 'validation_failed',
      message: 'Order validation failed with 2 issue(s).',
      details: ['Missing required field: branch_code', 'Order must contain at least one product.']
    });

    expect(component.sending()).toBe(false);
    expect(component.fieldErrors()['branch_code']).toEqual(['Missing required field: branch_code']);
    expect(component.fieldErrors()['products']).toEqual(['Order must contain at least one product.']);
    expect(component.validationSummary()?.totalCount).toBe(2);
    expect(component.apiResponse()?.statusCode).toBe(400);
    expect(component.apiResponse()?.responseText).toContain('Missing required field: branch_code');
    expect(focus.scrollToAndFocus).toHaveBeenCalledWith('field-branch-code');
  });

  it('clears a field error when the operator corrects that field', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onSendOrder();
    api.sendCalls[0].error({
      status: 400,
      code: 'validation_failed',
      message: 'Order validation failed with 2 issue(s).',
      details: ['Missing required field: branch_code', 'Missing required field: order_code']
    });

    component.onFieldChange({ fieldName: 'branch_code', value: '101' });

    expect(component.fieldErrors()['branch_code']).toBeUndefined();
    expect(component.fieldErrors()['order_code']).toEqual(['Missing required field: order_code']);
    expect(component.validationSummary()?.totalCount).toBe(1);
  });

  it('renders the U6 workflow in order and gates delivery navigation by capability', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    expect(component.sections().map(section => section.label)).toEqual(['Order header', 'Customer', 'Products', 'Payments', 'Payload & send']);
    expect(fixture.nativeElement.querySelector('[data-testid="order-summary-rail"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).not.toContain('Delivery Schedule');

    activeModuleSignal.set({
      key: 'upc_ecommerce',
      capabilities: { hasDeliveryFields: true, orderRequests: false }
    } as ModuleDto);
    fixture.detectChanges();

    expect(component.sections().map(section => section.label)).toEqual(['Order header', 'Customer', 'Delivery', 'Products', 'Payments', 'Payload & send']);
    expect(fixture.nativeElement.querySelector('[data-section-id="delivery-section"]')).toBeTruthy();
  });

  it('commits product table edits through the existing row endpoint and keeps local draft state responsive', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;
    const product: Product = { itemCode: 'A', itemName: 'A', quantity: 1, unitPrice: 10, vatPercentage: 15, discount: 0 };
    component.draftStore.setDraft({ ...emptyDraft(), products: [product] });

    component.onUpdateProduct({ index: 0, patch: { quantity: 3, discount: 2 } });

    expect(component.draftStore.draft().products[0]).toMatchObject({ quantity: 3, discount: 2 });
    expect(api.put).toHaveBeenCalledWith('modules/upc_ecommerce/products/0', { ...product, quantity: 3, discount: 2 });
  });

  it('opens a collapsed invalid section before focusing its mapped issue target', async () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;
    const productsSection = fixture.nativeElement.querySelector('#products-section') as HTMLElement;
    (productsSection.querySelector('.ui-section__toggle') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(productsSection.querySelector('#products-card')).toBeNull();

    component.onValidationIssue({ key: 'products', message: 'Add a product.', targetId: 'products-card' });
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();

    expect(productsSection.querySelector('#products-card')).toBeTruthy();
    expect(focus.scrollToAndFocus).toHaveBeenCalledWith('products-card');
  });

  it('keeps Validate non-sending because the API has no standalone validation endpoint', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.onValidate();

    expect(api.sentBodies).toEqual([]);
    expect(toast.showInfo).toHaveBeenCalledWith(expect.stringContaining('Server validation runs'));
  });
});
