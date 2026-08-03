import { AddProductDialogComponent, ItemLookupOutcome } from './add-product-dialog.component';
import { Product } from '../../../core/models';

const foundProduct: Product = {
  itemCode: '000000000000123456',
  itemName: 'Paracetamol 500mg',
  itemNameAr: 'باراسيتامول 500',
  quantity: 1,
  unitPrice: 10,
  vatPercentage: 15,
  discount: 0,
  netUnitPrice: 11.5
};

function createComponent(outcome: ItemLookupOutcome | null = null): AddProductDialogComponent {
  const component = new AddProductDialogComponent();
  component.branchCode = '101';
  if (outcome) {
    component.lookupOutcome = outcome;
    component.ngOnChanges({ lookupOutcome: { currentValue: outcome, previousValue: null, firstChange: true, isFirstChange: () => true } });
  }
  return component;
}

describe('AddProductDialogComponent', () => {
  it('blocks the lookup when no branch is selected and points to the picker', () => {
    const component = new AddProductDialogComponent();
    component.branchCode = '  ';
    component.searchCode = '123456';
    const emissions: unknown[] = [];
    component.lookupItem.subscribe(e => emissions.push(e));

    component.onItemLookup();

    expect(emissions).toEqual([]);
    expect(component.lookupMessage?.text).toContain('Select a branch');
  });

  it('requires an item code before emitting a lookup', () => {
    const component = new AddProductDialogComponent();
    component.branchCode = '101';
    component.searchCode = '   ';
    const emissions: unknown[] = [];
    component.lookupItem.subscribe(e => emissions.push(e));

    component.onItemLookup();

    expect(emissions).toEqual([]);
    expect(component.lookupMessage?.text).toContain('Enter an item/material code');
  });

  it('populates every verified lookup field and marks them as DB-filled', () => {
    const component = createComponent({ status: 'found', product: foundProduct });

    expect(component.product.itemCode).toBe(foundProduct.itemCode);
    expect(component.product.itemName).toBe(foundProduct.itemName);
    expect(component.product.itemNameAr).toBe(foundProduct.itemNameAr);
    expect(component.product.unitPrice).toBe(10);
    expect(component.product.vatPercentage).toBe(15);
    expect(component.dbFilled.has('itemCode')).toBe(true);
    expect(component.dbFilled.has('itemName')).toBe(true);
    expect(component.dbFilled.has('itemNameAr')).toBe(true);
    expect(component.dbFilled.has('unitPrice')).toBe(true);
    expect(component.dbFilled.has('vatPercentage')).toBe(true);
  });

  it('keeps the operator quantity when a lookup populates the form', () => {
    const component = new AddProductDialogComponent();
    component.branchCode = '101';
    component.product.quantity = 5;
    component.lookupOutcome = { status: 'found', product: foundProduct };
    component.ngOnChanges({ lookupOutcome: { currentValue: component.lookupOutcome, previousValue: null, firstChange: true, isFirstChange: () => true } });

    expect(component.product.quantity).toBe(5);
  });

  it('derives the net unit price with the verified display convention', () => {
    const component = createComponent({ status: 'found', product: foundProduct });

    // Product.cs NetUnitPrice: unitPrice + unitPrice * vat / 100, rounded to 2.
    expect(component.netUnitPrice()).toBe(11.5);

    component.product.unitPrice = 20;
    component.product.vatPercentage = 0;
    expect(component.netUnitPrice()).toBe(20);
  });

  it('clears stale populated data on a not-found result', () => {
    const component = createComponent({ status: 'found', product: foundProduct });

    component.lookupOutcome = { status: 'not-found', code: '999999' };
    component.ngOnChanges({ lookupOutcome: { currentValue: component.lookupOutcome, previousValue: null, firstChange: false, isFirstChange: () => false } });

    expect(component.product.itemCode).toBe('999999');
    expect(component.product.itemName).toBe('');
    expect(component.product.itemNameAr).toBeNull();
    expect(component.product.unitPrice).toBe(0);
    expect(component.dbFilled.size).toBe(0);
    expect(component.lookupMessage?.text).toContain('not found');
  });

  it('clears still-database-filled values while a new lookup is pending', () => {
    const component = createComponent({ status: 'found', product: foundProduct });
    const emissions: unknown[] = [];
    component.lookupItem.subscribe(e => emissions.push(e));
    component.searchCode = '999999';

    component.onItemLookup();

    expect(emissions).toEqual([{ code: '999999', branchCode: '101' }]);
    expect(component.product.itemCode).toBe('999999');
    expect(component.product.itemName).toBe('');
    expect(component.product.itemNameAr).toBeNull();
    expect(component.product.unitPrice).toBe(0);
    expect(component.product.vatPercentage).toBe(15);
    expect(component.dbFilled.size).toBe(0);
  });

  it('keeps manually edited lookup fields when a new lookup is pending', () => {
    const component = createComponent({ status: 'found', product: foundProduct });
    component.product.itemName = 'Operator override';
    component.onFieldEdit('itemName');
    component.searchCode = '999999';

    component.onItemLookup();

    expect(component.product.itemName).toBe('Operator override');
    expect(component.product.unitPrice).toBe(0);
  });

  it('keeps entered values untouched on an infrastructure failure', () => {
    const component = createComponent({ status: 'found', product: foundProduct });

    component.lookupOutcome = { status: 'error' };
    component.ngOnChanges({ lookupOutcome: { currentValue: component.lookupOutcome, previousValue: null, firstChange: false, isFirstChange: () => false } });

    expect(component.product.itemName).toBe(foundProduct.itemName);
    expect(component.product.unitPrice).toBe(10);
    expect(component.lookupMessage?.kind).toBe('error');
    expect(component.lookupMessage?.text).not.toContain('not found');
  });

  it('clears the DB marker when the operator edits a populated field', () => {
    const component = createComponent({ status: 'found', product: foundProduct });

    component.onFieldEdit('unitPrice');

    expect(component.dbFilled.has('unitPrice')).toBe(false);
  });

  it('emits the populated and edited product on add', () => {
    const component = createComponent({ status: 'found', product: foundProduct });
    const added: Product[] = [];
    component.add.subscribe(p => added.push(p));

    component.product.unitPrice = 12.5;
    component.product.quantity = 3;
    component.onAdd();

    expect(added.length).toBe(1);
    expect(added[0].itemCode).toBe(foundProduct.itemCode);
    expect(added[0].itemNameAr).toBe(foundProduct.itemNameAr);
    expect(added[0].unitPrice).toBe(12.5);
    expect(added[0].quantity).toBe(3);
  });

  it('does not emit an incomplete product', () => {
    const component = new AddProductDialogComponent();
    const added: Product[] = [];
    component.add.subscribe(p => added.push(p));

    component.onAdd();

    expect(added).toEqual([]);
  });
});
