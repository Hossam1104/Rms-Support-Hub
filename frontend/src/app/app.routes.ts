import { Routes } from '@angular/router';
import { capabilityGuard } from './core/guards/capability.guard';
import { environment } from '../environments/environment';

/** Dev-only -- environment.production is a build-time constant (see
 * fileReplacements in angular.json), so the production build can prove this
 * branch is dead and tree-shakes the kitchen-sink chunk out entirely. */
const kitchenSinkRoutes: Routes = environment.production ? [] : [
  {
    path: '_kitchen-sink',
    loadComponent: () => import('./features/kitchen-sink/kitchen-sink.component').then(m => m.KitchenSinkComponent)
  }
];

export const routes: Routes = [
  ...kitchenSinkRoutes,
  {
    path: '',
    loadComponent: () => import('./features/landing/landing.component').then(m => m.LandingComponent)
  },
  {
    path: 'modules/:key',
    loadComponent: () => import('./features/module-shell/module-shell.component').then(m => m.ModuleShellComponent),
    children: [
      { path: '', redirectTo: 'order', pathMatch: 'full' },
      {
        path: 'order',
        loadComponent: () => import('./features/flat-order/flat-order.component').then(m => m.FlatOrderComponent)
      },
      {
        path: 'unicommerce',
        loadComponent: () => import('./features/unicommerce/unicommerce.component').then(m => m.UnicommerceComponent)
      },
      {
        path: 'requests',
        canActivate: [capabilityGuard('orderRequests')],
        loadComponent: () => import('./features/order-requests/order-requests.component').then(m => m.OrderRequestsComponent)
      },
      {
        path: 'validation',
        canActivate: [capabilityGuard('orderRequests')],
        loadComponent: () => import('./features/order-validation/order-validation.component').then(m => m.OrderValidationComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
