import { TestBed } from '@angular/core/testing';
import { ThemeService, THEME_STORAGE_KEY } from './theme.service';

const LIGHT_QUERY = '(prefers-color-scheme: light)';

/**
 * Installs a controllable `window.matchMedia` stub. Returns a setter that
 * flips the simulated system theme and fires the registered change listeners.
 */
function stubSystemTheme(initialLight: boolean) {
  const state = { light: initialLight };
  const listeners: ((event: { matches: boolean }) => void)[] = [];

  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn((query: string) => ({
      matches: query === LIGHT_QUERY ? state.light : false,
      media: query,
      addEventListener: (_type: string, cb: (event: { matches: boolean }) => void) => {
        if (query === LIGHT_QUERY) listeners.push(cb);
      },
      removeEventListener: () => undefined
    }))
  });

  return {
    setSystemLight(light: boolean) {
      state.light = light;
      listeners.forEach(cb => cb({ matches: light }));
    }
  };
}

describe('ThemeService', () => {
  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
    Reflect.deleteProperty(window, 'matchMedia');
  });

  it('falls back to dark when there is no stored or system light preference', () => {
    stubSystemTheme(false);
    const service = TestBed.inject(ThemeService);

    expect(service.theme()).toBe('dark');
  });

  it('falls back to the system light theme when no user choice is stored', () => {
    stubSystemTheme(true);
    const service = TestBed.inject(ThemeService);

    expect(service.theme()).toBe('light');
  });

  it('applies the theme to the document root', () => {
    stubSystemTheme(false);
    const service = TestBed.inject(ThemeService);
    service.setTheme('light');
    TestBed.tick();

    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('persists an explicit user choice under the namespaced key', () => {
    stubSystemTheme(false);
    const service = TestBed.inject(ThemeService);
    service.setTheme('light');

    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');
    expect(localStorage.getItem('theme')).toBeNull();
  });

  it('restores a stored user choice over the system preference', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    stubSystemTheme(true);
    const service = TestBed.inject(ThemeService);

    expect(service.theme()).toBe('dark');
  });

  it('does not persist the system fallback as a user choice', () => {
    stubSystemTheme(true);
    TestBed.inject(ThemeService);
    TestBed.tick();

    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
  });

  it('tracks system changes while no user choice is stored', () => {
    const system = stubSystemTheme(false);
    const service = TestBed.inject(ThemeService);

    system.setSystemLight(true);
    expect(service.theme()).toBe('light');

    system.setSystemLight(false);
    expect(service.theme()).toBe('dark');
  });

  it('keeps an explicit user choice when the system theme changes', () => {
    const system = stubSystemTheme(false);
    const service = TestBed.inject(ThemeService);
    service.setTheme('dark');

    system.setSystemLight(true);
    expect(service.theme()).toBe('dark');
  });

  it('toggles between dark and light', () => {
    stubSystemTheme(false);
    const service = TestBed.inject(ThemeService);

    service.toggleTheme();
    expect(service.theme()).toBe('light');
    service.toggleTheme();
    expect(service.theme()).toBe('dark');
  });

  it('keeps theme selection usable when browser storage is blocked', () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });

    try {
      stubSystemTheme(false);
      const service = TestBed.inject(ThemeService);

      expect(service.theme()).toBe('dark');
      expect(() => service.setTheme('light')).not.toThrow();
      expect(service.theme()).toBe('light');
    } finally {
      getItem.mockRestore();
      setItem.mockRestore();
    }
  });
});
