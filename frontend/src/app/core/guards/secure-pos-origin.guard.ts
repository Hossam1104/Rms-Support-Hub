import { CanActivateFn } from '@angular/router';
import { POS_SUPPORT_HUB_MAINTENANCE_URL, POS_SUPPORT_HUB_ORIGIN } from '../pos-agent/pos-agent.constants';

/**
 * POS is a privileged workspace. A local HTTP hub must hand the browser to the exact secure
 * Testing origin instead of trying to widen the Agent CORS allow-list or running a second UI.
 */
export const securePosOriginGuard: CanActivateFn = (_route, state) => {
  if (typeof window === 'undefined') return true;

  const target = resolveSecurePosTarget(window.location.origin, state.url);
  if (target === null) return true;

  window.location.replace(target);
  return false;
};

/**
 * Decides the handoff without touching `window`, so the routing contract stays
 * unit-testable. `null` means the browser is already on the secure origin and
 * the POS workspace may activate in place.
 */
export function resolveSecurePosTarget(currentOrigin: string, url: string): string | null {
  if (currentOrigin === POS_SUPPORT_HUB_ORIGIN) return null;

  // A direct deep link keeps its path so the operator lands where they aimed;
  // anything else falls back to the canonical workspace entry point.
  return url.startsWith('/tools/pos-maintenance')
    ? `${POS_SUPPORT_HUB_ORIGIN}${url}`
    : POS_SUPPORT_HUB_MAINTENANCE_URL;
}
