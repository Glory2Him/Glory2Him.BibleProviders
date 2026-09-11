# The Standard — Foundation Services — Rules

## Single Entity

**ts-foundations-001** [ERROR] Foundation services must serve only one entity type.

## Validation

**ts-foundations-002** [ERROR] Foundation services must perform structural and logical validation on all incoming models before passing them to a broker.

## Broker Dependency

**ts-foundations-003** [ERROR] Foundation services must call only brokers — never another service.
**ts-foundations-004** [ERROR] Foundation services must not enrich a model with business data. Stamping audit values through the security audit broker is required, not a violation of this rule; deriving or defaulting a business field from another entity is.

## Exception Handling

**ts-foundations-005** [ERROR] Foundation services must map broker exceptions to local exceptions using dependency validation wrappers.
**ts-foundations-006** [ERROR] Foundation services must wrap all broker calls in try-catch blocks at minimum for storage exceptions and concurrency exceptions.

## Structure

**ts-foundations-007** [ERROR] Foundation services must be partial classes split by concern, not by operation: `{Entity}Service.cs` (the direct path and the shared do-work), `.Substrate.cs` (the event path), `.Validations.cs`, `.Exceptions.cs`. Entities needing more add `.Transitions.cs`, `.Transitions.Validations.cs` or `.Lookup.cs` alongside them.
**ts-foundations-008** [ERROR] Foundation service interfaces must follow the naming pattern `I{Entity}Service`.

## Identity

**ts-foundations-009** [ERROR] Foundation services must take the acting identity from the inbound event envelope's `SecurityContext`, never from an ambient accessor. Every audit and user-id call must pass that context explicitly — `ApplyAddAuditValuesAsync(entity, securityContext)`, `GetUserIdAsync(securityContext)`.

An ambient principal is captured when the broker instance is constructed. That is correct only while the instance is built inside the request it stamps for — never true on the event path, where the ambient principal is whoever published the triggering fact, not the actor the signature verified. Identity travels as signed envelope data.

**ts-foundations-010** [ERROR] Foundation services must enforce their own security (design §14.6). An exposer may bind to the foundation directly, so no layer may assume an upstream layer already gated the caller.

**ts-foundations-011** [ERROR] Event-path handlers must verify the envelope's integrity signature in the receiver, against the event name that handler serves and the request direction (design §14.6 rule 4). Without it, a caller who can put a message on the address states their own identity and roles and is believed.

**ts-foundations-012** [ERROR] A read a caller may not perform must answer not-found, never unauthorized — an authorization error confirms the row exists. Log the true denial reason server-side only.

## Dual Path

**ts-foundations-013** [ERROR] Every operation that has a request address must be reachable both directly and through the event substrate, and both paths must converge on one private `DoXAsync` method that owns the security gate, auditing, validation, storage, and the published fact. For such an operation the public method mints the envelope and hands off; it holds no logic of its own.

The addressed set is `Adding`, `Modifying`, `RemovingById`, `HardRemovingById`, `RetrievingById`. `RetrieveAll{Entity}sAsync` is deliberately **not** among them — it has no request address, no handler and no `DoXAsync`; it mints an envelope only to capture the caller for the visibility filter, and does its work inline. Do not invent an `OnRetrievingAll` handler. Entity-specific operations (a transition, a lookup) follow whichever of the two shapes their own address decision implies.

**ts-foundations-014** [ERROR] Mutating event handlers must check `ProcessedEvents` before acting and record BOTH the inbound request id and the outbound fact id after. A deduplicated delivery replies `null`. Read-only handlers are naturally idempotent and skip this bookkeeping.

**ts-foundations-015** [ERROR] Event-path handlers must always rethrow after categorizing, so the substrate records the delivery as `Error` and drives retries. Failures are never swallowed. Exceptions already categorized by a nested service call pass through unwrapped.
