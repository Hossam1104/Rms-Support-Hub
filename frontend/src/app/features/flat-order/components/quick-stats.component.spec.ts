import { TestBed } from '@angular/core/testing';
import { QuickStatsComponent } from './quick-stats.component';
import { TotalsSummary } from '../../../core/models';

const zeroTotals: TotalsSummary = {
  totalProductAmount: 0,
  totalProductVat: 0,
  orderDiscount: 0,
  deliveryCost: 0,
  totalOrderAmount: 0,
  totalPaidAmount: 0,
  remainingBalance: 0
};

const sampleTotals: TotalsSummary = {
  totalProductAmount: 100,
  totalProductVat: 13.5,
  orderDiscount: 10,
  deliveryCost: 5,
  totalOrderAmount: 118.5,
  totalPaidAmount: 50,
  remainingBalance: 68.5
};

describe('QuickStatsComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuickStatsComponent]
    }).compileComponents();
  });

  function createFixture() {
    return TestBed.createComponent(QuickStatsComponent);
  }

  it('renders the full server breakdown', () => {
    const fixture = createFixture();
    fixture.componentRef.setInput('totals', sampleTotals);
    fixture.componentRef.setInput('productCount', 2);
    fixture.componentRef.setInput('totalQuantity', 5);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('100.00'); // subtotal
    expect(text).toContain('13.50'); // VAT
    expect(text).toContain('10.00'); // discount
    expect(text).toContain('5.00');  // delivery
    expect(text).toContain('Products');
    expect(text).toContain('Quantity');
  });

  it('renders genuine zero values instead of an unavailable placeholder', () => {
    const fixture = createFixture();
    fixture.componentRef.setInput('totals', zeroTotals);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('0.00');
    expect(text).not.toContain('Totals have not been calculated yet');
  });

  it('shows an unavailable state, not fabricated zeros, before the first load', () => {
    const fixture = createFixture();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Totals have not been calculated yet');
  });

  it('shows the error state without fabricating totals', () => {
    const fixture = createFixture();
    fixture.componentRef.setInput('error', 'Totals could not be refreshed from the server.');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Totals could not be refreshed from the server.');
  });

  it('keeps the last valid totals on screen during a failed refresh', () => {
    const fixture = createFixture();
    fixture.componentRef.setInput('totals', sampleTotals);
    fixture.detectChanges();

    fixture.componentRef.setInput('error', 'Totals could not be refreshed from the server.');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('100.00'); // last valid subtotal still rendered
    expect(text).toContain('last known values');
  });
});
