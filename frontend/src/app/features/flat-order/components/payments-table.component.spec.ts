import { TestBed } from '@angular/core/testing';
import { PaymentsTableComponent } from './payments-table.component';
import { Payment } from '../../../core/models';

describe('PaymentsTableComponent', () => {
  it('emits committed amount/status patches and exposes method metadata', () => {
    const component = new PaymentsTableComponent();
    const updates: unknown[] = [];
    component.updatePayment.subscribe(update => updates.push(update));

    component.onAmountEdit(1, { target: { value: '125.50' } } as unknown as Event);
    component.onStatusChange(1, 'done_payment');

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

  it('shows the payment total in the summary strip below the rows', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = [
      { paymentMethod: 'Visa', paymentStatus: 'done_payment', paymentAmount: 100 },
      { paymentMethod: 'Mada', paymentStatus: 'done_payment', paymentAmount: 25.5 }
    ];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.py__total-value')?.textContent).toContain('125.50');
  });

  it('labels the status for operators while the payload value stays raw', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = samplePayments;
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('#payment-status-0') as HTMLButtonElement;
    expect(status.textContent).toContain('Paid');
    expect(status.textContent).not.toContain('done_payment');
    expect(fixture.componentInstance.paymentStatusOptions.map(option => option.value))
      .toEqual(['not_payment', 'done_payment', 'failed_payment']);
  });

  it('replaces the native select so the open list is not an OS popup', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = samplePayments;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('select')).toBeNull();
    expect(fixture.nativeElement.querySelector('ui-dropdown-select')).not.toBeNull();
  });

  it('keeps the status editable for a Visa row -- done_payment is a default, not a lock', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = samplePayments;
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('#payment-status-0') as HTMLButtonElement).disabled).toBe(false);
  });

  /** A draft saved before the UPC policy can still hold a method UPC no longer
   * accepts. The row shows what is actually stored so the operator can remove
   * it; it is never silently rewritten to an allowed method. */
  it('renders a payment method that policy no longer allows as its own identity', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = [{ paymentMethod: 'MisPay', paymentStatus: 'done_payment', paymentAmount: 100 }];
    fixture.componentInstance.errors = ["Payment method 'MisPay' is not allowed. The allowed payment methods: [Visa, Tamara, Tabby]"];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.pyrow__method')?.textContent?.trim()).toBe('MisPay');
    expect(fixture.nativeElement.querySelector('.py__errors')?.textContent).toContain("Payment method 'MisPay' is not allowed");
  });

  it('keeps the reference and metadata inside the payment identity block rather than a dedicated column', () => {
    const fixture = TestBed.createComponent(PaymentsTableComponent);
    fixture.componentInstance.payments = [
      { paymentMethod: 'Visa', paymentStatus: 'done_payment', paymentAmount: 100, transactionId: 'TX-9', cardName: 'Visa' }
    ];
    fixture.detectChanges();

    const identity = fixture.nativeElement.querySelector('.pyrow__id') as HTMLElement;
    expect(identity.querySelector('.reference-label')?.textContent?.trim()).toBe('TX-9');
    expect(identity.querySelector('.metadata-label')?.textContent?.trim()).toBe('Visa');
  });
});
