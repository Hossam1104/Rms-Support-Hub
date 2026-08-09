import { TestBed } from '@angular/core/testing';
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

  const sampleProducts: Product[] = [
    { itemCode: 'A1', itemName: 'Widget', quantity: 2, unitPrice: 20, vatPercentage: 15, discount: 0 }
  ];

  it('renders no internal "Products" heading -- that lives only in the host ui-section title', () => {
    const fixture = TestBed.createComponent(ProductsTableComponent);
    fixture.componentInstance.products = sampleProducts;
    fixture.detectChanges();

    const text = (fixture.nativeElement.textContent as string).trim();
    expect(text.split(/\s+/).filter(w => w === 'Products').length).toBe(0);
  });

  it('emits deleteProduct with the row index when the delete action is pressed', () => {
    const fixture = TestBed.createComponent(ProductsTableComponent);
    fixture.componentInstance.products = sampleProducts;
    const deleted: number[] = [];
    fixture.componentInstance.deleteProduct.subscribe(i => deleted.push(i));
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector('button[aria-label="Remove product"]') as HTMLButtonElement;
    deleteButton.click();

    expect(deleted).toEqual([0]);
  });

  it('shows the empty state and lets Add product be triggered from it when there are no rows', () => {
    const fixture = TestBed.createComponent(ProductsTableComponent);
    fixture.componentInstance.products = [];
    let opened = false;
    fixture.componentInstance.openAddDialog.subscribe(() => opened = true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.pt__list')).toBeNull();
    const addButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    addButton.click();

    expect(opened).toBe(true);
  });

  it('renders name, Arabic name, code and row total inside one product row entity', () => {
    const fixture = TestBed.createComponent(ProductsTableComponent);
    fixture.componentInstance.products = [
      { itemCode: '000000000000103532', itemName: 'Vittoria 5/160Mg 28Tabs.', itemNameAr: 'فيتوريا', quantity: 1, unitPrice: 56.85, vatPercentage: 0, discount: 0 }
    ];
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.prow');
    expect(rows.length).toBe(1);

    const row = rows[0] as HTMLElement;
    expect(row.querySelector('.prow__name')?.textContent?.trim()).toBe('Vittoria 5/160Mg 28Tabs.');
    expect(row.querySelector('.prow__ar')?.textContent?.trim()).toBe('فيتوريا');
    expect(row.querySelector('.prow__code')?.textContent?.trim()).toBe('000000000000103532');
    expect(row.querySelector('.cell--total')?.textContent).toContain('56.85');
    expect((row.querySelector('.cell--qty input') as HTMLInputElement).value).toBe('1');
  });
});
