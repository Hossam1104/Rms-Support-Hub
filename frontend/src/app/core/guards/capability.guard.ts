import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { ModuleService } from '../services/module.service';
import { ToastService } from '../services/toast.service';
import { ModuleCapabilities } from '../models';

/**
 * Gates a route on a real Capabilities.* flag read from /api/modules
 * (populated via ModuleService.initialize() at app bootstrap) instead of a
 * hardcoded module-key comparison -- see remediation_plan.md B21.
 * Redirects to the module's order builder with an explanatory toast when
 * the active module doesn't support the capability (e.g.
 * Capabilities.OrderRequests is false for ghc_ecommerce today, pending
 * database credentials -- see GhcEcommerceModule's TODO(db-creds)).
 */
export function capabilityGuard(capability: keyof ModuleCapabilities): CanActivateFn {
  return (route) => {
    const moduleService = inject(ModuleService);
    const router = inject(Router);
    const toast = inject(ToastService);

    const key = route.parent?.paramMap.get('key') ?? route.paramMap.get('key');
    const module = moduleService.modules().find(m => m.key === key);

    if (module?.capabilities?.[capability]) {
      return true;
    }

    toast.showWarning(`This feature isn't available for ${module?.label ?? key} yet.`);
    return router.createUrlTree(['/modules', key, 'order']);
  };
}
