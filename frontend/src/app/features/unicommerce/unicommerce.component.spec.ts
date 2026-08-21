import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { UnicommerceComponent } from './unicommerce.component';

describe('UnicommerceComponent', () => {
  async function flushMicrotasks() {
    for (let i = 0; i < 20; i++) await Promise.resolve();
  }

  function createComponent(overrides: Record<string, unknown> = {}) {
    const api = {
      get: vi.fn((path: string) => path.includes('/lookup/consumer')
        ? of({ success: true, data: { firstName: 'Lookup', lastName: 'Consumer', primaryPhoneNumber: '000000000' } })
        : of({})),
      put: vi.fn(() => of({ success: true })),
      post: vi.fn(() => of({ success: true, statusCode: 200, responseText: '', urlSent: 'http://testing.invalid' })),
      ...overrides
    } as any;
    const toast = { showSuccess: vi.fn(), showInfo: vi.fn(), showError: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        { provide: ApiService, useValue: api },
        { provide: ModuleService, useValue: { activeEnvironment: () => ({ key: 'GHC Uni-Commerce Testing' }) } },
        { provide: ToastService, useValue: toast }
      ]
    });

    const component = TestBed.runInInjectionContext(() => new UnicommerceComponent());
    return { component, api, toast };
  }

  it('serializes rapid complete-draft writes so the older response cannot win', async () => {
    const saveResponses: Subject<unknown>[] = [];
    const payloads: any[] = [];
    const { component } = createComponent({
      put: vi.fn((_path: string, payload: unknown) => {
        payloads.push(payload);
        const response = new Subject<unknown>();
        saveResponses.push(response);
        return response.asObservable();
      })
    });

    component.onFieldChange({ fieldName: 'reference_number', value: 'ORDER-A' });
    component.onFieldChange({ fieldName: 'reference_number', value: 'ORDER-B' });
    await flushMicrotasks();

    expect(payloads).toHaveLength(1);
    expect(payloads[0].orderData.reference_number).toBe('ORDER-A');

    saveResponses[0].next({ success: true });
    saveResponses[0].complete();
    await flushMicrotasks();

    expect(payloads).toHaveLength(2);
    expect(payloads[1].orderData.reference_number).toBe('ORDER-B');

    saveResponses[1].next({ success: true });
    saveResponses[1].complete();
    await flushMicrotasks();
    expect(component.compiledJson()).toEqual({});
  });

  it('refreshes compiled JSON only after the corresponding draft is persisted', async () => {
    const saveResponse = new Subject<unknown>();
    const exportResponse = new Subject<Record<string, unknown>>();
    const { component, api } = createComponent({
      put: vi.fn(() => saveResponse.asObservable()),
      get: vi.fn((path: string) => path.includes('/export-json')
        ? exportResponse.asObservable()
        : of({}))
    });

    component.onFieldChange({ fieldName: 'reference_number', value: 'ORDER-A' });
    await flushMicrotasks();
    expect(api.get).not.toHaveBeenCalled();
    expect(component.compiledJson()).toBeNull();

    saveResponse.next({ success: true });
    saveResponse.complete();
    await flushMicrotasks();
    expect(api.get).toHaveBeenCalledWith('modules/ghc_unicommerce/export-json');
    expect(component.compiledJson()).toBeNull();

    exportResponse.next({ reference_number: 'ORDER-A' });
    exportResponse.complete();
    await flushMicrotasks();
    expect(component.compiledJson()).toEqual({ reference_number: 'ORDER-A' });
  });

  it('waits for the latest draft persistence before sending', async () => {
    const saveResponse = new Subject<unknown>();
    const { component, api } = createComponent({
      put: vi.fn(() => saveResponse.asObservable())
    });

    component.onFieldChange({ fieldName: 'reference_number', value: 'ORDER-A' });
    component.onSendOrder();
    await flushMicrotasks();
    expect(api.post).not.toHaveBeenCalled();

    saveResponse.next({ success: true });
    saveResponse.complete();
    await flushMicrotasks();
    expect(api.post).toHaveBeenCalledWith(
      'modules/ghc_unicommerce/send-request',
      { environmentKey: 'GHC Uni-Commerce Testing' }
    );
  });

  it('blocks Send and surfaces a safe error when draft persistence fails', async () => {
    const { component, api, toast } = createComponent({
      put: vi.fn(() => throwError(() => ({ status: 503 })))
    });

    component.onFieldChange({ fieldName: 'reference_number', value: 'UNSAVED-ORDER' });
    component.onSendOrder();
    await flushMicrotasks();

    expect(api.post).not.toHaveBeenCalled();
    expect(component.apiResponse()).toEqual(expect.objectContaining({
      success: false,
      responseText: 'The latest invoice draft could not be saved. The invoice was not sent.'
    }));
    expect(toast.showError).toHaveBeenCalled();
  });

  it('orders row-item add/delete and consumer edits through the same queue', async () => {
    const saveResponses: Subject<unknown>[] = [];
    const payloads: any[] = [];
    const { component, api } = createComponent();
    const row = {
      quantity: 1,
      materialNumber: 'QA-ITEM',
      itemPrice: 10,
      itemDiscount: 0,
      vatPercentage: 0,
      barcode: 'QA-ITEM'
    };

    api.put = vi.fn((_path: string, payload: unknown) => {
      payloads.push(payload);
      const response = new Subject<unknown>();
      saveResponses.push(response);
      return response.asObservable();
    });

    component.onAddRowItem(row);
    component.onDeleteRowItem(0);
    component.onConsumerFieldChange({ fieldName: 'firstName', value: 'Latest' });
    await flushMicrotasks();

    expect(payloads).toHaveLength(1);
    expect(payloads[0].rowItems).toEqual([row]);

    for (let i = 0; i < 3; i++) {
      saveResponses[i].next({ success: true });
      saveResponses[i].complete();
      await flushMicrotasks();
    }

    expect(payloads).toHaveLength(3);
    expect(payloads[1].rowItems).toEqual([]);
    expect(payloads[2]).toEqual(expect.objectContaining({
      rowItems: [],
      consumer: expect.objectContaining({ firstName: 'Latest' })
    }));
  });

  it('persists consumer details populated by a database lookup', async () => {
    const { component, api } = createComponent();

    component.onLookupConsumer('000000000');
    await flushMicrotasks();

    expect(api.put).toHaveBeenCalledWith(
      'modules/ghc_unicommerce/state',
      expect.objectContaining({
        consumer: expect.objectContaining({ firstName: 'Lookup', lastName: 'Consumer' })
      })
    );
  });
});
