# The Standard — Foundation Services — Checklist

## Structure

- [ ] Service serves only one entity type (ts-foundations-001)
- [ ] Service is a partial class split by concern: `.cs`, `.Substrate.cs`, `.Validations.cs`, `.Exceptions.cs` (ts-foundations-007)
- [ ] Interface follows `I{Entity}Service`, with the event-facing half in `I{Entity}Service.Substrate.cs` (ts-foundations-008)
- [ ] Service depends only on brokers — never another service (ts-foundations-003)
- [ ] No business enrichment before the broker call; audit stamping is the allowed exception (ts-foundations-004)
- [ ] No business logic combining multiple entities exists in this service (ts-foundations-001)

## Validation

- [ ] All incoming models are structurally validated — null, empty strings, default Guid/DateTimeOffset (ts-foundations-002)
- [ ] String length caps are enforced in the service, not left to the column to reject (ts-foundations-002)
- [ ] `CreatedBy` / `UpdatedBy` are checked against the acting id resolved from the envelope's context (ts-foundations-002, ts-foundations-009)
- [ ] `CreatedBy` / `CreatedWhen` are checked unchanged against storage on modify (ts-foundations-002)

## Identity

- [ ] **Every** audit call passes a `SecurityContext` — no ambient overloads anywhere (ts-foundations-009)
- [ ] The context passed always comes off the **inbound envelope**, not a local or a field (ts-foundations-009)
- [ ] Every write path runs a security gate inside `DoXAsync`, not in the public wrapper (ts-foundations-010)
- [ ] The gate order is: authenticated → global ban → the permission itself (ts-foundations-010)
- [ ] Every `OnXingAsync` handler verifies the envelope signature before reading its context (ts-foundations-011)
- [ ] Denied reads answer not-found, never unauthorized; the real reason is logged server-side (ts-foundations-012)

## Dual path

- [ ] Every **addressed** operation — Adding, Modifying, RemovingById, HardRemovingById, RetrievingById — is reachable both directly and via the substrate (ts-foundations-013)
- [ ] For those, both paths converge on one private `DoXAsync` and the public method holds no logic (ts-foundations-013)
- [ ] `RetrieveAll{Entity}sAsync` has NOT been given a handler or an address — it is deliberately direct-only, and mints an envelope purely to capture the caller for the visibility filter (ts-foundations-013)
- [ ] Mutating handlers check `ProcessedEvents` before acting and reply `null` on a duplicate (ts-foundations-014)
- [ ] `DoXAsync` records **both** the inbound request id and the outbound fact id (ts-foundations-014)
- [ ] Read-only handlers skip the dedup bookkeeping (naturally idempotent) (ts-foundations-014)
- [ ] Each request address is wired in `EventSubscriptionRegistration` — the service only exposes the capability

## Exceptions

- [ ] Broker exceptions are caught and wrapped in local exception types (ts-foundations-005)
- [ ] All broker calls are wrapped in try-catch (ts-foundations-006)
- [ ] `TryCatchSubstrate` mirrors `TryCatch`'s taxonomy, plus the envelope guard (ts-foundations-015)
- [ ] Substrate failures always rethrow — never swallowed, never returned as `null` (ts-foundations-015)
- [ ] Already-categorized exceptions from nested calls pass through unwrapped (ts-foundations-015)
- [ ] `DuplicateKeyWithUniqueIndexException` has its own catch — it does NOT derive from `DuplicateKeyException`

## Tests

- [ ] A tampered envelope (`VerifyAsync` → false) is proven refused, per handler
- [ ] Each unauthorized caller shape is proven refused, driven from shared theory data
- [ ] A duplicate request is proven to reply `null` and write nothing
- [ ] Filler pins `IsDeleted` rather than drawing it — posture tests must not depend on the draw
- [ ] Audit mocks use `It.IsAny<SecurityContext>()`, matching the only overload that exists

> Unit tests mock the layer below, so tightening a foundation validation will not fail any
> caller's suite. When a rule changes, check the callers by hand or add acceptance coverage —
> a green suite does not prove them.
