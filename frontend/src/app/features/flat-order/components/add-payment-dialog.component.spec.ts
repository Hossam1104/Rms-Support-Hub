import { AddPaymentDialogComponent } from './add-payment-dialog.component';

describe('AddPaymentDialogComponent', () => {
  it('exposes the backend payment method contract', () => {
    const component = new AddPaymentDialogComponent();

    expect(component.paymentMethodOptions.map(option => option.value)).toEqual([
      'COD', 'Visa', 'RajhiPoints', 'Tamara', 'Tabby', 'NeqatyPoints',
      'QitafPoints', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney', 'PostToCredit'
    ]);
  });

  it('sets paid methods to the server-required amount and done_payment', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;

    for (const method of ['Visa', 'RajhiPoints', 'Tamara', 'Tabby', 'NeqatyPoints', 'QitafPoints', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney']) {
      component.payment.paymentMethod = method;
      component.onMethodChange();

      expect(component.payment.paymentAmount).toBe(107.9);
      expect(component.payment.paymentStatus).toBe('done_payment');
    }

    component.payment.paymentMethod = 'PostToCredit';
    component.onMethodChange();
    expect(component.payment.paymentAmount).toBe(107.9);
    expect(component.payment.paymentStatus).toBe('not_payment');

    component.payment.paymentMethod = 'COD';
    component.onMethodChange();
    expect(component.payment.paymentAmount).toBe(0);
  });

  it('keeps COD as not_payment while paid methods are done_payment on add', () => {
    const component = new AddPaymentDialogComponent();
    const added: Array<{ paymentMethod: string; paymentStatus: string; paymentAmount: number }> = [];
    component.add.subscribe(payment => added.push(payment));

    component.payment.paymentAmount = 10;
    component.onAdd();
    component.payment.paymentMethod = 'Tamara';
    component.payment.paymentAmount = 10;
    component.onAdd();

    expect(added.map(payment => [payment.paymentMethod, payment.paymentStatus])).toEqual([
      ['COD', 'not_payment'],
      ['Tamara', 'done_payment']
    ]);
  });

  it('does not overwrite a manually edited Visa amount when the required balance changes', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;
    component.payment.paymentMethod = 'Visa';
    component.onMethodChange();
    component.onAmountChange(50);

    component.requiredAmount = 125;
    component.ngOnChanges({ requiredAmount: {
      currentValue: 125,
      previousValue: 107.9,
      firstChange: false,
      isFirstChange: () => false
    } });

    expect(component.payment.paymentAmount).toBe(50);
  });
});
