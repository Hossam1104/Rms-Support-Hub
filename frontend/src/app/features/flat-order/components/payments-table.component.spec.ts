import { PaymentsTableComponent } from './payments-table.component';

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
});
