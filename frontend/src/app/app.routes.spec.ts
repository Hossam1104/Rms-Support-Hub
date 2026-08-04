import { routes } from './app.routes';

describe('application order-request routing contract', () => {
  function moduleChildren() {
    const moduleRoute = routes.find(route => route.path === 'modules/:key');
    return moduleRoute?.children || [];
  }

  it('exposes one guarded canonical list and a route-level order id', () => {
    const list = moduleChildren().find(route => route.path === 'order-requests');
    expect(list).toBeTruthy();
    expect(list?.canActivate).toBeTruthy();
    expect(list?.children?.map(route => route.path)).toEqual([':orderId']);
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
