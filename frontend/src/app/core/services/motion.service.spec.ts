import { TestBed } from '@angular/core/testing';
import { MotionService, MOTION_STORAGE_KEY } from './motion.service';

const MOTION_QUERY = '(prefers-reduced-motion: reduce)';

/**
 * Installs a controllable `window.matchMedia` stub. Returns a setter that
 * flips the simulated system reduced-motion flag and fires the registered
 * change listeners.
 */
function stubSystemMotion(initialReduced: boolean) {
  const state = { reduced: initialReduced };
  const listeners: ((event: { matches: boolean }) => void)[] = [];

  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn((query: string) => ({
      matches: query === MOTION_QUERY ? state.reduced : false,
      media: query,
      addEventListener: (_type: string, cb: (event: { matches: boolean }) => void) => {
        if (query === MOTION_QUERY) listeners.push(cb);
      },
      removeEventListener: () => undefined
    }))
  });

  return {
    setSystemReduced(reduced: boolean) {
      state.reduced = reduced;
      listeners.forEach(cb => cb({ matches: reduced }));
    }
  };
}

describe('MotionService', () => {
  afterEach(() => {
    localStorage.removeItem(MOTION_STORAGE_KEY);
    document.documentElement.removeAttribute('data-motion');
    Reflect.deleteProperty(window, 'matchMedia');
  });

  it('follows the system reduced-motion preference by default', () => {
    stubSystemMotion(true);
    const service = TestBed.inject(MotionService);

    expect(service.preference()).toBe('system');
    expect(service.reducedMotion()).toBe(true);
  });

  it('allows full motion by default when the system has no preference', () => {
    stubSystemMotion(false);
    const service = TestBed.inject(MotionService);

    expect(service.reducedMotion()).toBe(false);
  });

  it('stamps the resolved state on the document root', () => {
    stubSystemMotion(true);
    TestBed.inject(MotionService);
    TestBed.tick();

    expect(document.documentElement.getAttribute('data-motion')).toBe('reduce');
  });

  it('lets a user reduce override win over a system no-preference', () => {
    stubSystemMotion(false);
    const service = TestBed.inject(MotionService);
    service.setPreference('reduce');
    TestBed.tick();

    expect(service.reducedMotion()).toBe(true);
    expect(document.documentElement.getAttribute('data-motion')).toBe('reduce');
  });

  it('lets a user full override win over the system reduced preference', () => {
    stubSystemMotion(true);
    const service = TestBed.inject(MotionService);
    service.setPreference('full');
    TestBed.tick();

    expect(service.reducedMotion()).toBe(false);
    expect(document.documentElement.getAttribute('data-motion')).toBe('full');
  });

  it('persists an explicit user choice under the namespaced key', () => {
    stubSystemMotion(false);
    const service = TestBed.inject(MotionService);
    service.setPreference('reduce');

    expect(localStorage.getItem(MOTION_STORAGE_KEY)).toBe('reduce');
  });

  it('restores a stored user choice on the next session', () => {
    localStorage.setItem(MOTION_STORAGE_KEY, 'full');
    stubSystemMotion(true);
    const service = TestBed.inject(MotionService);

    expect(service.preference()).toBe('full');
    expect(service.reducedMotion()).toBe(false);
  });

  it('tracks system changes while the preference stays on system', () => {
    const system = stubSystemMotion(false);
    const service = TestBed.inject(MotionService);

    system.setSystemReduced(true);
    expect(service.reducedMotion()).toBe(true);

    system.setSystemReduced(false);
    expect(service.reducedMotion()).toBe(false);
  });

  it('ignores system changes while a user override is active', () => {
    const system = stubSystemMotion(true);
    const service = TestBed.inject(MotionService);
    service.setPreference('full');

    system.setSystemReduced(false);
    system.setSystemReduced(true);
    expect(service.reducedMotion()).toBe(false);
  });

  it('cycles system -> reduce -> full -> system', () => {
    stubSystemMotion(false);
    const service = TestBed.inject(MotionService);

    service.cyclePreference();
    expect(service.preference()).toBe('reduce');
    service.cyclePreference();
    expect(service.preference()).toBe('full');
    service.cyclePreference();
    expect(service.preference()).toBe('system');
  });
});
