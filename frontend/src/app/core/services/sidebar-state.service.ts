import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'order-tool.sidebar-collapsed';

@Injectable({ providedIn: 'root' })
export class SidebarStateService {
  readonly collapsed = signal(this.readPersistedState());

  toggle() { this.setCollapsed(!this.collapsed()); }

  setCollapsed(collapsed: boolean) {
    this.collapsed.set(collapsed);
    try {
      localStorage.setItem(STORAGE_KEY, String(collapsed));
    } catch {
      // Persistence is a convenience; a restricted browser still gets the
      // live signal and a functional sidebar for the current session.
    }
  }

  private readPersistedState(): boolean {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  }
}
