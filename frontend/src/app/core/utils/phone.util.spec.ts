import { normalizeLocalPhone } from './phone.util';

/**
 * These vectors deliberately mirror `NormalizersTests.NormalizeLocalPhone_*`
 * on the backend. The backend copy is the authoritative boundary; if either
 * side gains a case, add it here too rather than letting the two drift.
 */
describe('normalizeLocalPhone', () => {
  it('strips a leading Saudi country code in every written form', () => {
    expect(normalizeLocalPhone('+966556028080')).toBe('556028080');
    expect(normalizeLocalPhone('966556028080')).toBe('556028080');
    expect(normalizeLocalPhone('00966556028080')).toBe('556028080');
    expect(normalizeLocalPhone('0556028080')).toBe('556028080');
    expect(normalizeLocalPhone('+966 55 602 8080')).toBe('556028080');
    expect(normalizeLocalPhone('(966) 55-602-8080')).toBe('556028080');
  });

  it('leaves an already-local number untouched', () => {
    expect(normalizeLocalPhone('556028080')).toBe('556028080');
  });

  it('never removes a 966 that is part of the subscriber number', () => {
    expect(normalizeLocalPhone('509661234')).toBe('509661234');
    expect(normalizeLocalPhone('966966028080')).toBe('966028080');
  });

  it('does not coerce non-Saudi numbers', () => {
    expect(normalizeLocalPhone('14155552671')).toBe('14155552671');
    expect(normalizeLocalPhone('00447911123456')).toBe('00447911123456');
  });

  it('returns an empty string for empty or nullish input', () => {
    expect(normalizeLocalPhone('')).toBe('');
    expect(normalizeLocalPhone(null)).toBe('');
    expect(normalizeLocalPhone(undefined)).toBe('');
  });
});
