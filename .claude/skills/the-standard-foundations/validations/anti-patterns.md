# The Standard — Foundation Services — Anti-Patterns

## service-calling-service

**Violates:** ts-foundations-003
**What happens:** A foundation service injects and calls another foundation service (e.g., `StudentService` calls `CourseService`).
**Why it's wrong:** Foundation services must depend only on brokers. Cross-service calls belong in higher-order processing or orchestration services.
**Fix:** Create a processing or orchestration service to coordinate multiple foundation services.

## model-transformation

**Violates:** ts-foundations-004
**What happens:** A foundation service derives or defaults a business field before passing the model to the broker — reading another entity to fill in a foreign key, say.
**Why it's wrong:** Business enrichment is a higher-layer concern. Note the boundary: stamping audit values through the security audit broker is *required* here, not a violation.
**Fix:** Move the business enrichment to a processing service above the foundation.

## cross-entity-logic

**Violates:** ts-foundations-001
**What happens:** `StudentService` also manages `Course` records or checks `Enrollment` constraints.
**Why it's wrong:** Each foundation service must own exactly one entity. Cross-entity rules belong in higher-order services.
**Fix:** Create a dedicated `CourseService` and coordinate in an orchestration service.

## unhandled-broker-exception

**Violates:** ts-foundations-005, ts-foundations-006
**What happens:** A broker call throws a `SqlException` that propagates uncaught to the controller.
**Why it's wrong:** Raw infrastructure exceptions must not cross service boundaries. They reveal implementation details and break the error contract.
**Fix:** Wrap in try-catch and rethrow as a local exception (e.g., `FailedStorageStudentException`).

## ambient-identity

**Violates:** ts-foundations-009
**What happens:** The service calls `ApplyAddAuditValuesAsync(entity)` or `GetUserIdAsync()` — the overloads that take no `SecurityContext`.
**Why it's wrong:** Those read a `ClaimsPrincipal` captured in the broker's **constructor**, which is only correct while the instance is built inside the request it stamps for. On the event path the ambient principal is whoever published the triggering fact, not the actor whose signature was verified — so the audit record names the wrong person. Nothing fails: it compiles, its tests pass, and the divergence only shows up as a wrong `CreatedBy` under a race or on the event path.
**Fix:** Pass the inbound envelope's context explicitly. The ambient overloads have been removed from `ISecurityAuditBroker`, so reaching for one is now a compile error rather than a convention someone has to remember.

## unverified-envelope

**Violates:** ts-foundations-011
**What happens:** An `OnXingAsync` handler reads `envelope.SecurityContext` without first verifying the envelope's integrity signature.
**Why it's wrong:** The `SecurityContext` is only trustworthy because it is signed. Unverified, anyone who can put a message on that address states their own identity and roles — including `Administrators` — and is believed. Verification belongs in the receiver, not the transport, because a handler is reachable without going through the broker.
**Fix:** Call the entity's `Validate{Entity}EventEnvelopeAsync` first, passing the operation this handler serves.

## authorization-error-on-read

**Violates:** ts-foundations-012
**What happens:** A read the caller may not perform throws `Unauthorized{Entity}Exception`.
**Why it's wrong:** An authorization error confirms the row exists. That is an information leak in itself — a caller can enumerate ids and learn which ones are real.
**Fix:** Throw `NotFound{Entity}Exception` and log the true denial reason server-side only.

## divergent-paths

**Violates:** ts-foundations-013
**What happens:** The event handler re-implements the operation instead of calling the same private `DoXAsync` the direct path calls.
**Why it's wrong:** The two implementations drift. A security gate or validation added to one is silently missing from the other, and no test compares them.
**Fix:** Both paths call one `DoXAsync` that owns the gate, auditing, validation, storage, and the published fact.

## missing-dedup

**Violates:** ts-foundations-014
**What happens:** A mutating event handler acts without checking `ProcessedEvents`, or acts without recording the ids afterwards.
**Why it's wrong:** Deliveries are retried. Without the check a replayed request is applied twice; without the dual record the fact this call published can loop back into a request handler and be applied again. Note that `ProcessedEvents` deduplicates on the event id, and a re-entry carries a **fresh** one — so the record must be written for both the inbound and outbound envelope.
**Fix:** Check `AlreadyProcessedAsync` before acting (reply `null` if true), and call `RecordEventProcessedAsync` for both the inbound and outbound envelope.

## swallowed-substrate-failure

**Violates:** ts-foundations-015
**What happens:** An event handler catches an exception and returns `null`, or logs and continues.
**Why it's wrong:** The substrate reads the outcome to decide whether the delivery succeeded. A swallowed failure is recorded as success, so it is never retried and never surfaces.
**Fix:** Categorize into the service's typed exceptions and always rethrow. `null` means "deduplicated", never "failed".
