# ADR-0002: Verified External Contracts Are Authoritative

- **Status:** Accepted
- **Date:** 2026-07-25
- **Affected area:** Payload builders, validators, SQL repositories, contract tests

## Context

An earlier rewrite introduced guessed JSON keys and SQL columns. Green tests did not detect the drift because they asserted against the same assumptions.

## Decision

Treat reference payload JSON and its mirrored test fixtures as the payload contract. Treat current repository SQL plus the verified database-schema document as the database contract. Add or change fields only with evidence; never infer a contract from a similar module.

## Consequences

- Contract tests compare built payloads key-for-key.
- Dapper SQL remains explicit and reviewable.
- Live schema drift and fixture provenance still require external verification.
- Unknown GHC fields remain disabled or marked unverified instead of guessed.

## Revisit When

- An upstream owner supplies a versioned machine-readable schema or API contract that supersedes the current evidence.

## Evidence

- `docs/request_examples/`
- `backend/tests/OnlineOrderTool.Tests/fixtures/`
- `backend/tests/OnlineOrderTool.Tests/ContractTests.cs`
- `backend/src/OnlineOrderTool.Data/Repositories/`
- `docs/database-schema.md`
