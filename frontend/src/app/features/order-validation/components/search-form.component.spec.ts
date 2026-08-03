import { SearchFormComponent } from './search-form.component';

describe('SearchFormComponent', () => {
  it('emits the current database filters without changing their types', () => {
    const component = new SearchFormComponent();
    component.setText('orderNumber', 'UPC-998822');
    component.setText('phone', '0500000000');
    component.setText('branchCode', '201');
    component.setStatus('9');

    const emitted: unknown[] = [];
    component.search.subscribe(value => emitted.push(value));
    component.onSearch();

    expect(emitted).toEqual([{
      orderNumber: 'UPC-998822', phone: '0500000000', branchCode: '201', status: 9
    }]);
  });

  it('resets all filters and emits the cleared search', () => {
    const component = new SearchFormComponent();
    component.setText('orderNumber', 'UPC-998822');
    component.setStatus('1');
    const emitted: unknown[] = [];
    component.search.subscribe(value => emitted.push(value));

    component.resetFilters();

    expect(component.filters).toEqual({ orderNumber: '', phone: '', branchCode: '', status: null });
    expect(emitted).toEqual([{ orderNumber: '', phone: '', branchCode: '', status: null }]);
  });
});
