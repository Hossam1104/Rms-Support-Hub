/**
 * U4 (UI_Rework_Plan.md §U4): the single place send-request validation
 * errors (`{ success: false, errors: string[] }`, see docs/api-spec.md) are
 * normalized and mapped to inline form targets. The messages come from
 * FlatOrderValidator via module.Validate(draft) -- this mapper owns no
 * validation rules of its own, it only routes confirmed server messages.
 *
 * Two confirmed message shapes exist:
 *  - "Missing required field: <field_key>" -- the server already names the
 *    draft field, so the identifier is used directly.
 *  - Product/payment section messages (see FlatOrderValidator) -- routed to
 *    the products/payments section via the explicit table below.
 * Anything else stays global and is shown in the summary area only.
 */

export interface MappedValidationErrors {
  /** Inline messages keyed by draft field name or section key
   * ('products' | 'payments'). */
  fieldErrors: Record<string, string[]>;
  /** Errors with no confirmed field/section home -- summary area only. */
  globalErrors: string[];
  /** Total server-reported issue count (mapped + global). */
  totalCount: number;
  /** DOM id of the first invalid target in page order, for focus/scroll. */
  firstTargetId: string | null;
}

/** Draft field key -> DOM id of the control (or section card) to focus.
 * Only fields FlatOrderValidator can actually report are listed. */
const FIELD_TARGET_IDS: Record<string, string> = {
  branch_code: 'field-branch-code',
  order_code: 'field-order-code',
  client_phone: 'field-client-phone',
  client_first_name: 'field-client-first-name',
  order_address: 'field-order-address',
  products: 'products-card',
  payments: 'payments-card'
};

/** Page (DOM) order of the targets -- decides which invalid field is
 * focused/scrolled to first. */
const TARGET_ORDER = [
  'branch_code',
  'order_code',
  'client_phone',
  'client_first_name',
  'order_address',
  'products',
  'payments'
];

const MISSING_REQUIRED = /^Missing required field: ([a-z_]+)$/;

/** Explicit routing for the confirmed FlatOrderValidator section messages.
 * First match wins; anything unmatched stays global. */
const SECTION_RULES: ReadonlyArray<readonly [RegExp, string]> = [
  [/^Order must contain at least one product\./, 'products'],
  [/payment|PostToCredit|Digital wallet/i, 'payments']
];

export function mapSendValidationErrors(errors: string[]): MappedValidationErrors {
  const fieldErrors: Record<string, string[]> = {};
  const globalErrors: string[] = [];

  const push = (key: string, message: string) => {
    (fieldErrors[key] ??= []).push(message);
  };

  for (const message of errors) {
    const missing = MISSING_REQUIRED.exec(message);
    if (missing && missing[1] in FIELD_TARGET_IDS) {
      push(missing[1], message);
      continue;
    }

    const section = SECTION_RULES.find(([pattern]) => pattern.test(message));
    if (section) {
      push(section[1], message);
      continue;
    }

    globalErrors.push(message);
  }

  const firstKey = TARGET_ORDER.find(key => (fieldErrors[key]?.length ?? 0) > 0) ?? null;

  return {
    fieldErrors,
    globalErrors,
    totalCount: errors.length,
    firstTargetId: firstKey ? FIELD_TARGET_IDS[firstKey] : null
  };
}
