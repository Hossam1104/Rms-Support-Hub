import { RowItemsTableComponent } from './row-items-table.component';

describe('RowItemsTableComponent', () => {
  it('keeps display-only invoice rollups aligned with the existing row formulas', () => {
    const component = new RowItemsTableComponent();
    component.rowItems = [
      { materialNumber: 'MAT-1', barcode: '111', quantity: 2, itemPrice: 50, itemDiscount: 10, vatPercentage: 15 },
      { materialNumber: 'MAT-2', barcode: '222', quantity: 1, itemPrice: 20, itemDiscount: 0, vatPercentage: 15 }
    ];

    expect(component.totalQuantity()).toBe(3);
    expect(component.totalDiscount()).toBe(20);
    expect(component.totalGross()).toBe(120);
    expect(component.totalVat()).toBe(15);
    expect(component.totalNet()).toBe(115);
  });
});
