import { Injectable, computed, signal, effect } from '@angular/core';

/**
 * Motion preference: `system` follows `prefers-reduced-motion` live;
 * `reduce` and `full` are explicit user choices that override it.
 */
export type MotionPreference = 'system' | 'reduce' | 'full';

/** Namespaced storage key for the user's explicit motion choice. */
export const MOTION_STORAGE_KEY = 'qa-support-hub:motion';

const MOTION_QUERY = '(prefers-reduced-motion: reduce)';

/**
 * Single global motion preference service. Stamps `data-motion="reduce" |
 * "full"` on <html>; the reduced-motion rules in `_tokens.css`,
 * `_animations.css`, and `_gradients.css` key off that attribute (falling
 * back to the media query before the service runs), so an explicit user
 * choice can override the system preference in either direction.
 */
@Injectable({
  providedIn: 'root'
})
export class MotionService {
  readonly preference = signal<MotionPreference>(this.getStoredPreference());
  private readonly systemReduced = signal(this.readSystemReduced());

  readonly reducedMotion = computed(() => {
    const preference = this.preference();
    if (preference === 'reduce') return true;
    if (preference === 'full') return false;
    return this.systemReduced();
  });

  constructor() {
    effect(() => {
      document.documentElement.setAttribute('data-motion', this.reducedMotion() ? 'reduce' : 'full');
    });
    this.watchSystemMotion();
  }

  /** Explicit user choice; persists and overrides the system preference. */
  setPreference(preference: MotionPreference) {
    try {
      localStorage.setItem(MOTION_STORAGE_KEY, preference);
    } catch {
      // Motion selection remains available when browser storage is blocked.
    }
    this.preference.set(preference);
  }

  /** Cycle used by the shell toggle: system -> reduce -> full -> system. */
  cyclePreference() {
    const order: MotionPreference[] = ['system', 'reduce', 'full'];
    const next = order[(order.indexOf(this.preference()) + 1) % order.length];
    this.setPreference(next);
  }

  private getStoredPreference(): MotionPreference {
    try {
      const saved = localStorage.getItem(MOTION_STORAGE_KEY);
      return saved === 'reduce' || saved === 'full' ? saved : 'system';
    } catch {
      return 'system';
    }
  }

  private readSystemReduced(): boolean {
    return typeof window !== 'undefined'
      && !!window.matchMedia?.(MOTION_QUERY).matches;
  }

  private watchSystemMotion() {
    if (typeof window === 'undefined' || !window.matchMedia) return;
    window.matchMedia(MOTION_QUERY)
      .addEventListener?.('change', () => this.systemReduced.set(this.readSystemReduced()));
  }
}
