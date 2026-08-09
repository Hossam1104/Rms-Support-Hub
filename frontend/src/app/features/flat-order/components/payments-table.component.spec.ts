import { TestBed } from '@angular/core/testing';
import { PaymentsTableComponent } from './payments-table.component';
import { Payment } from '../../../core/models';

describe('PaymentsTableComponent', () => {
  it('emits committed amount/status patches and exposes method metadata', () => {
    const component = new PaymentsTableComponent();
    const updates: unknown[] = [];
    component.updatePayment.subscribe(update => updates.push(update));

    component.onEdit(1, 'paymentAmount', { target: { value: '125.50' } } as unknown as Event);
    component.onEdit(1, 'paymentStatus', { target: { value: 'done_payment' } } as unknown as Event);

    expect(updates).toEqual([
      { index: 1, patch: { paymentAmount: 125.5 } },
      { index: 1, patch: { paymentStatus: 'done_payment' } }
    ]);
    expect(component.metadata({ paymentMethod: 'Card', paymentStatus: 'done_payment', paymentAmount: 125.5, cardName: 'Visa', bankCode: 'BANK' })).toBe('Visa · BANK');
  });

  const samplePayments: Payment[] = [
    { paymentMethod: 'Visa', paymentStatus: 'done_payment', paymentAmount: 100 }
  ];

  it('renders no internal "Payments" heading -- that lives only in the host ui-section title', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = samplePayments;
    fixture.detectChanges();

    const text = (fixture.nativeElement.textContent as string).trim();
    expect(text.split(/\s+/).filter(w => w === 'Payments').length).toBe(0);
  });

  it('shows an em dash instead of "No transaction reference" when a payment has no reference', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = samplePayments;
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('No transaction reference');
    expect(fixture.nativeElement.querySelector('.reference-label')?.textContent?.trim()).toBe('—');
  });

  it('emits deletePayment with the row index when the delete action is pressed', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = samplePayments;
    const deleted: number[] = [];
    fixture.componentInstance.deletePayment.subscribe(i => deleted.push(i));
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector('button[aria-label="Remove payment"]') as HTMLButtonElement;
    deleteButton.click();

    expect(deleted).toEqual([0]);
  });

  it('shows the payment total in the footer, aligned with the Amount column', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = [
      { paymentMethod: 'Visa', paymentStatus: 'done_payment', paymentAmount: 100 },
      { paymentMethod: 'Mada', paymentStatus: 'done_payment', paymentAmount: 25.5 }
    ];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('tfoot .total-cell')?.textContent).toContain('125.50');
  });
});
