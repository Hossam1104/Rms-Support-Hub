import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { BranchOptionsService } from '../../../core/services/branch-options.service';
import { createDefaultOrderRequestFilters, OrderRequestsStore } from '../order-requests.store';
import { FilterBarComponent } from './filter-bar.component';

describe('FilterBarComponent', () => {
  let fixture: ComponentFixture<FilterBarComponent>;
  let component: FilterBarComponent;
  let store: OrderRequestsStore;
  let apiGet: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    apiGet = vi.fn((path: string) => path.endsWith('/order-requests')
      ? of({ items: [], page: 1, pageSize: 25, total: 0, totalPages: 1, stats: { total: 0, succeeded: 0, failed: 0, cancelled: 0 } })
      : of({}));
    TestBed.configureTestingModule({
      imports: [FilterBarComponent],
      providers: [
        OrderRequestsStore,
        { provide: ApiService, useValue: { get: apiGet } },
        { provide: BranchOptionsService, useValue: { list: vi.fn(() => of({ success: true, data: [] })) } }
      ]
    });
    fixture = TestBed.createComponent(FilterBarComponent);
    component = fixture.componentInstance;
    store = TestBed.inject(OrderRequestsStore);
    store.init('upc_ecommerce', 'UPC Testing', createDefaultOrderRequestFilters(), 1, 25);
    fixture.detectChanges();
  });

  it('renders a grouped, accessible search workbench', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('Find and monitor order requests');
    expect(element.textContent).toContain('Status');
    expect(element.querySelector('#order-request-search')).not.toBeNull();
    expect(element.querySelector('label[for="order-request-date-from"]')).not.toBeNull();
    expect(element.querySelectorAll('.status-chip')).toHaveLength(9);
    expect(element.querySelectorAll('.status-chip[aria-pressed="false"]')).toHaveLength(9);
  });

  it('keeps edits in a draft until Apply and sends one normalized filter set', () => {
    const requestCount = apiGet.mock.calls.filter(([path]) => path.endsWith('/order-requests')).length;
    component.patchDraft({ search: '  ORD-42  ', exactMatch: false, statuses: [9, 1] });

    expect(store.filters()).toEqual(createDefaultOrderRequestFilters());
    expect(apiGet.mock.calls.filter(([path]) => path.endsWith('/order-requests'))).toHaveLength(requestCount);

    component.apply();

    expect(store.filters()).toMatchObject({ search: 'ORD-42', exactMatch: false, statuses: [1, 9] });
    expect(apiGet.mock.calls.filter(([path]) => path.endsWith('/order-requests'))).toHaveLength(requestCount + 1);
  });

  it('rejects an inverted date range before it reaches the store', () => {
    component.patchDraft({ dateFrom: '2026-08-05', dateTo: '2026-08-04' });
    component.apply();

    expect(component.dateRangeError()).toContain('start date');
    expect(store.filters().dateFrom).toBeNull();
    expect(store.filters().dateTo).toBeNull();
  });

  it('collapses the filter controls without losing the applied state', () => {
    component.patchDraft({ branchCode: 'P001' });
    component.apply();
    component.toggleCollapsed();
    fixture.detectChanges();

    expect(component.collapsed()).toBe(true);
    expect(store.filters().branchCode).toBe('P001');
    expect(fixture.nativeElement.querySelector('.primary-filter-grid')).toBeNull();
  });

  it('Clear All resets a dirty draft and starts exactly one unfiltered request', () => {
    const requestCount = apiGet.mock.calls.filter(([path]) => path.endsWith('/order-requests')).length;
    component.patchDraft({ search: 'ORD-42', branchCode: 'P001', statuses: [3] });

    component.clearAll();

    expect(component.draft()).toEqual(createDefaultOrderRequestFilters());
    expect(component.draftDirty()).toBe(false);
    expect(store.filters()).toEqual(createDefaultOrderRequestFilters());
    expect(store.activeFilterChips()).toEqual([]);
    expect(apiGet.mock.calls.filter(([path]) => path.endsWith('/order-requests'))).toHaveLength(requestCount + 1);
  });
});
