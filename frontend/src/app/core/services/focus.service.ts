import { Injectable } from '@angular/core';

/**
 * U4: testable abstraction over the DOM focus/scroll performed when a send
 * validation failure maps to inline fields. Components inject this instead
 * of touching `document` directly, so specs can spy on the request itself.
 */
@Injectable({ providedIn: 'root' })
export class FocusService {
  /** Scrolls the element (or its first focusable control) into view and
   * focuses it. Returns false when no element with the id exists. */
  scrollToAndFocus(elementId: string): boolean {
    const el = document.getElementById(elementId);
    if (!el) return false;

    el.scrollIntoView({ behavior: 'smooth', block: 'center' });

    const target = this.isFocusable(el)
      ? el
      : el.querySelector<HTMLElement>('input, select, textarea, button, [tabindex]');
    target?.focus({ preventScroll: true });
    return true;
  }

  private isFocusable(el: HTMLElement): boolean {
    return ['INPUT', 'SELECT', 'TEXTAREA', 'BUTTON'].includes(el.tagName);
  }
}
