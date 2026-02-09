# Request Examples & Validation Rules

This directory contains the source of truth for API payloads.

## Files
- `valid_full_order.json`: A strictly valid, canonical order payload (COD example). Use this as the template.
- `invalid_payloads.json`: Examples of payloads that MUST be rejected by the backend/UI validation.
- `edge_cases.json`: Complex scenarios like split payments.

## Key Business Rules
1. **Canonical COD**: "cash" is NOT allowed. Use "COD" for both `payment_method` and `payment_option`.
2. **Strict Mapping**: Each `payment_method` has a strict set of allowed `payment_options`.
3. **Credit Logic**: `PostToCredit` must cover 100% of the order total.
4. **Status Logic**:
    - `COD` -> `not_payment`
    - `PostToCredit` -> `not_payment`
    - `Visa`, `Tamara`, `Tabby`, etc. -> `done_payment`
5. **Amount Logic**: Sum of `payment_amount` in `payment_methods_with_options` must equal `order_final_total_value` (except for specific partial payment flows, but generally true for completed orders).
