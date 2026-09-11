---
name: the-standard-foundations
version: 0.2.0
standard-version: v2.50.0
applies-to: ["*Service*.cs"]
depends-on: ["the-standard-core", "the-standard-brokers"]
---

# The Standard — Foundation Services

## 0/ Context (Purpose — WHY this skill exists)

0.0/ Where: The foundation (first-level) service layer of any Standard-compliant system.
0.1/ Who: Engineers implementing or reviewing foundation services and their tests.
0.2/ What: Enforces structural validation, single-entity responsibility, correct broker dependency rules, envelope-carried identity, and the dual-path (direct + event substrate) shape.
0.3/ Applies to: *Service*.cs
0.4/ Version: v2.50.0
0.5/ Depends on: the-standard-core, the-standard-brokers

## 1/ Actual (Dependency — WHAT the rules are and what they depend on)

1.0/ Dos:
  1. Foundation services must serve only one entity type (e.g., StudentService for Student) → see rules/rules.md#ts-foundations-001
  2. Foundation services must perform structural and logical validation on all incoming models → see rules/rules.md#ts-foundations-002
  3. Foundation services must call only brokers — never another service → see rules/rules.md#ts-foundations-003
  4. Foundation services must not enrich a model with business data; audit stamping is required, not a violation → see rules/rules.md#ts-foundations-004
  5. Foundation services must map broker exceptions to local exceptions using dependency validation → see rules/rules.md#ts-foundations-005
  6. Foundation services must implement try-catch wrapping for broker calls → see rules/rules.md#ts-foundations-006
  7. Foundation services must be partial classes, split by concern → see rules/rules.md#ts-foundations-007
  8. Foundation service interfaces must follow the naming pattern `I{Entity}Service` → see rules/rules.md#ts-foundations-008
  9. Identity must come from the inbound envelope's `SecurityContext`, passed explicitly to every audit call → see rules/rules.md#ts-foundations-009
  10. The foundation must enforce its own security — no layer may assume an upstream layer gated the caller → see rules/rules.md#ts-foundations-010
  11. Event handlers must verify the envelope's integrity signature in the receiver → see rules/rules.md#ts-foundations-011
  12. A denied read must answer not-found, never unauthorized → see rules/rules.md#ts-foundations-012
  13. Both entry paths must converge on one private `DoXAsync` → see rules/rules.md#ts-foundations-013
  14. Mutating handlers must dedup on `ProcessedEvents` and record both event ids → see rules/rules.md#ts-foundations-014
  15. Event handlers must always rethrow after categorizing → see rules/rules.md#ts-foundations-015

1.1/ Don'ts:
  1. Must not call another foundation service — only brokers → see validations/anti-patterns.md#service-calling-service
  2. Must not enrich the model with business data before the broker call → see validations/anti-patterns.md#model-transformation
  3. Must not contain business rules combining multiple entities → see validations/anti-patterns.md#cross-entity-logic
  4. Must not suppress exceptions from brokers without wrapping → see validations/anti-patterns.md#unhandled-broker-exception
  5. Must not use the ambient audit overloads → see validations/anti-patterns.md#ambient-identity
  6. Must not read an envelope's `SecurityContext` before verifying its signature → see validations/anti-patterns.md#unverified-envelope
  7. Must not throw an authorization error from a read → see validations/anti-patterns.md#authorization-error-on-read
  8. Must not re-implement the operation in the event handler → see validations/anti-patterns.md#divergent-paths
  9. Must not skip the `ProcessedEvents` check or the dual record → see validations/anti-patterns.md#missing-dedup
  10. Must not swallow a substrate failure → see validations/anti-patterns.md#swallowed-substrate-failure

1.2/ Ask:
  - Ask when a foundation service appears to need data from more than one entity.
  - Ask when validation logic resembles a business rule rather than a structural check.
  - Ask what this entity's security posture actually is — who may write, who may read, and what a caller who may not see a row is told. The template ships a placeholder; it must be replaced with a real, stated posture, not left as-is.

1.3/ Defaults:
  - When a service supports one entity, name it `{Entity}Service` and its interface `I{Entity}Service`.
  - Inner exceptions are public and carry no "Service" in the name (`NullStudentException`); only the four outer wrappers and `Failed{Entity}ServiceException` do.
  - Exception mapping: `DbUpdateConcurrencyException` → `Locked{Entity}Exception`; `SqlException`/`DbUpdateException` → `FailedStorage{Entity}Exception`; `DuplicateKeyException` **and** `DuplicateKeyWithUniqueIndexException` → `AlreadyExists{Entity}Exception`.
  - Structural validation checks: null, empty strings, default Guid, default DateTimeOffset, length caps.
  - Audit fields are `CreatedBy` / `CreatedWhen` / `UpdatedBy` / `UpdatedWhen` (see `IAudit`), not `CreatedDate`.

1.4/ Examples:
  - ✅ see examples/good/example_good_foundation_service.cs
  - ❌ see examples/bad/example_bad_foundation_service.cs

1.5/ Templates:
  - Scaffold a new foundation service: see templates/Template_FoundationService.cs — it carries the full ten-section shape (both interfaces, the event operation enum, the address identifiers, all four service partials, the fifteen exception models, and the test fixture) plus the placeholder conventions.

1.6/ Checklists:
  - Pre-review checklist: see validations/checklist.md

1.7/ Contracts:
  - Naming, validation, exception mapping and the audit-broker contract: see contracts/contracts.json

1.8/ Outside this skill:
  - Wire each request address to its handler in `Registrations/EventSubscriptionRegistration.cs`. The service exposes the capability; the central registration decides what is connected.
  - Add the entity's `IEventBroker.{Entity}.cs` publish/subscribe pair and its storage broker methods.

## 2/ Expected (Exposure — WHAT comes out)

2.0/ Format: C# source code.
2.1/ Outcome: Foundation services that validate structurally, depend only on brokers, wrap exceptions, serve exactly one entity type, enforce their own security, and carry identity as signed envelope data across both the direct and event-substrate paths.
2.2/ Tone: Direct. Cite rule IDs. Violations must be fixed, not suggested.
