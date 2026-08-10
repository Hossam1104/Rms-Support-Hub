# ADR-0014: Allowed payment methods are a flat-order variant policy

- Status: Accepted
- Affected area: `FlatVariant`, `FlatOrderValidator`, Add Payment dialog, payments table

## Context

`FlatOrderValidator` held one static `AllowedPaymentMethods` list — the twelve
methods from the legacy Flask `config.PAYMENT_METHODS` plus `PostToCredit` —
and applied it to every module.

UPC does not settle through twelve providers. It settles through Visa, Tamara
and Tabby. Everything else (`COD`, `RajhiPoints`, `NeqatyPoints`,
`QitafPoints`, `MisPay`, `Emkan`, `YouGotaGift`, `OgMoney`, `PostToCredit`)
either has no UPC merchant configuration or, in COD's case, is not an explicit
payment row at all. GHC's list is unchanged and must stay unchanged.

The Angular Add Payment dialog already filtered `PostToCredit` out for UPC with
a module-key comparison, but a filtered dropdown is a convenience, not a rule:
a draft saved earlier, or a payload built any other way, still reached the API.

## Decision

The accepted method set is a property of the flat-order variant.

- `FlatVariant` carries `AllowedPaymentMethods`.
  `FlatVariant.GhcVariant` gets `GhcPaymentMethods` (the full legacy list);
  `FlatVariant.UpcVariant` gets `UpcPaymentMethods` = `Visa`, `Tamara`,
  `Tabby`.
- `FlatOrderValidator` checks `variant.AllowedPaymentMethods` and reports
  `Payment method '<method>' is not allowed. The allowed payment methods:
  [<list>]`. The validator is the boundary; the UI is not.
- The rule keys off the variant, not off a module string inside validation, and
  not off an unrelated flag such as `IncludePaymentStatus`.
- UPC cash on delivery is unchanged and stays the zero-payment shape from
  ADR-0006: no payment rows, `order_payment_method` `COD`,
  `order_payment_status` `not_payment`. No synthetic COD row is created, and an
  explicit `COD` row on UPC is now rejected as a malformed payload.
- The frontend mirrors the same policy from one place,
  `features/flat-order/payment-policy.ts`, so the dialog offers exactly the
  methods the active module accepts and opens on the first of them.

## Consequences

- A UPC draft holding a method the policy no longer allows (a `MisPay` row
  saved before this change) is not crashed, hidden, or rewritten. The row keeps
  its identity, and send validation names the method so the operator can remove
  it deliberately.
- Visa, Tamara and Tabby default to `done_payment` on selection because they
  are settled up front. That is a default, not a lock: the row status control
  stays editable, and the payload values `not_payment` / `done_payment` /
  `failed_payment` are unchanged.
- Adding a module later means giving its variant a method list, not editing a
  shared static array.
