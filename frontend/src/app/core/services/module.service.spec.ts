import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ModuleDto, EnvironmentDto } from '../models';
import { ApiService } from './api.service';
import { ModuleService, orderModulesForDisplay } from './module.service';

const asModule = (key: string): ModuleDto => ({ key, label: key } as unknown as ModuleDto);

describe('orderModulesForDisplay', () => {
  it('pins UPC to the front whatever order the backend returned', () => {
    const ordered = orderModulesForDisplay([
      asModule('ghc_ecommerce'),
      asModule('ghc_unicommerce'),
      asModule('upc_ecommerce')
    ]);

    expect(ordered.map(m => m.key)).toEqual(['upc_ecommerce', 'ghc_ecommerce', 'ghc_unicommerce']);
  });

  it('keeps the backend order for everything behind the pinned keys', () => {
    const ordered = orderModulesForDisplay([
      asModule('upc_ecommerce'),
      asModule('ghc_unicommerce'),
      asModule('ghc_ecommerce')
    ]);

    expect(ordered.map(m => m.key)).toEqual(['upc_ecommerce', 'ghc_unicommerce', 'ghc_ecommerce']);
  });

  it('does not mutate the input array and tolerates a missing UPC module', () => {
    const input = [asModule('ghc_ecommerce'), asModule('ghc_unicommerce')];

    const ordered = orderModulesForDisplay(input);

    expect(ordered).not.toBe(input);
    expect(input.map(m => m.key)).toEqual(['ghc_ecommerce', 'ghc_unicommerce']);
    expect(ordered.map(m => m.key)).toEqual(['ghc_ecommerce', 'ghc_unicommerce']);
  });

  it('returns an empty list unchanged', () => {
    expect(orderModulesForDisplay([])).toEqual([]);
  });
});

describe('ModuleService environment selection', () => {
  const testing: EnvironmentDto = {
    key: 'UPC Testing',
    environment: 'Testing',
    isDefault: true
  } as EnvironmentDto;
  const production: EnvironmentDto = {
    key: 'UPC Production',
    environment: 'Production',
    isDefault: false
  } as EnvironmentDto;
  const upc: ModuleDto = {
    key: 'upc_ecommerce',
    environments: [production, testing]
  } as ModuleDto;

  let service: ModuleService;
  let api: { get: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    localStorage.clear();
    api = { get: vi.fn().mockReturnValue(of({ module: upc, state: {} })) };
    TestBed.configureTestingModule({
      providers: [
        ModuleService,
        { provide: ApiService, useValue: api }
      ]
    });
    service = TestBed.inject(ModuleService);
  });

  afterEach(() => localStorage.clear());

  it('persists a landing-page selection under the existing module-scoped key', () => {
    service.selectEnvironment(production, upc);

    expect(service.activeModule()).toBe(upc);
    expect(service.activeEnvironment()).toBe(production);
    expect(localStorage.getItem('onlineOrderTool.activeEnvironment.upc_ecommerce')).toBe('UPC Production');
  });

  it('restores the outside-module selection when the module shell loads', () => {
    service.selectEnvironment(production, upc);

    service.loadModuleDetails('upc_ecommerce').subscribe();

    expect(service.activeEnvironment()?.key).toBe('UPC Production');
    expect(api.get).toHaveBeenCalledWith('modules/upc_ecommerce');
  });

  it('keeps Testing as the default when no environment was persisted', () => {
    service.modules.set([upc]);

    service.loadModuleDetails('upc_ecommerce').subscribe();

    expect(service.activeEnvironment()?.key).toBe('UPC Testing');
  });
});
