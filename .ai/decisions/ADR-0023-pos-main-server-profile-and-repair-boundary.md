# ADR-0023: Agent-owned Main Server profiles and local repair boundary

- Status: Accepted for remaining implementation
- Affected area: POS identity discovery, Main Server API integration,
  installation repair, diagnostic execution, privileged mutations

## Context

The representative UPC POS has a complete local RMS+ payload and installer
surface. Its payload apphosts match the installed Branch, Cashier, Cashier UI,
and Service Manager apphosts. The installed Branch and Cashier configurations
agree on the actual RMS Main Server base address, while the owner-provided UPC
Swagger uses a different host.

The discovered Main Server OpenAPI exposes Branch/POS `PUT` routes named as
install/uninstall operations, but they carry only Branch Code/POS Number query
parameters and return an empty status DTO. It documents no package body,
operation identifier, progress resource, cancellation, idempotency, or
rollback. Some POS GETs are anonymously reachable despite a global Swagger
Bearer declaration. The Branch install inventory schema also contains a
credential-bearing property.

## Decision

Main Server integration is an Agent-owned typed boundary. The browser can
request only logical allow-listed operations. It never supplies a URL,
credential, authorization header, Branch Code, POS Number, arbitrary route, or
request body. The Agent binds Branch/POS targets to sanitized local RMS
discovery and resolves one provisioned client API profile from server-owned
configuration. Installed Branch/Cashier endpoints are the runtime source of
truth; `Settings:TheClient` is display/cross-validation evidence, not a URL.

Every Main Server route is individually classified for method, side effects,
response sensitivity, authorization, timeout, and outcome truth. Global
Swagger security is advisory only. Credential-bearing install inventories are
not transported, bundled, cached, or logged. A generic Main Server proxy is
prohibited.

Actual device file/service repair uses an exact local, Agent-owned package
adapter after package identity, root, ownership, and rollback verification.
Until an authorized runtime test proves broader semantics, the discovered
Branch/POS install/uninstall `PUT`s are treated only as installation-state
acknowledgements after local verification, not remote package executors.

Local package execution, Safe Diagnostic Console Run, and Main Server
mutations are distinct typed operations. They require existing exact-Origin,
Negotiate, local-Administrators authorization, one-use target/path/method-bound
tokens, exact confirmation, bounded concurrency/idempotency, audit, and
explicit outcome-unknown handling. Main Server POST/PUT/PATCH/DELETE or any
state-changing GET additionally requires explicit owner approval before each
live invocation.

## Consequences

- UPC and Whites are separate allow-listed profiles; no IP or URL is inferred
  by editing the installed endpoint or client name.
- Slice A can implement evidence/health/UI without external credentials or
  process launch. Slice B consumes those typed models for repair/install.
- Main Server responses are projected into safe local DTOs; remote schemas do
  not cross directly to Angular.
- Local package payload validation and rollback evidence are required before
  a repair executor can be enabled.
- Whites equivalence, remote mutation semantics, error envelopes, and installer
  switches remain explicit evidence gates rather than guessed contracts.
