import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { ModuleService } from '../services/module.service';
import { ProductionUnlockService } from '../services/production-unlock.service';

/** Protects both builder entry points, including direct URL navigation. */
export const productionBuilderGuard: CanActivateFn = (route) => {
  const moduleService = inject(ModuleService);
  const unlock = inject(ProductionUnlockService);
  const router = inject(Router);
  const moduleKey = route.parent?.paramMap.get('key') ?? route.paramMap.get('key') ?? '';
  const environment = moduleService.selectedEnvironment(moduleKey);
  const orderRequests = () => router.createUrlTree([
    '/tools/online-orders/modules', moduleKey, 'order-requests'
  ]);

  // The module catalog is the authority for both the environment and the
  // module's availability. A direct builder URL must never become an
  // implicitly Testing workspace when that authority is missing or failed to
  // initialize.
  if (!environment) return orderRequests();
  if (environment.environment !== 'Production') return true;
  if (unlock.isUnlocked(moduleKey, environment.key)) return true;

  unlock.open({
    moduleKey,
    environmentKey: environment.key,
    destination: route.routeConfig?.path === 'unicommerce' ? 'unicommerce' : 'order'
  });
  return orderRequests();
};
