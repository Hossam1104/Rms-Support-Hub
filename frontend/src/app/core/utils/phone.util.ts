/**
 * Saudi country code is carried by its own draft field
 * (`client_country_code`, default "966"), so the number field must hold only
 * the local subscriber number -- every reference payload under
 * docs/request_examples/UPC/** pairs `"client_country_code": "966"` with a
 * bare 9-digit `"client_phone": "556028080"`.
 *
 * The authoritative boundary for that split is the BACKEND:
 * `Normalizers.NormalizeLocalPhone` runs inside `FlatOrderPayloadBuilder`, so
 * what actually leaves the tool is normalized regardless of this file (and so
 * are drafts saved before the rule existed). This helper exists only so the
 * operator SEES the local number while typing -- `DraftStore.patch`
 * deliberately never assigns the server response back into local state (see
 * its D1 race note), so an entry-side pass is the only way the field can
 * settle without a full reload. Both sides implement the identical rule table
 * below and are covered by the same vectors; do not add a third variant.
 *
 *   "+966XXXXXXXXX" / "966XXXXXXXXX" (12 digits) -> drop "966"
 *   "00966XXXXXXXXX"                 (14 digits) -> drop "00966"
 *   "0XXXXXXXXX"                     (10 digits) -> drop "0"
 *
 * Anything else is returned as digits, unchanged: a "966" inside a valid
 * local number (e.g. "509661234") is preserved, and a genuine non-Saudi
 * number is never coerced into a Saudi shape.
 */
export function normalizeLocalPhone(phone: unknown): string {
  const digits = String(phone ?? '').replace(/\D/g, '');

  if (digits.length === 14 && digits.startsWith('00966')) return digits.slice(5);
  if (digits.length === 12 && digits.startsWith('966')) return digits.slice(3);
  if (digits.length === 10 && digits.startsWith('0')) return digits.slice(1);

  return digits;
}
