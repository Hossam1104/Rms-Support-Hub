import { Routes } from '@angular/router';
import { capabilityGuard } from './core/guards/capability.guard';

export const routes: Routes = [
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
