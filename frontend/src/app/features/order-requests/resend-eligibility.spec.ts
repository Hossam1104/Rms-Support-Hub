import { canResend, resendBlockedReason, resendStatusCode } from './resend-eligibility';

describe('Order Requests resend eligibility', () => {
  it('blocks New by canonical code and case-insensitive label', () => {
    expect(canResend(1)).toBe(false);
    expect(canResend('NEW')).toBe(false);
    expect(canResend(' new ')).toBe(false);
  });

  it('blocks With_Delegate without fuzzy matching', () => {
    expect(canResend(4)).toBe(false);
    expect(canResend('With_delegate')).toBe(false);
    expect(canResend(' with_delegate ')).toBe(false);
    expect(canResend('delegated')).toBe(false);
  });

  it('allows every other known backend status and safely blocks unknown values', () => {
    for (const status of [2, 3, 5, 6, 7, 8, 9]) expect(canResend(status)).toBe(true);
    expect(canResend('Confirmed')).toBe(true);
    expect(canResend(null)).toBe(false);
    expect(canResend('')).toBe(false);
    expect(canResend('not-a-status')).toBe(false);
  });

  it('normalizes only whitespace/casing and exposes canonical reasons', () => {
    expect(resendStatusCode(' WITH_DELEGATE ')).toBe(4);
    expect(resendBlockedReason('NEW')).toContain('New');
    expect(resendBlockedReason('With_delegate')).toContain('With_Delegate');
  });
});
