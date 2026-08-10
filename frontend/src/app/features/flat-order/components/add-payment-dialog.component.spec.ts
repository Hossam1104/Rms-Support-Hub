import { SimpleChange } from '@angular/core';
import { AddPaymentDialogComponent } from './add-payment-dialog.component';

/** Simulates an open of the dialog for a module: the host recreates the
 * component per open, so the moduleKey change IS the open. */
function openFor(component: AddPaymentDialogComponent, moduleKey: string) {
  const previous = component.moduleKey;
  component.moduleKey = moduleKey;
  component.ngOnChanges({ moduleKey: new SimpleChange(previous, moduleKey, previous === '') });
}

describe('AddPaymentDialogComponent', () => {
  it('exposes the full backend payment method contract for GHC', () => {
    const component = new AddPaymentDialogComponent();
    openFor(component, 'ghc_ecommerce');

    expect(component.availablePaymentMethodOptions.map(option => option.value)).toEqual([
      'COD', 'Visa', 'RajhiPoints', 'Tamara', 'Tabby', 'NeqatyPoints',
      'QitafPoints', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney', 'PostToCredit'
    ]);
  });

  it('offers UPC exactly Visa, Tamara and Tabby, in that order', () => {
    const component = new AddPaymentDialogComponent();
    openFor(component, 'upc_ecommerce');

    expect(component.availablePaymentMethodOptions.map(option => option.value)).toEqual(['Visa', 'Tamara', 'Tabby']);
  });

  it('never offers COD, MisPay or PostToCredit as a UPC payment row', () => {
    const component = new AddPaymentDialogComponent();
    openFor(component, 'upc_ecommerce');

    const values = component.availablePaymentMethodOptions.map(option => option.value);
    for (const blocked of ['COD', 'MisPay', 'PostToCredit', 'Emkan', 'RajhiPoints', 'NeqatyPoints', 'QitafPoints', 'YouGotaGift', 'OgMoney']) {
      expect(values).not.toContain(blocked);
    }
  });

  it('opens a UPC dialog on Visa with a done_payment status', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;
    openFor(component, 'upc_ecommerce');

    expect(component.payment.paymentMethod).toBe('Visa');
    expect(component.payment.paymentStatus).toBe('done_payment');
    expect(component.payment.paymentAmount).toBe(107.9);
  });

  it('keeps GHC opening on COD with a not_payment status', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;
    openFor(component, 'ghc_ecommerce');

    expect(component.payment.paymentMethod).toBe('COD');
    expect(component.payment.paymentStatus).toBe('not_payment');
    expect(component.payment.paymentAmount).toBe(0);
  });

  it('defaults each UPC method to done_payment as soon as it is selected', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;
    openFor(component, 'upc_ecommerce');

    for (const method of ['Visa', 'Tamara', 'Tabby']) {
      component.onMethodChange(method);

      expect(component.payment.paymentMethod).toBe(method);
      expect(component.payment.paymentStatus).toBe('done_payment');
    }
  });

  it('adds each UPC method with a done_payment status', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;
    openFor(component, 'upc_ecommerce');
    const added: Array<[string, string]> = [];
    component.add.subscribe(payment => added.push([payment.paymentMethod, payment.paymentStatus]));

    for (const method of ['Visa', 'Tamara', 'Tabby']) {
      component.onMethodChange(method);
      component.onAdd();
    }

    expect(added).toEqual([['Visa', 'done_payment'], ['Tamara', 'done_payment'], ['Tabby', 'done_payment']]);
  });

  it('starts a reopened dialog from the active module rather than the previous one', () => {
    const component = new AddPaymentDialogComponent();
    openFor(component, 'ghc_ecommerce');
    component.onMethodChange('MisPay');

    openFor(component, 'upc_ecommerce');

    expect(component.payment.paymentMethod).toBe('Visa');
    expect(component.payment.paymentStatus).toBe('done_payment');
  });

  it('labels the payment statuses for operators while keeping the payload values', () => {
    const component = new AddPaymentDialogComponent();

    expect(component.paymentStatusOptions.map(option => [option.value, option.label])).toEqual([
      ['not_payment', 'Not paid'],
      ['done_payment', 'Paid'],
      ['failed_payment', 'Failed']
    ]);
  });

  it('sets paid methods to the server-required amount and done_payment', () => {
    const component = new AddPaymentDialogComponent();
    component.requiredAmount = 107.9;
    openFor(component, 'ghc_ecommerce');

    for (const method of ['Visa', 'RajhiPoints', 'Tamara', 'Tabby', 'NeqatyPoints', 'QitafPoints', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney']) {
      component.onMethodChange(method);

      expect(component.payment.paymentAmount).toBe(107.9);
      expect(component.payment.paymentStatus).toBe('done_payment');
    }

    component.onMethodChange('PostToCredit');
    expect(component.payment.paymentAmount).toBe(107.9);
    expect(component.payment.paymentStatus).toBe('not_payment');

    component.onMethodChange('COD');
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
    component.onMethodChange('Visa');
    component.onAmountChange(50);

    component.requiredAmount = 125;
    component.ngOnChanges({ requiredAmount: new SimpleChange(107.9, 125, false) });

    expect(component.payment.paymentAmount).toBe(50);
  });
});
