import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'dark' | 'light';

/** Namespaced storage key for the user's explicit theme choice. */
export const THEME_STORAGE_KEY = 'qa-support-hub:theme';

/**
 * Single global light/dark theme service. Applies `data-theme` to <html>.
 *
 * Resolution order: an explicit user choice persisted in localStorage wins;
 * otherwise the system `prefers-color-scheme` is followed live. Only explicit
 * choices are persisted, so the system fallback keeps tracking OS changes
 * until the user picks a theme.
 */
@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  readonly theme = signal<Theme>(this.getInitialTheme());

  constructor() {
    effect(() => {
      document.documentElement.setAttribute('data-theme', this.theme());
    });
    this.watchSystemTheme();
  }

  /** Explicit user choice; persists and overrides the system preference. */
  setTheme(theme: Theme) {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
    this.theme.set(theme);
  }

  toggleTheme() {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }

  private getInitialTheme(): Theme {
    return this.getUserPreference() ?? this.systemTheme();
  }

  private getUserPreference(): Theme | null {
    const saved = localStorage.getItem(THEME_STORAGE_KEY);
    return saved === 'dark' || saved === 'light' ? saved : null;
  }

  private systemTheme(): Theme {
    const matches = typeof window !== 'undefined'
      && window.matchMedia?.('(prefers-color-scheme: light)').matches;
    return matches ? 'light' : 'dark';
  }

  private watchSystemTheme() {
    if (typeof window === 'undefined' || !window.matchMedia) return;
    window.matchMedia('(prefers-color-scheme: light)')
      .addEventListener?.('change', () => {
        // Only track the system while the user has not made an explicit choice.
        if (!this.getUserPreference()) this.theme.set(this.systemTheme());
      });
  }
}
