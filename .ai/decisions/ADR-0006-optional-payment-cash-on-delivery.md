# ADR-0006: An empty payment list is Cash on Delivery, not a validation error

- Status: Accepted
- Affected area: `FlatOrderValidator`, `FlatOrderPayloadBuilder`, flat-order UI

## Context

`FlatOrderValidator` rejected any payload whose `payment_methods_with_options`
was empty with "Order must contain at least one payment method." and returned
immediately. An operator taking a Cash-on-Delivery order therefore could not
send it at all.

That rule contradicted the verified fixtures. Five UPC reference payloads under
`docs/request_examples/UPC/` are cash-on-delivery orders whose payment list is
entirely commented out, and `FlatOrderPayloadBuilder` already emitted the
matching shape for a payment-free draft: `order_payment_method` from
`GetPaymentMethodString` (which falls back to `"COD"` when no payment exists)
and `order_payment_status` `"not_payment"` from `DeterminePaymentStatus`.
The builder and the validator disagreed, and the validator was wrong.

## Decision

The empty payment list is accepted as the Cash-on-Delivery state.

- The early-return error is removed. A missing or non-list
  `payment_methods_with_options` parses to an empty list, so the per-payment
  loop simply has nothing to iterate.
- No synthetic zero-value payment is fabricated to stand in for one. The
  payload keeps the empty list the reference fixtures show.
- Every other payment rule is unchanged and still applies whenever payments
  are present: allowed methods, per-method status rules, `PostToCredit`
  capability gating and credit-customer fields, single Tamara/Tabby, and the
  full-settlement rules.
- The UI states the outcome instead of reporting a gap: the summary rail and
  compact action bar show "Cash on Delivery", and the empty payments table
  explains it rather than presenting an unmet requirement.

## Consequences

- A Cash-on-Delivery order can be sent. Send is no longer blocked by the
  absence of a payment.
- The full-settlement rules key off `order_payment_status`, so an underpaid
  card order remains `"partially_paid"` and is still a valid, non-error state.
  That behaviour predates this change and is unaffected by it.

## Open risk: `"COD"` versus `"cash"`

The builder emits `order_payment_method` `"COD"`, while the five verified
cash-on-delivery reference payloads carry `"cash"`. `"COD"` appears in the
repository only in `docs/request_examples/invalid_payloads.json`.

This divergence predates this ADR and is systemic rather than COD-specific:
the builder emits the application's canonical method vocabulary
(`"Visa"`, `"Tamara"`, ...) while the fixtures use lowercase (`"visa"`,
`"tamara"`). `"COD"` is also the vocabulary `FlatOrderValidator`
depends on: it is in `AllowedPaymentMethods` and drives the
`method == "COD"` status rule.

It was previously unreachable. Validation blocked payment-free orders, so the
`"COD"` fallback never actually shipped to a client RMS. This ADR makes that
path reachable, which promotes a dormant divergence into a live one.

It is deliberately NOT changed here. Rewriting the emitted method vocabulary
is a payload-contract change affecting every method and every module, well
beyond the approved rule, and there is no evidence in the repository about
whether the receiving RMS matches case-sensitively.

Required follow-up before relying on Cash on Delivery in Production: send one
payment-free order against **UPC Testing** and confirm the RMS accepts
`"COD"`. If it does not, the fix belongs in `GetPaymentMethodString` together
with a decision about the casing of every other method.
