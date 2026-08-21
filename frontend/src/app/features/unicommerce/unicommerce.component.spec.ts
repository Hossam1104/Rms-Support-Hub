import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { UnicommerceComponent } from './unicommerce.component';

describe('UnicommerceComponent', () => {
  function createComponent() {
    const api = {
      get: vi.fn((path: string) => path.includes('/lookup/consumer')
        ? of({ success: true, data: { firstName: 'Lookup', lastName: 'Consumer', primaryPhoneNumber: '000000000' } })
        : of({})),
      put: vi.fn(() => of({ success: true }))
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: ApiService, useValue: api },
        { provide: ModuleService, useValue: { activeEnvironment: () => ({ key: 'GHC Uni-Commerce Testing' }) } },
        { provide: ToastService, useValue: { showSuccess: vi.fn(), showInfo: vi.fn(), showError: vi.fn() } }
      ]
    });

    const component = TestBed.runInInjectionContext(() => new UnicommerceComponent());
    return { component, api };
  }

  it('persists order and row-item edits as one complete draft', () => {
    const { component, api } = createComponent();
    const row = {
      quantity: 1,
      materialNumber: 'QA-ITEM',
      itemPrice: 10,
      itemDiscount: 0,
      vatPercentage: 0,
      barcode: 'QA-ITEM'
    };

    component.onFieldChange({ fieldName: 'reference_number', value: 'QA-ORDER' });
    component.onAddRowItem(row);

    expect(api.put).toHaveBeenLastCalledWith(
      'modules/ghc_unicommerce/state',
      expect.objectContaining({
        orderData: { reference_number: 'QA-ORDER' },
        rowItems: [row]
      })
    );
  });

  it('persists consumer details populated by a database lookup', () => {
    const { component, api } = createComponent();

    component.onLookupConsumer('000000000');

    expect(api.put).toHaveBeenCalledWith(
      'modules/ghc_unicommerce/state',
      expect.objectContaining({
        consumer: expect.objectContaining({ firstName: 'Lookup', lastName: 'Consumer' })
      })
    );
  });
});
