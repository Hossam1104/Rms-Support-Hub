import { Route } from '@angular/router';
import { routes } from './app.routes';
import { TOOL_ROUTE_DATA, ToolRouteData } from './core/models';

describe('application order-request routing contract', () => {
  function moduleChildren() {
    const moduleRoute = routes.find(route => route.path === 'modules/:key');
    return moduleRoute?.children || [];
  }

  it('exposes one guarded canonical list and a route-level order id', () => {
    const list = moduleChildren().find(route => route.path === 'order-requests');
    expect(list).toBeTruthy();
    expect(list?.data).toEqual({ breadcrumb: 'Order Requests' });
    expect(list?.canActivate).toBeTruthy();
    expect(list?.children?.map(route => route.path)).toEqual([':orderId']);
  });

  it('keeps workspace tab labels in route metadata for shared shell breadcrumbs', () => {
    expect(moduleChildren().find(route => route.path === 'order')?.data).toEqual({ breadcrumb: 'Order Builder' });
    expect(moduleChildren().find(route => route.path === 'unicommerce')?.data).toEqual({ breadcrumb: 'Invoice Builder' });
  });

  it('keeps old requests and validation links as compatibility redirects', () => {
    const children = moduleChildren();
    const oldDetail = children.find(route => route.path === 'requests/:requestId');
    const oldRequests = children.find(route => route.path === 'requests');
    const oldValidation = children.find(route => route.path === 'validation');
    expect(oldDetail?.redirectTo).toBe('order-requests/:requestId');
    expect(oldDetail?.pathMatch).toBe('full');
    expect(oldRequests?.redirectTo).toBe('order-requests');
    expect(oldRequests?.pathMatch).toBe('full');
    expect(oldValidation?.redirectTo).toBe('order-requests');
    expect(oldValidation?.pathMatch).toBe('full');
  });
});

describe('RMS+ Support Hub route skeleton', () => {
  function routeAt(path: string): Route | undefined {
    return routes.find(route => route.path === path);
  }

  function toolData(path: string): ToolRouteData {
    return routeAt(path)?.data as ToolRouteData;
  }

  it('serves the hub dashboard placeholder at the root', () => {
    const hub = routeAt('');
    expect(hub?.loadComponent).toBeTruthy();
    expect(hub?.redirectTo).toBeUndefined();
    const data = hub?.data as ToolRouteData;
    expect(data.title).toBe('RMS+ Support Hub');
    expect(data.breadcrumb).toBe('Dashboard');
    expect(data.status).toBe('available');
    expect(data.accent).toBeTruthy();
  });

  it('lazy-loads every tool route with typed metadata', () => {
    for (const path of ['tools/prompt-studio', 'tools/online-orders', 'tools/pos-maintenance']) {
      const route = routeAt(path);
      expect(route?.redirectTo, path).toBeUndefined();
      const data = route?.data as ToolRouteData;
      expect(data.title, path).toBeTruthy();
      expect(data.breadcrumb, path).toBeTruthy();
      expect(['available', 'migration-pending'], path).toContain(data.status);
      expect(['brand', 'info', 'amber'], path).toContain(data.accent);
    }
    // Prompt Studio and POS Maintenance are lazy feature routes; Online Orders
    // is a componentless parent whose children lazy-load individually.
    expect(routeAt('tools/prompt-studio')?.loadChildren).toBeTruthy();
    expect(routeAt('tools/pos-maintenance')?.loadComponent).toBeTruthy();
  });

  it('keeps POS Maintenance pending while migrated tools are available', () => {
    expect(toolData('tools/pos-maintenance')).toEqual({ ...TOOL_ROUTE_DATA.posMaintenance });
    expect(toolData('tools/pos-maintenance').status).toBe('migration-pending');
    expect(toolData('tools/prompt-studio')).toEqual({ ...TOOL_ROUTE_DATA.promptStudio });
    expect(toolData('tools/prompt-studio').status).toBe('available');
    expect(toolData('tools/online-orders').status).toBe('available');
  });

  it('mounts the Online Order workspace under /tools/online-orders', () => {
    const children = routeAt('tools/online-orders')?.children || [];
    const picker = children.find(route => route.path === '');
    expect(picker?.loadComponent).toBeTruthy();
    const workspace = children.find(route => route.path === 'modules/:key');
    expect(workspace?.loadComponent).toBeTruthy();
    expect(workspace?.children?.map(route => route.path)).toContain('order-requests');
  });

  it('keeps the legacy /modules/:key mount so existing URLs still resolve', () => {
    const legacy = routeAt('modules/:key');
    expect(legacy?.loadComponent).toBeTruthy();
    expect(legacy?.children?.map(route => route.path)).toContain('order-requests');
  });

  it('keeps capability guarding on Order Requests in both mounts', () => {
    const mounts = [
      routeAt('tools/online-orders')?.children?.find(route => route.path === 'modules/:key'),
      routeAt('modules/:key')
    ];

    for (const mount of mounts) {
      const requests = mount?.children?.find(route => route.path === 'order-requests');
      expect(requests?.canActivate).toBeTruthy();
    }
  });

  it('keeps the wildcard redirect last', () => {
    const wildcard = routes[routes.length - 1];
    expect(wildcard.path).toBe('**');
    expect(wildcard.redirectTo).toBe('');
  });
});
