import { CanActivateFn } from '@angular/router';
import { POS_SUPPORT_HUB_MAINTENANCE_URL, POS_SUPPORT_HUB_ORIGIN } from '../pos-agent/pos-agent.constants';

/**
 * POS is a privileged workspace. A local HTTP hub must hand the browser to the exact secure
 * Testing origin instead of trying to widen the Agent CORS allow-list or running a second UI.
 */
export const securePosOriginGuard: CanActivateFn = (_route, state) => {
  if (typeof window === 'undefined' || window.location.origin === POS_SUPPORT_HUB_ORIGIN) return true;

  const target = state.url.startsWith('/tools/pos-maintenance')
    ? `${POS_SUPPORT_HUB_ORIGIN}${state.url}`
    : POS_SUPPORT_HUB_MAINTENANCE_URL;
  window.location.replace(target);
  return false;
};
