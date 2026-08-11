# ADR-0018: Agent contract authority and destination client ownership

- Status: Accepted; implementation and contract evidence remain open
- Affected area: OpenAPI, generated Angular client, CI ownership, trigger outcomes

## Context

The independent POS project has a historical Angular workspace and CI workflow.
The final product is the RMS+ Support Hub, while privileged operations belong
to a separate Windows Agent. Keeping two competing frontend contracts or
copying the POS workflow into the Hub would leave ownership ambiguous.

## Decision

The POS Agent owns the authoritative POS OpenAPI contract, typed operation
semantics, security requirements, and the remote-trigger outcome model.
Support Hub Angular owns the final generated/typed consumer and the user
experience. Generated output is derived and is not imported as POS application
source. The standalone POS Angular workspace is reference-only. Support Hub
owns `package.json`, its lockfile, routes, UI primitives, design tokens, and the
final POS feature.

The contract uses this exact trigger-truth boundary:

```text
Proven pre-dispatch input/policy/DNS/SSRF/cancellation rejection
→ NotAttempted

Positive acknowledgement
→ Accepted

Dispatch/connection/transport/timeout/cancellation ambiguity
→ OutcomeUnknown

Any received non-success remote trigger response,
without an authoritative side-effect-free remote contract
→ OutcomeUnknown
```

`Failed` is reserved for a definitive remote rejection only when a future
authoritative contract proves the remote operation was not accepted. No generic
HTTP status supplies that proof, and no automatic retry follows
`OutcomeUnknown`.

## Provenance amendment (INT-03R)

The original INT-01 through INT-03 destination imports remain attributed to
the historical POS provenance snapshot:

```text
25922b499d33bd73f241ffc26c212dd000e81433
```

The post-Opus Agent provenance correction is:

```text
010abc52dc110cfde3dc2c53e057890ff6edaf97
```

The original snapshot's broad `artifacts/` ignore rule omitted
`src/PosAdminTool.Agent/Artifacts/ArtifactCatalog.cs`. INT-03R narrowed that
rule and tracked the existing source. The corrected SHA is the candidate
Agent provenance baseline for INT-04+; prior imports remain historically
attributed to the original SHA. This amendment does not change the accepted
contract-authority or conservative trigger-truth decisions.

The POS `.github/workflows/ci.yml` is recorded as:

```text
REFERENCE ONLY - RESPONSIBILITIES DECOMPOSED INTO DESTINATION WORKFLOWS
```

Future destination CI owns separate lanes for Support Hub portable/backend/
frontend regression; POS Windows build/test/Agent security; OpenAPI and client
generation validation; retained WinUI publish validation; cross-process tests;
and protected representative-device evidence. INT-00 creates no workflow.

## Consequences

- One component owns the source contract and one destination owns its final
  browser consumer.
- Client generation can be validated against the Agent OpenAPI contract in CI
  without importing the standalone POS frontend.
- Trigger ambiguity is surfaced to operators and is never converted into an
  unsafe automatic retry.
- Contract details, operations, and real remote idempotency remain evidence
  gates for INT-01; INT-00 makes no runtime claim.
