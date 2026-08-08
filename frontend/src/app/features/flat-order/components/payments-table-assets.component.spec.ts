import { PaymentsTableComponent } from './payments-table.component';

describe('PaymentsTableComponent commerce presentation helpers', () => {
  it('uses only known payment brands and rolls up the displayed total', () => {
    const component = new PaymentsTableComponent();
    component.payments = [
      { paymentMethod: 'Visa', paymentStatus: 'done_payment', paymentAmount: 125.5 },
      { paymentMethod: 'ApplePay', paymentStatus: 'done_payment', paymentAmount: 24.5 }
    ];

    expect(component.paymentAsset(' Visa ')).toBeTruthy();
    expect(component.paymentAsset('ApplePay')).toBeNull();
    expect(component.totalAmount()).toBe(150);
  });
});
