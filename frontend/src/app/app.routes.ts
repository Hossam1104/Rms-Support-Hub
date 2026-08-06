import { Route, Routes } from '@angular/router';
import { capabilityGuard } from './core/guards/capability.guard';
import { TOOL_ROUTE_DATA, ToolRouteData } from './core/models';
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

/** The Online Order workspace, mounted canonically under
 * /tools/online-orders and re-mounted at the legacy /modules/:key path so
 * pre-hub bookmarks and deep links keep resolving unchanged (Session 01
 * compatibility). A factory keeps the two mounts independent. */
const onlineOrdersWorkspaceRoute = (): Route => ({
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
      path: 'order-requests',
      canActivate: [capabilityGuard('orderRequests')],
      loadComponent: () => import('./features/order-requests/order-requests.component').then(m => m.OrderRequestsComponent),
      children: [
        {
          path: ':orderId',
          loadComponent: () => import('./features/order-requests/components/order-request-details.component').then(m => m.OrderRequestDetailsComponent)
        }
      ]
    },
    {
      path: 'requests/:requestId',
      redirectTo: 'order-requests/:requestId',
      pathMatch: 'full'
    },
    {
      path: 'requests',
      redirectTo: 'order-requests',
      pathMatch: 'full'
    },
    {
      path: 'validation',
      redirectTo: 'order-requests',
      pathMatch: 'full'
    }
  ]
});

export const routes: Routes = [
  ...kitchenSinkRoutes,
  {
    path: '',
    loadComponent: () => import('./features/hub/hub.component').then(m => m.HubComponent),
    data: {
      title: 'QA Support Hub',
      breadcrumb: 'Dashboard',
      status: 'available',
      accent: 'brand'
    } satisfies ToolRouteData
  },
  {
    path: 'tools/prompt-studio',
    loadComponent: () => import('./features/prompt-studio/prompt-studio-placeholder.component').then(m => m.PromptStudioPlaceholderComponent),
    data: { ...TOOL_ROUTE_DATA.promptStudio } satisfies ToolRouteData
  },
  {
    path: 'tools/online-orders',
    data: { ...TOOL_ROUTE_DATA.onlineOrders } satisfies ToolRouteData,
    children: [
      {
        path: '',
        loadComponent: () => import('./features/landing/landing.component').then(m => m.LandingComponent)
      },
      onlineOrdersWorkspaceRoute()
    ]
  },
  {
    path: 'tools/pos-maintenance',
    loadComponent: () => import('./features/pos-maintenance/pos-maintenance-placeholder.component').then(m => m.PosMaintenancePlaceholderComponent),
    data: { ...TOOL_ROUTE_DATA.posMaintenance } satisfies ToolRouteData
  },
  // Legacy compatibility mount: pre-hub Online Order URLs keep working.
  onlineOrdersWorkspaceRoute(),
  { path: '**', redirectTo: '' }
];
