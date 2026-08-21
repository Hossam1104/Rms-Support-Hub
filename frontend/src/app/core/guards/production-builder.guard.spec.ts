import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, convertToParamMap } from '@angular/router';
import { ProductionUnlockService } from '../services/production-unlock.service';
import { ModuleService } from '../services/module.service';
import { productionBuilderGuard } from './production-builder.guard';

describe('productionBuilderGuard', () => {
  function route(path: 'order' | 'unicommerce'): ActivatedRouteSnapshot {
    return {
      paramMap: convertToParamMap({ key: 'upc_ecommerce' }),
      parent: { paramMap: convertToParamMap({ key: 'upc_ecommerce' }) },
      routeConfig: { path }
    } as unknown as ActivatedRouteSnapshot;
  }

  function setup(environment: 'Testing' | 'Production', unlocked: boolean) {
    const redirect = {} as UrlTree;
    const moduleService = { selectedEnvironment: vi.fn(() => ({ key: `UPC ${environment}`, environment })) };
    const unlock = {
      isUnlocked: vi.fn(() => unlocked),
      open: vi.fn()
    };
    const router = { createUrlTree: vi.fn(() => redirect) };
    TestBed.configureTestingModule({
      providers: [
        { provide: ModuleService, useValue: moduleService },
        { provide: ProductionUnlockService, useValue: unlock },
        { provide: Router, useValue: router }
      ]
    });
    return { redirect, moduleService, unlock, router };
  }

  const state = {} as RouterStateSnapshot;

  it('leaves Testing builders immediately available', () => {
    const dependencies = setup('Testing', false);

    const result = TestBed.runInInjectionContext(() => productionBuilderGuard(route('order'), state));

    expect(result).toBe(true);
    expect(dependencies.unlock.open).not.toHaveBeenCalled();
  });

  it('redirects a locked Production deep link and opens the unlock dialog', () => {
    const dependencies = setup('Production', false);

    const result = TestBed.runInInjectionContext(() => productionBuilderGuard(route('unicommerce'), state));

    expect(result).toBe(dependencies.redirect);
    expect(dependencies.unlock.open).toHaveBeenCalledWith({
      moduleKey: 'upc_ecommerce',
      environmentKey: 'UPC Production',
      destination: 'unicommerce'
    });
    expect(dependencies.router.createUrlTree).toHaveBeenCalledWith([
      '/tools/online-orders/modules', 'upc_ecommerce', 'order-requests'
    ]);
  });

  it('allows a Production deep link only after the matching in-memory unlock', () => {
    const dependencies = setup('Production', true);

    const result = TestBed.runInInjectionContext(() => productionBuilderGuard(route('order'), state));

    expect(result).toBe(true);
    expect(dependencies.unlock.isUnlocked).toHaveBeenCalledWith('upc_ecommerce', 'UPC Production');
  });
});
