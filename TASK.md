# SESSION 06 — Online Orders Dense UI, Branding, Tables & Commerce Assets

## Goal

Implement the screenshot-driven Online Order UI improvements while preserving every business contract.

## Branch

```text
refactor/rms-hub-06-online-orders-ui
```

## Hard Boundary

Do NOT change:
- APIs
- DTOs
- payload mapping
- module keys
- capability guard
- filters/paging business rules
- payment codes
- status logic
- Send/Resend/Cancel behavior

UI presentation only.

### 1. Module identity

Use:
- RMS+ global brand
- UPC logo for UPC E-Commerce
- GHC/Whites only where actual context supports it

Improve module card and sidebar/header identity.

### 2. Compact Order Builder

Reduce:
- page header height
- workflow height
- inter-section gap
- accordion/header height
- field row gap
- panel padding

Keep it readable.

### 3. Order Header

Compact the existing grid without changing fields:
- Branch
- Order code
- Parent order code
- Delivery cost
- Order status
- Notes
- coordinates

### 4. Products table

Add:
- outer border
- safe inset from parent
- header separation
- row separators
- compact controls
- aligned price/qty/discount/VAT/total
- aligned delete action
- no page-level horizontal overflow

### 5. Items summary

Add:
- outer border
- row separators
- clear totals/footer row
- compact cell padding
- numeric alignment
- safe card margins

### 6. Order Requests

Apply the same shared table contract while preserving all existing functionality.

### 7. Riyal

Use the canonical Saudi Riyal asset/presentation everywhere monetary values use the Riyal.

Do not regress the existing verified Riyal check.

### 8. Payment assets

Map visual logos only to existing methods:
- Visa
- Mastercard
- Mada
- Tabby
- Tamara

Underlying values/codes/payloads remain unchanged.

Unknown method = generic fallback; never guessed logo.

### 9. Offer and loading

Use `offer_logo.png` only for existing discount/offer concepts.

Use loader only if it improves a real current loading state.

### 10. Browser safety

If browser available, use the configured local backend/proxy.

Read-only validation only.

Never click:
- Send
- Resend
- Cancel
- Submit
- destructive order actions

Widths:
- 1440
- 1024
- 900
- 768
- 390

Validate:
- reduced scrolling
- table borders
- table/card margins
- responsive behavior
- summary readability
- logos/payment assets
- no distortion
- console clean

## Full Validation

```text
npm --prefix frontend test -- --watch=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
npm --prefix frontend run build -- --configuration production
git diff --check
```

## Commit

```text
refactor(online-orders): compact workflows and brand commerce UI
```

---
