import { ProductsTableComponent } from './products-table.component';
import { Product } from '../../../core/models';

describe('ProductsTableComponent', () => {
  it('computes the row total with the verified server convention (Product.cs EstimatedTotal)', () => {
    const component = new ProductsTableComponent();
    const product: Product = {
      itemCode: 'X',
      itemName: 'X',
      quantity: 2,
      unitPrice: 50,
      vatPercentage: 15,
      discount: 10
    };

    // rowSubtotal = 2 * 50 = 100; discount is row-level, applied ONCE;
    // vat = round((100 - 10) * 0.15, 2) = 13.5; total = 100 - 10 + 13.5.
    // The retired per-unit formula would have produced 92.00.
    expect(component.getProductTotal(product)).toBe(103.5);
  });

  it('handles a zero-quantity row without NaN', () => {
    const component = new ProductsTableComponent();
    const product: Product = {
      itemCode: 'X',
      itemName: 'X',
      quantity: 0,
      unitPrice: 50,
      vatPercentage: 15,
      discount: 0
    };

    // rowSubtotal = 0; vat = round((0 - 0) * 0.15, 2) = 0; total = 0.
    expect(component.getProductTotal(product)).toBe(0);
  });

  it('emits one committed row patch instead of reacting to raw keypresses', () => {
    const component = new ProductsTableComponent();
    const updates: unknown[] = [];
    component.updateProduct.subscribe(update => updates.push(update));

    component.onEdit(0, 'quantity', { target: { value: '4' } } as unknown as Event);

    expect(updates).toEqual([{ index: 0, patch: { quantity: 4 } }]);
  });
});
