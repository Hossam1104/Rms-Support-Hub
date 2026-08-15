import { resolveSecurePosTarget } from './secure-pos-origin.guard';
import {
  POS_AGENT_ORIGIN,
  POS_SUPPORT_HUB_MAINTENANCE_URL,
  POS_SUPPORT_HUB_ORIGIN
} from '../pos-agent/pos-agent.constants';

describe('secure POS origin handoff', () => {
  it('activates in place when the browser is already on the secure Testing origin', () => {
    expect(resolveSecurePosTarget(POS_SUPPORT_HUB_ORIGIN, '/tools/pos-maintenance')).toBeNull();
    expect(resolveSecurePosTarget(POS_SUPPORT_HUB_ORIGIN, '/tools/pos-maintenance?tab=services')).toBeNull();
  });

  it('hands a direct POS deep link on the dev origin to the same path on the secure origin', () => {
    expect(resolveSecurePosTarget('http://localhost:4200', '/tools/pos-maintenance')).toBe(
      `${POS_SUPPORT_HUB_ORIGIN}/tools/pos-maintenance`
    );
    expect(resolveSecurePosTarget('http://localhost:4200', '/tools/pos-maintenance?tab=services')).toBe(
      `${POS_SUPPORT_HUB_ORIGIN}/tools/pos-maintenance?tab=services`
    );
  });

  it('sends any other wrong-origin entry to the canonical workspace URL', () => {
    for (const origin of ['http://localhost:4200', 'https://localhost:4443', 'http://127.0.0.1:4200']) {
      expect(resolveSecurePosTarget(origin, '/'), origin).toBe(POS_SUPPORT_HUB_MAINTENANCE_URL);
    }
  });

  it('never hands off to a plain-HTTP or Agent origin', () => {
    const target = resolveSecurePosTarget('http://localhost:4200', '/tools/pos-maintenance');

    expect(target?.startsWith('https://')).toBe(true);
    expect(target?.startsWith(POS_AGENT_ORIGIN)).toBe(false);
  });
});

describe('POS origin policy constants', () => {
  // The Agent trusts exactly one browser origin. Widening either constant --
  // or pointing the UI at localhost -- is the change this test exists to block.
  it('pins the exact Agent and Support Hub origins', () => {
    expect(POS_AGENT_ORIGIN).toBe('https://rms-pos-agent.localhost:5001');
    expect(POS_SUPPORT_HUB_ORIGIN).toBe('https://support-hub.integration.test:4443');
    expect(POS_SUPPORT_HUB_MAINTENANCE_URL).toBe('https://support-hub.integration.test:4443/tools/pos-maintenance');
  });

  it('keeps the Agent origin distinct from the browser origin the Agent trusts', () => {
    expect(POS_AGENT_ORIGIN).not.toBe(POS_SUPPORT_HUB_ORIGIN);
    expect(POS_SUPPORT_HUB_ORIGIN).not.toContain('localhost:4200');
  });
});
