import { Routes } from '@angular/router';
import { LandingComponent } from './features/landing/landing.component';
import { ModuleShellComponent } from './features/module-shell/module-shell.component';
import { FlatOrderComponent } from './features/flat-order/flat-order.component';
import { UnicommerceComponent } from './features/unicommerce/unicommerce.component';
import { OrderRequestsComponent } from './features/order-requests/order-requests.component';
import { OrderValidationComponent } from './features/order-validation/order-validation.component';

export const routes: Routes = [
  { path: '', component: LandingComponent },
  {
    path: 'modules/:key',
    component: ModuleShellComponent,
    children: [
      { path: '', redirectTo: 'order', pathMatch: 'full' },
      { path: 'order', component: FlatOrderComponent },
      { path: 'unicommerce', component: UnicommerceComponent },
      { path: 'api', component: FlatOrderComponent },
      { path: 'database', component: FlatOrderComponent },
      { path: 'test', component: FlatOrderComponent },
      { path: 'requests', component: OrderRequestsComponent },
      { path: 'validation', component: OrderValidationComponent }
    ]
  },
  { path: '**', redirectTo: '' }
];
