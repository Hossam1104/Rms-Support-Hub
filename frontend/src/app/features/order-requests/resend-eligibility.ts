/**
 * Canonical resend rule shared by list/detail/dialog/final-submit UI guards.
 * The backend stores RequestOrderHeaders.OrderStatus as an int; string labels
 * are accepted only for defensive display/test paths and are matched exactly
 * after trimming and case normalization. No substring matching is allowed.
 */
export const RESEND_BLOCKED_STATUS_CODES = new Set([1, 4]);

const STATUS_LABEL_CODES = new Map<string, number>([
  ['new', 1],
  ['confirmed', 2],
  ['ready', 3],
  ['with_delegate', 4],
  ['with delegate', 4],
  ['rejected', 5],
  ['canceledbyclient', 6],
  ['canceled by client', 6],
  ['canceledbyadmin', 7],
  ['canceled by admin', 7],
  ['processing', 8],
  ['done', 9]
]);

export function resendStatusCode(status: number | string | null | undefined): number | null {
  if (typeof status === 'number') {
    return Number.isInteger(status) && status >= 1 && status <= 9 ? status : null;
  }

  if (typeof status !== 'string') return null;
  const normalized = status.trim().toLowerCase();
  if (!normalized) return null;

  const numeric = Number(normalized);
  if (Number.isInteger(numeric) && numeric >= 1 && numeric <= 9) return numeric;
  return STATUS_LABEL_CODES.get(normalized) ?? null;
}

export function canResend(status: number | string | null | undefined): boolean {
  const code = resendStatusCode(status);
  return code !== null && !RESEND_BLOCKED_STATUS_CODES.has(code);
}

export function resendBlockedReason(status: number | string | null | undefined): string {
  const code = resendStatusCode(status);
  if (code === 1) return 'Resend is unavailable for New orders.';
  if (code === 4) return 'Resend is unavailable for With_Delegate orders.';
  return 'Resend is unavailable because the order status is unknown.';
}
