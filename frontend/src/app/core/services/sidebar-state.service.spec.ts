import { SidebarStateService } from './sidebar-state.service';

describe('SidebarStateService', () => {
  beforeEach(() => localStorage.removeItem('order-tool.sidebar-collapsed'));

  it('publishes and persists the collapsed state', () => {
    const service = new SidebarStateService();

    expect(service.collapsed()).toBe(false);
    service.toggle();
    expect(service.collapsed()).toBe(true);
    expect(localStorage.getItem('order-tool.sidebar-collapsed')).toBe('true');

    const reloaded = new SidebarStateService();
    expect(reloaded.collapsed()).toBe(true);
    reloaded.setCollapsed(false);
    expect(localStorage.getItem('order-tool.sidebar-collapsed')).toBe('false');
  });
});
