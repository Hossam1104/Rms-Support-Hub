import { TestBed } from '@angular/core/testing';
import { OrderSummaryRailComponent, OrderValidationIssue } from './order-summary-rail.component';
import { EnvironmentDto, ModuleEndpoint, TotalsSummary } from '../../../core/models';

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

const environment = { key: 'UPC Testing', environment: 'Testing' } as EnvironmentDto;
const endpoint = { environmentKey: 'UPC Testing', environment: 'Testing', apiUrl: 'https://testing.example/api' } as ModuleEndpoint;

describe('OrderSummaryRailComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [OrderSummaryRailComponent] }).compileComponents();
  });

  it('renders genuine zero totals and server issue status without local balance math', () => {
    const fixture = TestBed.createComponent(OrderSummaryRailComponent);
    fixture.componentRef.setInput('totals', totalsOf());
    fixture.componentRef.setInput('environment', environment);
    fixture.componentRef.setInput('endpoint', endpoint);
    fixture.componentRef.setInput('validationSummary', { totalCount: 1, globalErrors: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('0.00');
    expect(fixture.nativeElement.textContent).toContain('1 issue');
    expect((fixture.nativeElement.querySelectorAll('ui-button button')[1] as HTMLButtonElement).disabled).toBe(true);
  });

  it('shows loading without fabricated amounts and supports compact action-bar mode', () => {
    const loading = TestBed.createComponent(OrderSummaryRailComponent);
    loading.componentRef.setInput('loading', true);
    loading.componentRef.setInput('environment', environment);
    loading.componentRef.setInput('endpoint', endpoint);
    loading.detectChanges();
    expect(loading.nativeElement.textContent).not.toContain('0.00');
    expect(loading.nativeElement.querySelector('app-skeleton')).toBeTruthy();

    const compact = TestBed.createComponent(OrderSummaryRailComponent);
    compact.componentRef.setInput('compact', true);
    compact.componentRef.setInput('totals', totalsOf({ totalOrderAmount: 125 }));
    compact.componentRef.setInput('environment', environment);
    compact.componentRef.setInput('endpoint', endpoint);
    compact.detectChanges();
    expect(compact.nativeElement.querySelector('[data-testid="order-summary-action-bar"]')).toBeTruthy();
    expect(compact.nativeElement.querySelector('[data-testid="order-summary-rail"]')).toBeNull();
  });

  it('states Cash on Delivery for a payment-free order without blocking send', () => {
    const fixture = TestBed.createComponent(OrderSummaryRailComponent);
    fixture.componentRef.setInput('totals', totalsOf({ totalOrderAmount: 115 }));
    fixture.componentRef.setInput('environment', environment);
    fixture.componentRef.setInput('endpoint', endpoint);
    fixture.componentRef.setInput('validationSummary', { totalCount: 0, globalErrors: [] });
    fixture.componentRef.setInput('isCashOnDelivery', true);
    fixture.detectChanges();

    const note = fixture.nativeElement.querySelector('[data-testid="summary-cod-note"]');
    expect(note).toBeTruthy();
    expect(note.textContent).toContain('Cash on Delivery');
    expect((fixture.nativeElement.querySelectorAll('ui-button button')[1] as HTMLButtonElement).disabled).toBe(false);

    fixture.componentRef.setInput('isCashOnDelivery', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="summary-cod-note"]')).toBeNull();
  });

  it('surfaces Cash on Delivery in the compact action bar too', () => {
    const compact = TestBed.createComponent(OrderSummaryRailComponent);
    compact.componentRef.setInput('compact', true);
    compact.componentRef.setInput('totals', totalsOf({ totalOrderAmount: 115 }));
    compact.componentRef.setInput('environment', environment);
    compact.componentRef.setInput('endpoint', endpoint);
    compact.componentRef.setInput('isCashOnDelivery', true);
    compact.detectChanges();

    expect(compact.nativeElement.querySelector('[data-testid="summary-action-bar-cod"]')).toBeTruthy();
  });

  it('emits a clickable mapped issue and disables send while it is known', () => {
    const fixture = TestBed.createComponent(OrderSummaryRailComponent);
    const issue: OrderValidationIssue = { key: 'products', message: 'Add one product.', targetId: 'products-card' };
    fixture.componentRef.setInput('totals', totalsOf({ totalOrderAmount: 40 }));
    fixture.componentRef.setInput('environment', environment);
    fixture.componentRef.setInput('endpoint', endpoint);
    fixture.componentRef.setInput('validationSummary', { totalCount: 0, globalErrors: [] });
    fixture.componentRef.setInput('validationIssues', [issue]);
    fixture.detectChanges();

    const selected: OrderValidationIssue[] = [];
    fixture.componentInstance.issueSelected.subscribe(value => selected.push(value));
    (fixture.nativeElement.querySelector('.summary-rail__issues button') as HTMLButtonElement).click();

    expect(selected).toEqual([issue]);
    expect((fixture.nativeElement.querySelectorAll('ui-button button')[1] as HTMLButtonElement).disabled).toBe(true);
  });
});
