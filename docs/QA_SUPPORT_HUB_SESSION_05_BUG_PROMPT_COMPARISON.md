# Session 05 Bug Prompt Quality Comparison

## Legacy Bug Input

- Title: Discount calculation mismatch when applying multiple promotions sequentially
- Preconditions: Cashier is logged in and the cart contains multiple items.
- Steps: Apply two promotions and review the transaction summary.
- Expected result: Discounts, tax, and total are calculated correctly.
- Actual result: One item receives a double deduction and shows a negative line price.
- Attachments: `checkout_calculation_error.png`, `checkout_calculation_debug.log`

## Legacy Prompt Characteristics

- Accepted six fields: title, preconditions, steps, expected result, actual result, and attachments.
- Produced one fixed six-section Markdown template.
- Marked empty values with insertion placeholders, but did not distinguish confirmed facts from suggestions.
- Did not model environment, build, test data, impact, severity, priority, diagnostics, or target output format.
- Used standalone localStorage keys, inline DOM handlers, a global prompt variable, and a CDN Three.js runtime.

## Enhanced Prompt Characteristics

- Accepts the expanded typed Bug input and preserves every supplied value and evidence reference.
- Explicitly separates CONFIRMED FACT, INFERRED / SUGGESTED, and MISSING INFORMATION.
- Uses `[NEEDS INVESTIGATION]` for unsupported missing values and `[Suggested]` for optional severity or priority recommendations.
- Supports Concise, Standard, and Deep detail levels through shared deterministic sections.
- Supports Generic Markdown, Jira, and Azure DevOps paste-ready formats without API coupling or duplicated templates.
- Requires atomic, observable reproduction steps; preserves ambiguous wording; checks expected-versus-actual contradictions; and keeps possible causes unconfirmed.
- Provides configurable Missing Information, Fix Acceptance Criteria, Retest Checklist, and Regression Scope sections.
- Runs inside Angular with namespaced draft persistence, shared clipboard/export behavior, global theme and motion services, and no external network or AI dependency.
