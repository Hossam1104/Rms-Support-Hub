# ADR-0007: The phone field carries the local number only

- Status: Accepted
- Affected area: `Normalizers`, `FlatOrderPayloadBuilder`, flat-order client UI

## Context

The payload carries the country code in its own key. Every verified UPC
reference payload pairs `"client_country_code": "966"` with a bare nine-digit
`"client_phone": "556028080"`.

`FlatOrderPayloadBuilder` passed `client_phone` and `order_phone` through
untouched, so an operator who typed or pasted `+966556028080` — or a consumer
lookup that returned the number in international form — sent the country code
twice: once in `client_country_code` and again inside the number.

## Decision

`Normalizers.NormalizeLocalPhone` is the authoritative split, and it runs
inside `FlatOrderPayloadBuilder` for both `client_phone` and `order_phone`.

Only these exact leading forms are stripped, and only at the documented full
length:

| Input                                | Length | Result       |
|--------------------------------------|--------|--------------|
| `00966XXXXXXXXX`                     | 14     | drop `00966` |
| `+966XXXXXXXXX` / `966XXXXXXXXX`     | 12     | drop `966`   |
| `0XXXXXXXXX`                         | 10     | drop `0`     |

Anything else is returned as digits, unchanged. Separators (`+`, spaces,
dashes, parentheses) are dropped because the fixtures store digits only.

The length conditions are what make this safe: a `966` inside a valid local
number (`509661234`) is preserved, and a genuine non-Saudi number
(`14155552671`, `00447911123456`) is never coerced into a Saudi shape.

The country code fields themselves are untouched. `client_country_code` keeps
its existing `"966"` default and `order_country_code` keeps its existing
empty default; that asymmetry predates this ADR.

## Consequences

- What leaves the tool is normalized regardless of how it was entered,
  including drafts saved before this rule existed, because the split happens
  in the builder rather than at the UI boundary.
- `frontend/src/app/core/utils/phone.util.ts` mirrors the same rule table so
  the operator sees the local number while typing. This duplication is
  deliberate: `DraftStore.patch` never assigns the server response back into
  local state (see its D1 race note), so an entry-side pass is the only way
  the field can settle without a full reload. Both sides are covered by the
  same vectors — `NormalizersTests.NormalizeLocalPhone_*` and
  `phone.util.spec.ts`. Do not add a third variant.
