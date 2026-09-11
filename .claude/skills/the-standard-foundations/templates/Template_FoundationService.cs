// ---
// skill: the-standard-foundations
// type: template
// source-section: "2.1 Foundation Services"
// ---

// ═══════════════════════════════════════════════════════════════════════════════
// HOW TO USE THIS TEMPLATE
// ═══════════════════════════════════════════════════════════════════════════════
//
// PLACEHOLDERS
//   {Entity}          PascalCase type name .................. ApprovalSetting
//   {entity}          camelCase variable name ............... approvalSetting
//   {Entity Display}  sentence-case display name ............ Approval setting
//   {entity display}  lowercase display name ................ approval setting
//   {Namespace}       root namespace ........................ Glory2Him.Core
//
//   Inside an interpolated string, {{entity}Id} renders as {approvalSettingId} —
//   the outer braces are C# interpolation, the inner pair is the placeholder.
//
// ─────────────────────────────────────────────────────────────────────────────
// THE ARCHITECTURE THIS TEMPLATE SCAFFOLDS
// ─────────────────────────────────────────────────────────────────────────────
//
// Every ADDRESSED operation — Adding, Modifying, RemovingById, HardRemovingById,
// RetrievingById — is reachable two ways, and both converge on ONE private method.
// RetrieveAll is the deliberate exception: no address, no handler, no DoXAsync (see
// the note on it in Section 5).
//
//   non-event path   {Entity}Service.cs
//                    public AddXAsync(entity)
//                      → eventEnvelopeBroker.CreateAsync(content)   [mints envelope
//                        from the ambient caller]
//                      → DoAddXAsync(entity, inboundEnvelope, ct)
//
//   event path       {Entity}Service.Substrate.cs
//                    public OnAddingXAsync(envelope)
//                      → verify the envelope's integrity signature
//                      → ProcessedEvents dedup check
//                      → DoAddXAsync(envelope.Content, envelope, ct)
//                      → CreateNextAsync(...) as the delivery's reply
//
// The private DoXAsync owns: the security gate, auditing, validation, storage,
// the ProcessedEvents dual-record, and publishing the past-tense fact. Because
// both paths run the same method, they cannot diverge.
//
// IDENTITY TRAVELS AS SIGNED ENVELOPE DATA, NOT AS AN AMBIENT PRINCIPAL.
// This is the rule the whole shape exists to serve. Every audit and validation
// call takes the SecurityContext off the inbound envelope:
//
//     ApplyAddAuditValuesAsync(entity, securityContext)   ← ALWAYS this overload
//     GetUserIdAsync(securityContext)                     ← ALWAYS this overload
//
// The parameterless overloads read a ClaimsPrincipal captured in the broker's
// CONSTRUCTOR. That is correct only while the broker instance is built inside the
// request it stamps for — never true on the event path, where the ambient
// principal is whoever published the triggering fact, not the actor the signature
// verified. Those overloads no longer exist; use the SecurityContext ones.
//
// Per design §14.6 the foundation enforces security ITSELF. An exposer may bind
// to the foundation directly, so no layer may assume an upstream layer already
// gated the caller. Per §14.6 rule 4, the event path verifies the envelope's
// integrity signature in the RECEIVER — a handler is reachable without going
// through the broker, and without that check a caller who can put a message on
// the address states their own identity and roles and is believed.
//
// ─────────────────────────────────────────────────────────────────────────────
// FILES THIS TEMPLATE PRODUCES
// ─────────────────────────────────────────────────────────────────────────────
//   Services/Foundations/{Entity}s/I{Entity}Service.cs
//   Services/Foundations/{Entity}s/I{Entity}Service.Substrate.cs
//   Services/Foundations/{Entity}s/{Entity}Service.cs
//   Services/Foundations/{Entity}s/{Entity}Service.Substrate.cs
//   Services/Foundations/{Entity}s/{Entity}Service.Validations.cs
//   Services/Foundations/{Entity}s/{Entity}Service.Exceptions.cs
//   Models/Events/Foundations/{Entity}EventOperation.cs
//   Models/Configurations/EventBrokerIdentifiers.{Entity}.cs
//   Models/Foundations/{Entity}s/Exceptions/*.cs            (15 files)
//   Tests/Unit/Services/Foundations/{Entity}s/*.cs
//
// Also required, outside this template:
//   • Registrations/EventSubscriptionRegistration.cs — wire each request address
//     to its handler. The service EXPOSES the capability; the central
//     registration decides what is connected.
//   • Brokers/Events/IEventBroker.{Entity}.cs + EventBroker.{Entity}.cs —
//     the Publish/Subscribe pair for this entity.
//   • Brokers/Storages/Sql — the Insert/Select/Update/Delete pair.
//
// Entities needing extra shape add further partials alongside these, following
// the same convention: {Entity}Service.Transitions.cs (state machine),
// {Entity}Service.Lookup.cs (non-CRUD reads). Do not fold them into the files
// below.
// ═══════════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 1: INTERFACE — NON-EVENT PATH
// ═══════════════════════════════════════════════════════════════════════════════

// I{Entity}Service.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using {Namespace}.Models.Foundations.{Entity}s;

namespace {Namespace}.Services.Foundations.{Entity}s
{
    internal partial interface I{Entity}Service
    {
        ValueTask<{Entity}> Add{Entity}Async(
            {Entity} {entity},
            CancellationToken cancellationToken = default);

        ValueTask<IQueryable<{Entity}>> RetrieveAll{Entity}sAsync(
            CancellationToken cancellationToken = default);

        ValueTask<{Entity}> Retrieve{Entity}ByIdAsync(
            Guid {entity}Id,
            CancellationToken cancellationToken = default);

        ValueTask<{Entity}> Modify{Entity}Async(
            {Entity} {entity},
            CancellationToken cancellationToken = default);

        ValueTask<{Entity}> Remove{Entity}ByIdAsync(
            Guid {entity}Id,
            string? deletionReason = null,
            CancellationToken cancellationToken = default);

        ValueTask<{Entity}> HardRemove{Entity}ByIdAsync(
            Guid {entity}Id,
            CancellationToken cancellationToken = default);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 2: INTERFACE — EVENT PATH
// ═══════════════════════════════════════════════════════════════════════════════

// I{Entity}Service.Substrate.cs
using System.Threading;
using System.Threading.Tasks;
using {Namespace}.Models.Events;
using {Namespace}.Models.Foundations.{Entity}s;

namespace {Namespace}.Services.Foundations.{Entity}s
{
    /// <summary>
    /// The event-facing surface of the service: request handlers invoked by the event
    /// substrate, one per request address. These are wired to event listeners exclusively in
    /// <c>EventSubscriptionRegistration</c> — the service exposes the capability; the central
    /// registration decides what is connected. Every handler replies with the operation's
    /// outcome envelope (recorded on the delivery), or <c>null</c> when a duplicated request
    /// was skipped.
    /// </summary>
    internal partial interface I{Entity}Service
    {
        ValueTask<EventEnvelope<{Entity}>?> OnAdding{Entity}Async(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default);

        ValueTask<EventEnvelope<{Entity}>?> OnModifying{Entity}Async(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default);

        ValueTask<EventEnvelope<{Entity}>?> OnRemoving{Entity}ByIdAsync(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default);

        ValueTask<EventEnvelope<{Entity}>?> OnHardRemoving{Entity}ByIdAsync(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default);

        ValueTask<EventEnvelope<{Entity}>?> OnRetrieving{Entity}ByIdAsync(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 3: EVENT OPERATION ENUM
// ═══════════════════════════════════════════════════════════════════════════════

// {Entity}EventOperation.cs
namespace {Namespace}.Models.Events.Foundations
{
    /// <summary>
    /// The operations a <c>{Entity}</c> event can represent — requests (present tense:
    /// <see cref="Adding"/>, <see cref="Modifying"/>, <see cref="RemovingById"/>,
    /// <see cref="HardRemovingById"/>, <see cref="RetrievingById"/>) answered by responder
    /// handlers, and facts (past tense: <see cref="Added"/>, <see cref="Modified"/>,
    /// <see cref="Removed"/>, <see cref="HardRemoved"/>) published by the service after the
    /// work is done. Every request operation maps to its own event address (for example
    /// <c>{Entity}-Adding</c>) and composes the stored event name (for example
    /// <c>"{Entity}Adding"</c>). <see cref="HardRemoved"/> shares the <see cref="Removed"/>
    /// event address and is distinguished purely by its event name
    /// (<c>"{Entity}HardRemoved"</c>). Entity-specific operations may be appended here (with
    /// a matching event address in <c>EventBrokerIdentifiers</c>) without affecting any
    /// other entity.
    /// </summary>
    public enum {Entity}EventOperation
    {
        Adding,
        Modifying,
        RemovingById,
        HardRemovingById,
        RetrievingById,
        Added,
        Modified,
        Removed,
        HardRemoved
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 4: EVENT BROKER IDENTIFIERS
// ═══════════════════════════════════════════════════════════════════════════════

// EventBrokerIdentifiers.{Entity}.cs
//
// Address and subscription ids are STABLE GUIDs — generate each one once and never
// regenerate it. The substrate keys its stored addresses and subscriptions off these
// values; changing one silently orphans every existing subscription on that address.
//
// ⚠ REPLACE EVERY ZERO GUID BELOW BEFORE BUILDING. There are thirteen, and leaving any
// two of the eight ADDRESS ids equal does NOT fail to compile — it throws
// ArgumentException while initialising the {Entity}EventAddresses dictionary, which
// surfaces as a TypeInitializationException on first touch of EventBrokerIdentifiers.
// That static class is shared by every entity, so one unedited copy takes the whole
// event layer down at runtime, not just this entity's. Generate them with
// `[guid]::NewGuid()` (PowerShell) or `uuidgen`, one per line.

using System;
using System.Collections.Generic;
using {Namespace}.Models.Events.Foundations;

namespace {Namespace}.Models.Configurations
{
    internal static partial class EventBrokerIdentifiers
    {
        public static readonly Guid {Entity}AddingEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}ModifyingEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}RemovingByIdEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}HardRemovingByIdEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}RetrievingByIdEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}AddedEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}ModifiedEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public static readonly Guid {Entity}RemovedEventAddressId =
            new Guid("00000000-0000-0000-0000-000000000000");

        internal static readonly IReadOnlyDictionary<{Entity}EventOperation, Guid>
            {Entity}EventAddressIds = new Dictionary<{Entity}EventOperation, Guid>
            {
                { {Entity}EventOperation.Adding, {Entity}AddingEventAddressId },
                { {Entity}EventOperation.Modifying, {Entity}ModifyingEventAddressId },
                { {Entity}EventOperation.RemovingById, {Entity}RemovingByIdEventAddressId },
                { {Entity}EventOperation.HardRemovingById, {Entity}HardRemovingByIdEventAddressId },
                { {Entity}EventOperation.RetrievingById, {Entity}RetrievingByIdEventAddressId },
                { {Entity}EventOperation.Added, {Entity}AddedEventAddressId },
                { {Entity}EventOperation.Modified, {Entity}ModifiedEventAddressId },

                // HardRemoved is published to the SAME address as Removed on purpose —
                // consumers subscribe to one removal address and distinguish hard removals
                // by the composed event name ("{Entity}HardRemoved" vs "{Entity}Removed").
                { {Entity}EventOperation.Removed, {Entity}RemovedEventAddressId },
                { {Entity}EventOperation.HardRemoved, {Entity}RemovedEventAddressId }
            };

        internal static readonly IReadOnlyDictionary<Guid, string> {Entity}EventAddresses =
            new Dictionary<Guid, string>
            {
                { {Entity}AddingEventAddressId, "{Entity}-Adding" },
                { {Entity}ModifyingEventAddressId, "{Entity}-Modifying" },
                { {Entity}RemovingByIdEventAddressId, "{Entity}-RemovingById" },
                { {Entity}HardRemovingByIdEventAddressId, "{Entity}-HardRemovingById" },
                { {Entity}RetrievingByIdEventAddressId, "{Entity}-RetrievingById" },
                { {Entity}AddedEventAddressId, "{Entity}-Added" },
                { {Entity}ModifiedEventAddressId, "{Entity}-Modified" },
                { {Entity}RemovedEventAddressId, "{Entity}-Removed" }
            };

        public static readonly Guid {Entity}OnAdding{Entity}SubscriptionId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public const string {Entity}OnAdding{Entity}SubscriptionName =
            "{Entity}Service.OnAdding{Entity}";

        public static readonly Guid {Entity}OnModifying{Entity}SubscriptionId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public const string {Entity}OnModifying{Entity}SubscriptionName =
            "{Entity}Service.OnModifying{Entity}";

        public static readonly Guid {Entity}OnRemoving{Entity}ByIdSubscriptionId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public const string {Entity}OnRemoving{Entity}ByIdSubscriptionName =
            "{Entity}Service.OnRemoving{Entity}ById";

        public static readonly Guid {Entity}OnHardRemoving{Entity}ByIdSubscriptionId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public const string {Entity}OnHardRemoving{Entity}ByIdSubscriptionName =
            "{Entity}Service.OnHardRemoving{Entity}ById";

        public static readonly Guid {Entity}OnRetrieving{Entity}ByIdSubscriptionId =
            new Guid("00000000-0000-0000-0000-000000000000");

        public const string {Entity}OnRetrieving{Entity}ByIdSubscriptionName =
            "{Entity}Service.OnRetrieving{Entity}ById";
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 5: SERVICE IMPLEMENTATION — MAIN PARTIAL
// ═══════════════════════════════════════════════════════════════════════════════

// {Entity}Service.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using {Namespace}.Brokers.DateTimes;
using {Namespace}.Brokers.EventEnvelopes;
using {Namespace}.Brokers.Events;
using {Namespace}.Brokers.Identifiers;
using {Namespace}.Brokers.Integrities;
using {Namespace}.Brokers.Loggings;
using {Namespace}.Brokers.Securities;
using {Namespace}.Brokers.Storages.Sql;
using {Namespace}.Models.Configurations;
using {Namespace}.Models.Events;
using {Namespace}.Models.Events.Foundations;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.{Entity}s.Exceptions;

namespace {Namespace}.Services.Foundations.{Entity}s
{
    /// <summary>
    /// Foundation service for {entity display}s. Every addressed operation is both callable
    /// directly (the non-event path: object in → request envelope → shared do-work) and
    /// reachable through the event substrate (the event path in the <c>.Substrate</c> partial:
    /// request envelope in → shared do-work); <c>RetrieveAll{Entity}sAsync</c> is the exception,
    /// having no address and doing its work inline. The private <c>DoXAsync</c> methods own auditing, validation,
    /// storage, and publishing the past-tense fact, so the two paths cannot diverge; the
    /// inbound envelope carries the original caller's <c>SecurityContext</c> and anchors the
    /// causation chain. Per design §14.6 the foundation enforces security itself — never
    /// assuming an upstream orchestration already gated the caller.
    ///
    /// <para>Replace this paragraph with the entity's actual security posture: who may write,
    /// who may read, and what a caller who may not see a row is told (not-found, never
    /// unauthorized). State it here so a reviewer can check the code against a claim.</para>
    /// </summary>
    internal partial class {Entity}Service : I{Entity}Service
    {
        private readonly IStorageBroker storageBroker;
        private readonly IDateTimeBroker dateTimeBroker;
        private readonly IIdentifierBroker identifierBroker;
        private readonly IEventBroker eventBroker;
        private readonly IEventEnvelopeBroker eventEnvelopeBroker;
        private readonly ISecurityAuditBroker securityAuditBroker;
        private readonly IEnvelopeIntegrityBroker envelopeIntegrityBroker;
        private readonly ILoggingBroker loggingBroker;

        public {Entity}Service(
            IStorageBroker storageBroker,
            IDateTimeBroker dateTimeBroker,
            IIdentifierBroker identifierBroker,
            IEventBroker eventBroker,
            IEventEnvelopeBroker eventEnvelopeBroker,
            ISecurityAuditBroker securityAuditBroker,
            IEnvelopeIntegrityBroker envelopeIntegrityBroker,
            ILoggingBroker loggingBroker)
        {
            this.storageBroker = storageBroker;
            this.dateTimeBroker = dateTimeBroker;
            this.identifierBroker = identifierBroker;
            this.eventBroker = eventBroker;
            this.eventEnvelopeBroker = eventEnvelopeBroker;
            this.securityAuditBroker = securityAuditBroker;
            this.envelopeIntegrityBroker = envelopeIntegrityBroker;
            this.loggingBroker = loggingBroker;
        }

        // ── the non-event path ────────────────────────────────────────────────
        // Each public method does exactly three things: guard the token, mint a request
        // envelope capturing the ambient caller, and hand off to the shared do-work. No
        // logic lives here — everything a reviewer cares about is in DoXAsync, where the
        // event path can reach it too.

        public ValueTask<{Entity}> Add{Entity}Async(
            {Entity} {entity},
            CancellationToken cancellationToken = default) =>
            TryCatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Validate{Entity}IsNotNull({entity});

                EventEnvelope<{Entity}> envelope =
                    await this.eventEnvelopeBroker.CreateAsync(content: {entity});

                return await DoAdd{Entity}Async(
                    {entity}: {entity},
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);
            });

        public ValueTask<IQueryable<{Entity}>> RetrieveAll{Entity}sAsync(
            CancellationToken cancellationToken = default) =>
            TryCatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // the envelope exists to capture the ambient security context the
                // visibility filter runs against — the request payload is empty
                EventEnvelope<{Entity}> envelope =
                    await this.eventEnvelopeBroker.CreateAsync(content: new {Entity}());

                IQueryable<{Entity}> all{Entity}s =
                    await this.storageBroker.SelectAll{Entity}sAsync(cancellationToken);

                return ApplyCollectionReadVisibilityFilter(
                    {entity}s: all{Entity}s,
                    securityContext: envelope.SecurityContext);
            });

        public ValueTask<{Entity}> Retrieve{Entity}ByIdAsync(
            Guid {entity}Id,
            CancellationToken cancellationToken = default) =>
            TryCatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var retrieveRequest = new {Entity}
                {
                    Id = {entity}Id
                };

                EventEnvelope<{Entity}> envelope =
                    await this.eventEnvelopeBroker.CreateAsync(content: retrieveRequest);

                return await DoRetrieve{Entity}ByIdAsync(
                    {entity}Id: {entity}Id,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);
            });

        public ValueTask<{Entity}> Modify{Entity}Async(
            {Entity} {entity},
            CancellationToken cancellationToken = default) =>
            TryCatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Validate{Entity}IsNotNull({entity});

                EventEnvelope<{Entity}> envelope =
                    await this.eventEnvelopeBroker.CreateAsync(content: {entity});

                return await DoModify{Entity}Async(
                    {entity}: {entity},
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);
            });

        public ValueTask<{Entity}> Remove{Entity}ByIdAsync(
            Guid {entity}Id,
            string? deletionReason = null,
            CancellationToken cancellationToken = default) =>
            TryCatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var removeRequest = new {Entity}
                {
                    Id = {entity}Id,
                    DeletionReason = deletionReason
                };

                EventEnvelope<{Entity}> envelope =
                    await this.eventEnvelopeBroker.CreateAsync(content: removeRequest);

                return await DoRemove{Entity}ByIdAsync(
                    {entity}Id: {entity}Id,
                    deletionReason: deletionReason,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);
            });

        public ValueTask<{Entity}> HardRemove{Entity}ByIdAsync(
            Guid {entity}Id,
            CancellationToken cancellationToken = default) =>
            TryCatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hardRemoveRequest = new {Entity}
                {
                    Id = {entity}Id
                };

                EventEnvelope<{Entity}> envelope =
                    await this.eventEnvelopeBroker.CreateAsync(content: hardRemoveRequest);

                return await DoHardRemove{Entity}ByIdAsync(
                    {entity}Id: {entity}Id,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);
            });

        // ── the shared do-work ────────────────────────────────────────────────
        // Reached from BOTH paths. Owns the security gate, auditing, validation, storage,
        // the ProcessedEvents dual-record, and the published fact. The SecurityContext
        // always comes off inboundEnvelope — never from an ambient accessor.

        // A row the caller may not see answers NOT-FOUND, never unauthorized: an
        // authorization error would confirm the row exists. The true denial reason is
        // logged server-side only. Adjust the gate to this entity's posture, but keep
        // that shape.
        private async ValueTask<{Entity}> DoRetrieve{Entity}ByIdAsync(
            Guid {entity}Id,
            EventEnvelope<{Entity}> inboundEnvelope,
            CancellationToken cancellationToken)
        {
            ValidateOnRetrieve{Entity}ById({entity}Id);

            {Entity} maybe{Entity} =
                await this.storageBroker.Select{Entity}ByIdAsync({entity}Id, cancellationToken);

            ValidateStorage{Entity}(maybe{Entity}, {entity}Id);

            if (maybe{Entity}.IsDeleted)
            {
                await this.loggingBroker.LogInformationAsync(
                    message: $"{Entity Display} read denied. {Entity Display} {{entity}Id} is " +
                        "soft-deleted; reported to the caller as not found.");

                throw new NotFound{Entity}Exception(
                    message: $"{Entity Display} not found with id: {{entity}Id}.");
            }

            SecurityContext? securityContext = inboundEnvelope.SecurityContext;

            if (securityContext is null || securityContext.IsAuthenticated is false)
            {
                await this.loggingBroker.LogWarningAsync(
                    message: $"{Entity Display} read denied. {Entity Display} {{entity}Id} is " +
                        "visible to authenticated callers only and the caller is not " +
                        "authenticated; reported to the caller as not found.");

                throw new NotFound{Entity}Exception(
                    message: $"{Entity Display} not found with id: {{entity}Id}.");
            }

            return maybe{Entity};
        }

        // the collection twin of the single-row posture: a row the caller may not see
        // drops out of the set instead of erroring, so a collection read never reveals
        // how many rows exist
        private static IQueryable<{Entity}> ApplyCollectionReadVisibilityFilter(
            IQueryable<{Entity}> {entity}s,
            SecurityContext? securityContext)
        {
            IQueryable<{Entity}> visible{Entity}s = {entity}s.Where({entity} =>
                {entity}.IsDeleted == false);

            bool isAuthenticated =
                securityContext is not null && securityContext.IsAuthenticated;

            if (isAuthenticated is false)
            {
                return visible{Entity}s.Where({entity} => false);
            }

            return visible{Entity}s;
        }

        private async ValueTask<{Entity}> DoAdd{Entity}Async(
            {Entity} {entity},
            EventEnvelope<{Entity}> inboundEnvelope,
            CancellationToken cancellationToken)
        {
            ValidateUserIsAllowedToWrite{Entity}(inboundEnvelope.SecurityContext);

            {entity} = await this.securityAuditBroker
                .ApplyAddAuditValuesAsync(entity: {entity}, securityContext: inboundEnvelope.SecurityContext);

            await ValidateOnAdd{Entity}Async(
                {entity}: {entity},
                securityContext: inboundEnvelope.SecurityContext);

            {Entity} added{Entity} =
                await this.storageBroker.Insert{Entity}Async({entity}, cancellationToken);

            await RecordEventProcessedAsync(
                envelope: inboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName,
                cancellationToken: cancellationToken);

            EventEnvelope<{Entity}> outboundEnvelope =
                await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: inboundEnvelope,
                    content: added{Entity});

            await this.eventBroker.Publish{Entity}Async(
                envelope: outboundEnvelope,
                operation: {Entity}EventOperation.Added);

            // dual-record: the INBOUND id so a replayed request is skipped, and the
            // OUTBOUND id so the fact this call published cannot loop back in and be
            // applied a second time
            await RecordEventProcessedAsync(
                envelope: outboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName,
                cancellationToken: cancellationToken);

            return added{Entity};
        }

        private async ValueTask<{Entity}> DoModify{Entity}Async(
            {Entity} {entity},
            EventEnvelope<{Entity}> inboundEnvelope,
            CancellationToken cancellationToken)
        {
            ValidateUserIsAllowedToWrite{Entity}(inboundEnvelope.SecurityContext);

            {entity} = await this.securityAuditBroker
                .ApplyModifyAuditValuesAsync(entity: {entity}, securityContext: inboundEnvelope.SecurityContext);

            await ValidateOnModify{Entity}Async(
                {entity}: {entity},
                securityContext: inboundEnvelope.SecurityContext);

            {Entity} maybe{Entity} = await this.storageBroker.Select{Entity}ByIdAsync(
                {entity}Id: {entity}.Id,
                cancellationToken: cancellationToken);

            ValidateStorage{Entity}(maybe{Entity}, {entity}Id: {entity}.Id);

            {entity} = await this.securityAuditBroker
                .EnsureOtherAuditValuesRemainsUnchangedOnModifyAsync(
                    entity: {entity},
                    storageEntity: maybe{Entity});

            ValidateAgainstStorage{Entity}OnModify(
                input{Entity}: {entity},
                storage{Entity}: maybe{Entity});

            {Entity} updated{Entity} =
                await this.storageBroker.Update{Entity}Async({entity}, cancellationToken);

            await RecordEventProcessedAsync(
                envelope: inboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnModifying{Entity}SubscriptionName,
                cancellationToken: cancellationToken);

            EventEnvelope<{Entity}> outboundEnvelope =
                await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: inboundEnvelope,
                    content: updated{Entity});

            await this.eventBroker.Publish{Entity}Async(
                envelope: outboundEnvelope,
                operation: {Entity}EventOperation.Modified);

            await RecordEventProcessedAsync(
                envelope: outboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnModifying{Entity}SubscriptionName,
                cancellationToken: cancellationToken);

            return updated{Entity};
        }

        private async ValueTask<{Entity}> DoRemove{Entity}ByIdAsync(
            Guid {entity}Id,
            string? deletionReason,
            EventEnvelope<{Entity}> inboundEnvelope,
            CancellationToken cancellationToken)
        {
            // permission comes before the idempotent short-circuit, so an unauthorized
            // caller learns nothing about the row's deletion state
            ValidateUserIsAllowedToWrite{Entity}(inboundEnvelope.SecurityContext);
            ValidateOnRemove{Entity}ById({entity}Id, deletionReason);

            {Entity} maybe{Entity} =
                await this.storageBroker.Select{Entity}ByIdAsync({entity}Id, cancellationToken);

            ValidateStorage{Entity}(maybe{Entity}, {entity}Id);

            if (maybe{Entity}.IsDeleted)
                return maybe{Entity};

            // pass the reason as an argument — do NOT pre-set it on the entity and rely on
            // the audit call to leave it alone; the reason is the audit broker's to stamp
            {Entity} audited{Entity} =
                await this.securityAuditBroker.ApplyRemoveAuditValuesAsync(
                    entity: maybe{Entity},
                    securityContext: inboundEnvelope.SecurityContext,
                    deletionReason: deletionReason);

            {Entity} removed{Entity} = await this.storageBroker.Update{Entity}Async(
                {entity}: audited{Entity},
                cancellationToken: cancellationToken);

            await RecordEventProcessedAsync(
                envelope: inboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnRemoving{Entity}ByIdSubscriptionName,
                cancellationToken: cancellationToken);

            EventEnvelope<{Entity}> outboundEnvelope =
                await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: inboundEnvelope,
                    content: removed{Entity});

            await this.eventBroker.Publish{Entity}Async(
                envelope: outboundEnvelope,
                operation: {Entity}EventOperation.Removed);

            await RecordEventProcessedAsync(
                envelope: outboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnRemoving{Entity}ByIdSubscriptionName,
                cancellationToken: cancellationToken);

            return removed{Entity};
        }

        private async ValueTask<{Entity}> DoHardRemove{Entity}ByIdAsync(
            Guid {entity}Id,
            EventEnvelope<{Entity}> inboundEnvelope,
            CancellationToken cancellationToken)
        {
            ValidateUserIsAllowedToHardRemove{Entity}(inboundEnvelope.SecurityContext);
            ValidateOnHardRemove{Entity}ById({entity}Id);

            {Entity} maybe{Entity} =
                await this.storageBroker.Select{Entity}ByIdAsync({entity}Id, cancellationToken);

            ValidateStorage{Entity}(maybe{Entity}, {entity}Id);

            {Entity} deleted{Entity} =
                await this.storageBroker.Delete{Entity}Async(maybe{Entity}, cancellationToken);

            await RecordEventProcessedAsync(
                envelope: inboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnHardRemoving{Entity}ByIdSubscriptionName,
                cancellationToken: cancellationToken);

            EventEnvelope<{Entity}> outboundEnvelope =
                await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: inboundEnvelope,
                    content: deleted{Entity});

            await this.eventBroker.Publish{Entity}Async(
                envelope: outboundEnvelope,
                operation: {Entity}EventOperation.HardRemoved);

            await RecordEventProcessedAsync(
                envelope: outboundEnvelope,
                receiverName: EventBrokerIdentifiers.{Entity}OnHardRemoving{Entity}ByIdSubscriptionName,
                cancellationToken: cancellationToken);

            return deleted{Entity};
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 6: SERVICE IMPLEMENTATION — SUBSTRATE PARTIAL (EVENT PATH)
// ═══════════════════════════════════════════════════════════════════════════════

// {Entity}Service.Substrate.cs
using System.Threading;
using System.Threading.Tasks;
using {Namespace}.Models.Configurations;
using {Namespace}.Models.Events;
using {Namespace}.Models.Events.Foundations;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.ProcessedEvents;

namespace {Namespace}.Services.Foundations.{Entity}s
{
    /// <summary>
    /// The event path of the service: request handlers the event substrate dispatches to,
    /// one per request address (<c>{Entity}-Adding</c>, <c>-Modifying</c>,
    /// <c>-RemovingById</c>, <c>-HardRemovingById</c>, <c>-RetrievingById</c>). Handlers
    /// receive the full request envelope — including the original caller's
    /// <c>SecurityContext</c> — converge on the same private <c>DoXAsync</c> methods the
    /// non-event path uses (which publish the past-tense facts and record both the inbound
    /// and outbound event ids in the <c>ProcessedEvents</c> table), and return the outcome
    /// as the delivery's reply envelope. Mutating handlers check that table first so replayed
    /// or duplicated requests — including a published fact ever looping back into a request
    /// handler — are not applied twice; a deduplicated delivery replies <c>null</c>. Failures
    /// are categorized into the service's typed exceptions and rethrown so the substrate
    /// records the delivery as <c>Error</c> and drives retries; they are never swallowed.
    /// </summary>
    internal partial class {Entity}Service
    {
        public ValueTask<EventEnvelope<{Entity}>?> OnAdding{Entity}Async(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default) =>
            TryCatchSubstrate(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Validate{Entity}EventEnvelopeAsync(
                    envelope, {Entity}EventOperation.Adding);

                bool alreadyProcessed = await AlreadyProcessedAsync(
                    envelope: envelope,
                    receiverName: EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName,
                    cancellationToken: cancellationToken);

                if (alreadyProcessed)
                    return null;

                {Entity} added{Entity} = await DoAdd{Entity}Async(
                    {entity}: envelope.Content,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);

                return await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: envelope,
                    content: added{Entity});
            });

        public ValueTask<EventEnvelope<{Entity}>?> OnModifying{Entity}Async(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default) =>
            TryCatchSubstrate(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Validate{Entity}EventEnvelopeAsync(
                    envelope, {Entity}EventOperation.Modifying);

                bool alreadyProcessed = await AlreadyProcessedAsync(
                    envelope: envelope,
                    receiverName: EventBrokerIdentifiers.{Entity}OnModifying{Entity}SubscriptionName,
                    cancellationToken: cancellationToken);

                if (alreadyProcessed)
                    return null;

                {Entity} modified{Entity} = await DoModify{Entity}Async(
                    {entity}: envelope.Content,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);

                return await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: envelope,
                    content: modified{Entity});
            });

        public ValueTask<EventEnvelope<{Entity}>?> OnRemoving{Entity}ByIdAsync(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default) =>
            TryCatchSubstrate(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Validate{Entity}EventEnvelopeAsync(
                    envelope, {Entity}EventOperation.RemovingById);

                bool alreadyProcessed = await AlreadyProcessedAsync(
                    envelope: envelope,
                    receiverName: EventBrokerIdentifiers.{Entity}OnRemoving{Entity}ByIdSubscriptionName,
                    cancellationToken: cancellationToken);

                if (alreadyProcessed)
                    return null;

                {Entity} removed{Entity} = await DoRemove{Entity}ByIdAsync(
                    {entity}Id: envelope.Content.Id,
                    deletionReason: envelope.Content.DeletionReason,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);

                return await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: envelope,
                    content: removed{Entity});
            });

        public ValueTask<EventEnvelope<{Entity}>?> OnHardRemoving{Entity}ByIdAsync(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default) =>
            TryCatchSubstrate(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Validate{Entity}EventEnvelopeAsync(
                    envelope, {Entity}EventOperation.HardRemovingById);

                bool alreadyProcessed = await AlreadyProcessedAsync(
                    envelope: envelope,
                    receiverName: EventBrokerIdentifiers
                        .{Entity}OnHardRemoving{Entity}ByIdSubscriptionName,
                    cancellationToken: cancellationToken);

                if (alreadyProcessed)
                    return null;

                {Entity} deleted{Entity} = await DoHardRemove{Entity}ByIdAsync(
                    {entity}Id: envelope.Content.Id,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);

                return await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: envelope,
                    content: deleted{Entity});
            });

        public ValueTask<EventEnvelope<{Entity}>?> OnRetrieving{Entity}ByIdAsync(
            EventEnvelope<{Entity}> envelope,
            CancellationToken cancellationToken = default) =>
            TryCatchSubstrate(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Validate{Entity}EventEnvelopeAsync(
                    envelope, {Entity}EventOperation.RetrievingById);

                // read-only: naturally idempotent, so no ProcessedEvents bookkeeping; the
                // shared do-work runs the visibility posture against the REQUEST envelope's
                // security context, not the ambient one
                {Entity} retrieved{Entity} = await DoRetrieve{Entity}ByIdAsync(
                    {entity}Id: envelope.Content.Id,
                    inboundEnvelope: envelope,
                    cancellationToken: cancellationToken);

                return await this.eventEnvelopeBroker.CreateNextAsync(
                    sourceEnvelope: envelope,
                    content: retrieved{Entity});
            });

        private async ValueTask<bool> AlreadyProcessedAsync(
            EventEnvelope<{Entity}> envelope,
            string receiverName,
            CancellationToken cancellationToken) =>
            await this.storageBroker.SelectProcessedEventExistsAsync(
                eventId: envelope.Metadata.EventId,
                receiverName: receiverName,
                cancellationToken: cancellationToken);

        private async ValueTask RecordEventProcessedAsync(
            EventEnvelope<{Entity}> envelope,
            string receiverName,
            CancellationToken cancellationToken) =>
            await this.storageBroker.InsertProcessedEventAsync(
                processedEvent: new ProcessedEvent
                {
                    Id = await this.identifierBroker.GetIdentifierAsync(),
                    EventId = envelope.Metadata.EventId,
                    ReceiverName = receiverName,
                    ProcessedAt = await this.dateTimeBroker.GetCurrentDateTimeOffsetAsync()
                },
                cancellationToken: cancellationToken);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 7: SERVICE IMPLEMENTATION — VALIDATIONS PARTIAL
// ═══════════════════════════════════════════════════════════════════════════════

// {Entity}Service.Validations.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using {Namespace}.Models.Events;
using {Namespace}.Models.Events.Foundations;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.{Entity}s.Exceptions;
using {Namespace}.Models.Securities;

namespace {Namespace}.Services.Foundations.{Entity}s
{
    internal partial class {Entity}Service
    {
        // the foundation enforces the same security rules as the orchestration (design
        // §14.6): an exposer may bind to either service directly, so no layer may assume
        // an upstream layer already gated the caller

        // Replace with this entity's actual write posture. The shape below — authenticated
        // first, then the global ReadOnly ban, then the permission itself — is the order to
        // keep: the ban precedes the role check so a banned administrator cannot reach past it.
        private static void ValidateUserIsAllowedToWrite{Entity}(
            SecurityContext securityContext)
        {
            if (securityContext is null || securityContext.IsAuthenticated is false)
            {
                throw new Unauthorized{Entity}Exception(
                    message: "The current user is not authenticated.");
            }

            if (securityContext.Roles.Contains(Roles.ReadOnly))
            {
                throw new Unauthorized{Entity}Exception(
                    message: "The current user is blocked from writing {entity display}s.");
            }

            if (securityContext.Roles.Contains(Roles.Administrators) is false)
            {
                throw new Unauthorized{Entity}Exception(
                    message: "The current user is not allowed to write {entity display}s.");
            }
        }

        // hard removal is a separate gate from the ordinary write: it destroys the audit
        // trail, so it is Administrators-only even where ordinary writes are wider
        private static void ValidateUserIsAllowedToHardRemove{Entity}(
            SecurityContext securityContext)
        {
            if (securityContext is null || securityContext.IsAuthenticated is false)
            {
                throw new Unauthorized{Entity}Exception(
                    message: "The current user is not authenticated.");
            }

            if (securityContext.Roles.Contains(Roles.ReadOnly))
            {
                throw new Unauthorized{Entity}Exception(
                    message: "The current user is blocked from removing {entity display}s.");
            }

            if (securityContext.Roles.Contains(Roles.Administrators) is false)
            {
                throw new Unauthorized{Entity}Exception(
                    message: "The current user is not allowed to hard remove {entity display}s.");
            }
        }

        private async ValueTask ValidateOnAdd{Entity}Async(
            {Entity} {entity},
            SecurityContext securityContext)
        {
            Validate{Entity}IsNotNull({entity});

            // the acting user id comes from the ENVELOPE's context, so the CreatedBy this
            // rule pins is the actor the signature verified — not whoever is ambient
            string currentUserId = await this.securityAuditBroker.GetUserIdAsync(securityContext);

            Validate(
                message: "{Entity Display} is invalid, fix the errors and try again.",
                (Rule: IsInvalid({entity}.Id), Parameter: nameof({Entity}.Id)),
                (Rule: IsInvalid({entity}.CreatedBy), Parameter: nameof({Entity}.CreatedBy)),
                (Rule: IsInvalid({entity}.UpdatedBy), Parameter: nameof({Entity}.UpdatedBy)),
                (Rule: IsInvalid({entity}.CreatedWhen), Parameter: nameof({Entity}.CreatedWhen)),
                (Rule: IsInvalid({entity}.UpdatedWhen), Parameter: nameof({Entity}.UpdatedWhen)),

                (Rule: IsGreaterThan({entity}.CreatedBy, 255),
                    Parameter: nameof({Entity}.CreatedBy)),

                (Rule: IsGreaterThan({entity}.UpdatedBy, 255),
                    Parameter: nameof({Entity}.UpdatedBy)),

                (Rule: IsNotSame(
                        firstDate: {entity}.UpdatedWhen,
                        secondDate: {entity}.CreatedWhen,
                        secondDateName: nameof({Entity}.CreatedWhen)),
                    Parameter: nameof({Entity}.UpdatedWhen)),

                (Rule: IsNotSame(
                        first: currentUserId,
                        second: {entity}.CreatedBy),
                    Parameter: nameof({Entity}.CreatedBy)),

                (Rule: IsNotSame(
                        first: {entity}.UpdatedBy,
                        second: {entity}.CreatedBy,
                        secondName: nameof({Entity}.CreatedBy)),
                    Parameter: nameof({Entity}.UpdatedBy)),

                (Rule: await IsNotRecentAsync({entity}.CreatedWhen),
                    Parameter: nameof({Entity}.CreatedWhen)));
        }

        private async ValueTask ValidateOnModify{Entity}Async(
            {Entity} {entity},
            SecurityContext securityContext)
        {
            Validate{Entity}IsNotNull({entity});
            string currentUserId = await this.securityAuditBroker.GetUserIdAsync(securityContext);

            Validate(
                message: "{Entity Display} is invalid, fix the errors and try again.",
                (Rule: IsInvalid({entity}.Id), Parameter: nameof({Entity}.Id)),
                (Rule: IsInvalid({entity}.CreatedBy), Parameter: nameof({Entity}.CreatedBy)),
                (Rule: IsInvalid({entity}.UpdatedBy), Parameter: nameof({Entity}.UpdatedBy)),
                (Rule: IsInvalid({entity}.CreatedWhen), Parameter: nameof({Entity}.CreatedWhen)),
                (Rule: IsInvalid({entity}.UpdatedWhen), Parameter: nameof({Entity}.UpdatedWhen)),

                (Rule: IsGreaterThan({entity}.CreatedBy, 255),
                    Parameter: nameof({Entity}.CreatedBy)),

                (Rule: IsGreaterThan({entity}.UpdatedBy, 255),
                    Parameter: nameof({Entity}.UpdatedBy)),

                (Rule: IsNotSame(
                        first: currentUserId,
                        second: {entity}.UpdatedBy),
                    Parameter: nameof({Entity}.UpdatedBy)),

                (Rule: IsSame(
                        firstDate: {entity}.UpdatedWhen,
                        secondDate: {entity}.CreatedWhen,
                        secondDateName: nameof({Entity}.CreatedWhen)),
                    Parameter: nameof({Entity}.UpdatedWhen)),

                (Rule: await IsNotRecentAsync({entity}.UpdatedWhen),
                    Parameter: nameof({Entity}.UpdatedWhen)));
        }

        // Null-check first (a malformed event), then verify the integrity signature against the
        // event name this handler serves and the request direction. The signature is what makes
        // the envelope's SecurityContext trustworthy on the event path: without it a caller who can
        // put a message on this address states their own identity and roles and is believed
        // (design §14.6 rule 4). Verification sits in the receiver, not the transport, because a
        // handler is reachable without going through the broker.
        private async ValueTask Validate{Entity}EventEnvelopeAsync(
            EventEnvelope<{Entity}> envelope,
            {Entity}EventOperation operation)
        {
            if (envelope is null || envelope.Content is null || envelope.Metadata is null)
            {
                throw new Invalid{Entity}EventException(
                    message: "Invalid {entity display} event. " +
                        "The event envelope, its content and metadata are required.");
            }

            string eventName = $"{nameof({Entity})}{operation}";

            bool isSignatureValid = await this.envelopeIntegrityBroker.VerifyAsync(
                envelope, eventName, EnvelopeDirection.Request);

            if (isSignatureValid is false)
            {
                throw new Invalid{Entity}EventException(
                    message: "Invalid {entity display} event. Integrity verification failed.");
            }
        }

        private static void ValidateAgainstStorage{Entity}OnModify(
            {Entity} input{Entity},
            {Entity} storage{Entity})
        {
            Validate(
                message: "{Entity Display} is invalid, fix the errors and try again.",

                (Rule: IsNotSame(
                        firstDate: input{Entity}.CreatedWhen,
                        secondDate: storage{Entity}.CreatedWhen,
                        secondDateName: nameof({Entity}.CreatedWhen)),
                    Parameter: nameof({Entity}.CreatedWhen)),

                (Rule: IsNotSame(
                        first: input{Entity}.CreatedBy,
                        second: storage{Entity}.CreatedBy,
                        secondName: nameof({Entity}.CreatedBy)),
                    Parameter: nameof({Entity}.CreatedBy)),

                (Rule: IsSame(
                        firstDate: input{Entity}.UpdatedWhen,
                        secondDate: storage{Entity}.UpdatedWhen,
                        secondDateName: nameof({Entity}.UpdatedWhen)),
                    Parameter: nameof({Entity}.UpdatedWhen)));
        }

        private static void ValidateOnRetrieve{Entity}ById(Guid {entity}Id) =>
            Validate(
                message: "{Entity Display} is invalid, fix the errors and try again.",
                (Rule: IsInvalid({entity}Id), Parameter: nameof({Entity}.Id)));

        // the deletion reason is caller-supplied free text that lands on the row unchanged,
        // so its storage cap is enforced here rather than left to the column to reject
        private static void ValidateOnRemove{Entity}ById(
            Guid {entity}Id,
            string? deletionReason) =>
            Validate(
                message: "{Entity Display} is invalid, fix the errors and try again.",
                (Rule: IsInvalid({entity}Id), Parameter: nameof({Entity}.Id)),

                (Rule: IsGreaterThan(deletionReason, 500),
                    Parameter: nameof({Entity}.DeletionReason)));

        private static void ValidateOnHardRemove{Entity}ById(Guid {entity}Id) =>
            Validate(
                message: "{Entity Display} is invalid, fix the errors and try again.",
                (Rule: IsInvalid({entity}Id), Parameter: nameof({Entity}.Id)));

        private static void ValidateStorage{Entity}({Entity} maybe{Entity}, Guid {entity}Id)
        {
            if (maybe{Entity} is null)
            {
                throw new NotFound{Entity}Exception(
                    message: $"{Entity Display} not found with id: {{entity}Id}.");
            }
        }

        private static void Validate{Entity}IsNotNull({Entity} {entity})
        {
            if ({entity} is null)
            {
                throw new Null{Entity}Exception(message: "{Entity Display} is null.");
            }
        }

        private static dynamic IsInvalid(Guid id) => new
        {
            Condition = id == Guid.Empty,
            Message = "Id is required"
        };

        private static dynamic IsInvalid(string text) => new
        {
            Condition = string.IsNullOrWhiteSpace(text),
            Message = "Text is required"
        };

        private static dynamic IsInvalid(DateTimeOffset date) => new
        {
            Condition = date == default,
            Message = "Date is required"
        };

        private static dynamic IsNotSame(
            string first,
            string second) => new
            {
                Condition = first != second,
                Message = $"Expected value to be '{first}' but found '{second}'."
            };

        private static dynamic IsNotSame(
            string first,
            string second,
            string secondName) => new
            {
                Condition = first != second,
                Message = $"Text is not the same as {secondName}"
            };

        private static dynamic IsNotSame(
            DateTimeOffset firstDate,
            DateTimeOffset secondDate,
            string secondDateName) => new
            {
                Condition = firstDate != secondDate,
                Message = $"Date is not the same as {secondDateName}"
            };

        private static dynamic IsGreaterThan(string? text, int maxLength) => new
        {
            Condition = (text ?? string.Empty).Length > maxLength,
            Message = $"Text exceed max length of {maxLength} characters"
        };

        private static dynamic IsSame(
            DateTimeOffset firstDate,
            DateTimeOffset secondDate,
            string secondDateName) => new
            {
                Condition = firstDate == secondDate,
                Message = $"Date is the same as {secondDateName}"
            };

        private async ValueTask<dynamic> IsNotRecentAsync(DateTimeOffset date)
        {
            var (isNotRecent, startDate, endDate) = await IsDateNotRecentAsync(date);

            return new
            {
                Condition = isNotRecent,
                Message = $"Date is not recent. Expected a value between {startDate} and {endDate} but found {date}"
            };
        }

        private async ValueTask<(bool IsNotRecent, DateTimeOffset StartDate, DateTimeOffset EndDate)>
            IsDateNotRecentAsync(DateTimeOffset date)
        {
            int pastThreshold = 90;
            int futureThreshold = 0;
            DateTimeOffset currentDateTime = await this.dateTimeBroker.GetCurrentDateTimeOffsetAsync();
            DateTimeOffset startDate = currentDateTime.AddSeconds(-pastThreshold);
            DateTimeOffset endDate = currentDateTime.AddSeconds(futureThreshold);
            bool isNotRecent = date < startDate || date > endDate;

            return (isNotRecent, startDate, endDate);
        }

        private static void Validate(
            string message,
            params (dynamic Rule, string Parameter)[] validations)
        {
            var invalid{Entity}Exception = new Invalid{Entity}Exception(message);

            foreach ((dynamic rule, string parameter) in validations)
            {
                if (rule.Condition)
                {
                    invalid{Entity}Exception.UpsertDataList(
                        key: parameter,
                        value: rule.Message);
                }
            }

            invalid{Entity}Exception.ThrowIfContainsErrors();
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 8: SERVICE IMPLEMENTATION — EXCEPTIONS PARTIAL
// ═══════════════════════════════════════════════════════════════════════════════

// {Entity}Service.Exceptions.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using EFxceptions.Models.Exceptions;
using {Namespace}.Models.Events;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.{Entity}s.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xeptions;

namespace {Namespace}.Services.Foundations.{Entity}s
{
    internal partial class {Entity}Service
    {
        private delegate ValueTask<{Entity}> Returning{Entity}Function();
        private delegate ValueTask<IQueryable<{Entity}>> Returning{Entity}sFunction();

        private delegate ValueTask<EventEnvelope<{Entity}>?>
            Returning{Entity}EventEnvelopeFunction();

        // The event-path wrapper: categorizes failures with the same taxonomy as the
        // non-event TryCatch (so the two entry paths cannot diverge), plus the envelope
        // guard that only exists on this path, and ALWAYS rethrows so the substrate records
        // the delivery as Error and drives retries. Exceptions already categorized by nested
        // service calls pass through unwrapped.
        private async ValueTask<EventEnvelope<{Entity}>?> TryCatchSubstrate(
            Returning{Entity}EventEnvelopeFunction returning{Entity}EventEnvelopeFunction)
        {
            try
            {
                return await returning{Entity}EventEnvelopeFunction();
            }
            catch (OperationCanceledException operationCanceledException)
                when (operationCanceledException.CancellationToken.IsCancellationRequested is false)
            {
                var timeoutException =
                    new TimeoutException("The dependency operation timed out.");

                var timeout{Entity}Exception =
                    new Timeout{Entity}Exception(
                        message: "Failed {entity display} timeout error occurred, contact support.",
                        innerException: timeoutException,
                        data: timeoutException.Data);

                throw await CreateAndLogTimeoutDependencyExceptionAsync(exception: timeout{Entity}Exception);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Invalid{Entity}EventException invalid{Entity}EventException)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: invalid{Entity}EventException);
            }
            catch (Unauthorized{Entity}Exception unauthorized{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: unauthorized{Entity}Exception);
            }
            catch (Null{Entity}Exception null{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: null{Entity}Exception);
            }
            catch (Invalid{Entity}Exception invalid{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: invalid{Entity}Exception);
            }
            catch (NotFound{Entity}Exception notFound{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: notFound{Entity}Exception);
            }
            // already-categorized failures from nested service calls pass through as-is,
            // so one failure is not wrapped twice
            catch ({Entity}ValidationException)
            {
                throw;
            }
            catch ({Entity}DependencyValidationException)
            {
                throw;
            }
            catch ({Entity}DependencyException)
            {
                throw;
            }
            catch ({Entity}ServiceException)
            {
                throw;
            }
            catch (SqlException sqlException)
            {
                var failedStorage{Entity}Exception = new FailedStorage{Entity}Exception(
                    message: "Failed {entity display} storage error occurred, contact support.",
                    innerException: sqlException,
                    data: sqlException.Data);

                throw await CreateAndLogCriticalDependencyExceptionAsync(
                    exception: failedStorage{Entity}Exception);
            }
            catch (DuplicateKeyException duplicateKeyException)
            {
                var alreadyExists{Entity}Exception = new AlreadyExists{Entity}Exception(
                    message: "{Entity Display} already exists with the same Id.",
                    innerException: duplicateKeyException,
                    data: duplicateKeyException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(alreadyExists{Entity}Exception);
            }
            // A unique-INDEX violation (EF's HasIndex().IsUnique(), and the ProcessedEvents
            // dedup index) arrives as a type that does NOT derive from DuplicateKeyException,
            // so the clause above misses it; without this it falls through to the general
            // handler and mis-reports a business-key collision as "our code is broken".
            catch (DuplicateKeyWithUniqueIndexException duplicateKeyWithUniqueIndexException)
            {
                var alreadyExists{Entity}Exception = new AlreadyExists{Entity}Exception(
                    message: "{Entity Display} already exists, "
                        + "a uniqueness rule rejected the write.",
                    innerException: duplicateKeyWithUniqueIndexException,
                    data: duplicateKeyWithUniqueIndexException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(
                    alreadyExists{Entity}Exception);
            }
            catch (ForeignKeyConstraintConflictException foreignKeyConstraintConflictException)
            {
                var invalid{Entity}ReferenceException = new Invalid{Entity}ReferenceException(
                    message: "Invalid {entity display} reference error occurred.",
                    innerException: foreignKeyConstraintConflictException,
                    data: foreignKeyConstraintConflictException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(invalid{Entity}ReferenceException);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyException)
            {
                var locked{Entity}Exception = new Locked{Entity}Exception(
                    message: "Locked {entity display} record, please try again later.",
                    innerException: dbUpdateConcurrencyException,
                    data: dbUpdateConcurrencyException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(locked{Entity}Exception);
            }
            catch (DbUpdateException dbUpdateException)
            {
                var failedStorage{Entity}Exception = new FailedStorage{Entity}Exception(
                    message: "Failed {entity display} storage error occurred, contact support.",
                    innerException: dbUpdateException,
                    data: dbUpdateException.Data);

                throw await CreateAndLogDependencyExceptionAsync(failedStorage{Entity}Exception);
            }
            catch (Exception exception)
            {
                var failed{Entity}ServiceException = new Failed{Entity}ServiceException(
                    message: "Failed {entity display} service error occurred, please contact support.",
                    innerException: exception,
                    data: exception.Data);

                throw await CreateAndLogServiceExceptionAsync(failed{Entity}ServiceException);
            }
        }

        private async ValueTask<{Entity}> TryCatch(
            Returning{Entity}Function returning{Entity}Function)
        {
            try
            {
                return await returning{Entity}Function();
            }
            catch (OperationCanceledException operationCanceledException)
                when (operationCanceledException.CancellationToken.IsCancellationRequested is false)
            {
                var timeoutException =
                    new TimeoutException("The dependency operation timed out.");

                var timeout{Entity}Exception =
                    new Timeout{Entity}Exception(
                        message: "Failed {entity display} timeout error occurred, contact support.",
                        innerException: timeoutException,
                        data: timeoutException.Data);

                throw await CreateAndLogTimeoutDependencyExceptionAsync(exception: timeout{Entity}Exception);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Unauthorized{Entity}Exception unauthorized{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: unauthorized{Entity}Exception);
            }
            catch (Null{Entity}Exception null{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: null{Entity}Exception);
            }
            catch (Invalid{Entity}Exception invalid{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: invalid{Entity}Exception);
            }
            catch (NotFound{Entity}Exception notFound{Entity}Exception)
            {
                throw await CreateAndLogValidationExceptionAsync(exception: notFound{Entity}Exception);
            }
            catch (SqlException sqlException)
            {
                var failedStorage{Entity}Exception = new FailedStorage{Entity}Exception(
                    message: "Failed {entity display} storage error occurred, contact support.",
                    innerException: sqlException,
                    data: sqlException.Data);

                throw await CreateAndLogCriticalDependencyExceptionAsync(
                    exception: failedStorage{Entity}Exception);
            }
            catch (DuplicateKeyException duplicateKeyException)
            {
                var alreadyExists{Entity}Exception = new AlreadyExists{Entity}Exception(
                    message: "{Entity Display} already exists with the same Id.",
                    innerException: duplicateKeyException,
                    data: duplicateKeyException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(alreadyExists{Entity}Exception);
            }
            catch (DuplicateKeyWithUniqueIndexException duplicateKeyWithUniqueIndexException)
            {
                var alreadyExists{Entity}Exception = new AlreadyExists{Entity}Exception(
                    message: "{Entity Display} already exists, "
                        + "a uniqueness rule rejected the write.",
                    innerException: duplicateKeyWithUniqueIndexException,
                    data: duplicateKeyWithUniqueIndexException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(
                    alreadyExists{Entity}Exception);
            }
            catch (ForeignKeyConstraintConflictException foreignKeyConstraintConflictException)
            {
                var invalid{Entity}ReferenceException = new Invalid{Entity}ReferenceException(
                    message: "Invalid {entity display} reference error occurred.",
                    innerException: foreignKeyConstraintConflictException,
                    data: foreignKeyConstraintConflictException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(invalid{Entity}ReferenceException);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyException)
            {
                var locked{Entity}Exception = new Locked{Entity}Exception(
                    message: "Locked {entity display} record, please try again later.",
                    innerException: dbUpdateConcurrencyException,
                    data: dbUpdateConcurrencyException.Data);

                throw await CreateAndLogDependencyValidationExceptionAsync(locked{Entity}Exception);
            }
            catch (DbUpdateException dbUpdateException)
            {
                var failedStorage{Entity}Exception = new FailedStorage{Entity}Exception(
                    message: "Failed {entity display} storage error occurred, contact support.",
                    innerException: dbUpdateException,
                    data: dbUpdateException.Data);

                throw await CreateAndLogDependencyExceptionAsync(failedStorage{Entity}Exception);
            }
            catch (Exception exception)
            {
                var failed{Entity}ServiceException = new Failed{Entity}ServiceException(
                    message: "Failed {entity display} service error occurred, please contact support.",
                    innerException: exception,
                    data: exception.Data);

                throw await CreateAndLogServiceExceptionAsync(failed{Entity}ServiceException);
            }
        }

        private async ValueTask<IQueryable<{Entity}>> TryCatch(
            Returning{Entity}sFunction returning{Entity}sFunction)
        {
            try
            {
                return await returning{Entity}sFunction();
            }
            catch (OperationCanceledException operationCanceledException)
                when (operationCanceledException.CancellationToken.IsCancellationRequested is false)
            {
                var timeoutException =
                    new TimeoutException("The dependency operation timed out.");

                var timeout{Entity}Exception =
                    new Timeout{Entity}Exception(
                        message: "Failed {entity display} timeout error occurred, contact support.",
                        innerException: timeoutException,
                        data: timeoutException.Data);

                throw await CreateAndLogTimeoutDependencyExceptionAsync(exception: timeout{Entity}Exception);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SqlException sqlException)
            {
                var failedStorage{Entity}Exception = new FailedStorage{Entity}Exception(
                    message: "Failed {entity display} storage error occurred, contact support.",
                    innerException: sqlException,
                    data: sqlException.Data);

                throw await CreateAndLogCriticalDependencyExceptionAsync(
                    exception: failedStorage{Entity}Exception);
            }
            catch (Exception exception)
            {
                var failed{Entity}ServiceException = new Failed{Entity}ServiceException(
                    message: "Failed {entity display} service error occurred, please contact support.",
                    innerException: exception,
                    data: exception.Data);

                throw await CreateAndLogServiceExceptionAsync(failed{Entity}ServiceException);
            }
        }

        private async ValueTask<{Entity}ValidationException>
            CreateAndLogValidationExceptionAsync(Xeption exception)
        {
            var {entity}ValidationException = new {Entity}ValidationException(
                message: "{Entity Display} validation error occurred, fix the errors and try again.",
                innerException: exception);

            await this.loggingBroker.LogErrorAsync({entity}ValidationException);

            return {entity}ValidationException;
        }

        private async ValueTask<{Entity}DependencyException>
            CreateAndLogDependencyExceptionAsync(Xeption exception)
        {
            var {entity}DependencyException = new {Entity}DependencyException(
                message: "{Entity Display} dependency error occurred, contact support.",
                innerException: exception);

            await this.loggingBroker.LogErrorAsync({entity}DependencyException);

            return {entity}DependencyException;
        }

        // Intentionally a named twin of CreateAndLogDependencyExceptionAsync (same wrapper,
        // same LogError): timeouts categorize as a non-critical dependency failure, but keep
        // their own seam so the call site reads as a timeout and the behavior can diverge
        // later without touching generic dependency handling.
        private async ValueTask<{Entity}DependencyException>
            CreateAndLogTimeoutDependencyExceptionAsync(Xeption exception)
        {
            var {entity}DependencyException =
                new {Entity}DependencyException(
                    message: "{Entity Display} dependency error occurred, contact support.",
                    innerException: exception);

            await this.loggingBroker.LogErrorAsync({entity}DependencyException);

            return {entity}DependencyException;
        }

        private async ValueTask<{Entity}DependencyException>
            CreateAndLogCriticalDependencyExceptionAsync(Xeption exception)
        {
            var {entity}DependencyException = new {Entity}DependencyException(
                message: "{Entity Display} dependency error occurred, contact support.",
                innerException: exception);

            await this.loggingBroker.LogCriticalAsync({entity}DependencyException);

            return {entity}DependencyException;
        }

        private async ValueTask<{Entity}DependencyValidationException>
            CreateAndLogDependencyValidationExceptionAsync(Xeption exception)
        {
            var {entity}DependencyValidationException = new {Entity}DependencyValidationException(
                message: "{Entity Display} dependency validation error occurred, fix the errors and try again.",
                innerException: exception);

            await this.loggingBroker.LogErrorAsync({entity}DependencyValidationException);

            return {entity}DependencyValidationException;
        }

        private async ValueTask<{Entity}ServiceException> CreateAndLogServiceExceptionAsync(Xeption exception)
        {
            var {entity}ServiceException = new {Entity}ServiceException(
                message: "{Entity Display} service error occurred, contact support.",
                innerException: exception);

            await this.loggingBroker.LogErrorAsync({entity}ServiceException);

            return {entity}ServiceException;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 9: EXCEPTION MODELS
// ═══════════════════════════════════════════════════════════════════════════════
//
// Fifteen files under Models/Foundations/{Entity}s/Exceptions/. The INNER exceptions
// (the ones the service throws and catches) are public; the four OUTER wrappers the
// caller sees are internal.
//
// Note the naming: Null{Entity}Exception, NOT Null{Entity}ServiceException. Only the
// four outer wrappers and Failed{Entity}ServiceException carry "Service" in the name.

// Null{Entity}Exception.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Null{Entity}Exception : Xeption
    {
        public Null{Entity}Exception(string message)
            : base(message)
        { }
    }
}

// Invalid{Entity}Exception.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Invalid{Entity}Exception : Xeption
    {
        public Invalid{Entity}Exception(string message)
            : base(message)
        { }
    }
}

// NotFound{Entity}Exception.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class NotFound{Entity}Exception : Xeption
    {
        public NotFound{Entity}Exception(string message)
            : base(message)
        { }
    }
}

// Unauthorized{Entity}Exception.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Unauthorized{Entity}Exception : Xeption
    {
        public Unauthorized{Entity}Exception(string message)
            : base(message)
        { }
    }
}

// Invalid{Entity}EventException.cs — the event path's own guard: a malformed envelope
// or a failed integrity signature
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Invalid{Entity}EventException : Xeption
    {
        public Invalid{Entity}EventException(string message)
            : base(message)
        { }
    }
}

// AlreadyExists{Entity}Exception.cs
using System;
using System.Collections;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class AlreadyExists{Entity}Exception : Xeption
    {
        public AlreadyExists{Entity}Exception(string message, Exception innerException, IDictionary data)
            : base(message, innerException, data)
        { }
    }
}

// Invalid{Entity}ReferenceException.cs
using System;
using System.Collections;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Invalid{Entity}ReferenceException : Xeption
    {
        public Invalid{Entity}ReferenceException(string message, Exception innerException, IDictionary data)
            : base(message, innerException, data)
        { }
    }
}

// Locked{Entity}Exception.cs
using System;
using System.Collections;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Locked{Entity}Exception : Xeption
    {
        public Locked{Entity}Exception(string message, Exception innerException, IDictionary data)
            : base(message, innerException, data)
        { }
    }
}

// FailedStorage{Entity}Exception.cs
using System;
using System.Collections;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class FailedStorage{Entity}Exception : Xeption
    {
        public FailedStorage{Entity}Exception(string message, Exception innerException, IDictionary data)
            : base(message, innerException, data)
        { }
    }
}

// Failed{Entity}ServiceException.cs
using System;
using System.Collections;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Failed{Entity}ServiceException : Xeption
    {
        public Failed{Entity}ServiceException(string message, Exception innerException, IDictionary data)
            : base(message, innerException, data)
        { }
    }
}

// Timeout{Entity}Exception.cs
using System;
using System.Collections;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    public class Timeout{Entity}Exception : Xeption
    {
        public Timeout{Entity}Exception(string message, Exception innerException, IDictionary data)
            : base(message, innerException, data)
        { }
    }
}

// ── the four outer wrappers the caller sees ──────────────────────────────────

// {Entity}ValidationException.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    internal class {Entity}ValidationException : Xeption
    {
        public {Entity}ValidationException(string message, Xeption innerException)
            : base(message, innerException)
        { }
    }
}

// {Entity}DependencyValidationException.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    internal class {Entity}DependencyValidationException : Xeption
    {
        public {Entity}DependencyValidationException(string message, Xeption innerException)
            : base(message, innerException)
        { }
    }
}

// {Entity}DependencyException.cs
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    internal class {Entity}DependencyException : Xeption
    {
        public {Entity}DependencyException(string message, Xeption innerException)
            : base(message, innerException)
        { }
    }
}

// {Entity}ServiceException.cs
using System;
using Xeptions;

namespace {Namespace}.Models.Foundations.{Entity}s.Exceptions
{
    internal class {Entity}ServiceException : Xeption
    {
        public {Entity}ServiceException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 10: UNIT TESTS
// ═══════════════════════════════════════════════════════════════════════════════

// {Entity}ServiceTests.cs — test fixture base
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EFxceptions.Models.Exceptions;
using {Namespace}.Brokers.DateTimes;
using {Namespace}.Brokers.EventEnvelopes;
using {Namespace}.Brokers.Events;
using {Namespace}.Brokers.Identifiers;
using {Namespace}.Brokers.Integrities;
using {Namespace}.Brokers.Loggings;
using {Namespace}.Brokers.Securities;
using {Namespace}.Brokers.Storages.Sql;
using {Namespace}.Models.Events;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.{Entity}s.Exceptions;
using {Namespace}.Models.Securities;
using {Namespace}.Services.Foundations.{Entity}s;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tynamix.ObjectFiller;
using Xeptions;

namespace {Namespace}.Tests.Unit.Services.Foundations.{Entity}s
{
    public partial class {Entity}ServiceTests
    {
        private readonly Mock<IStorageBroker> storageBrokerMock;
        private readonly Mock<IDateTimeBroker> dateTimeBrokerMock;
        private readonly Mock<IIdentifierBroker> identifierBrokerMock;
        private readonly Mock<IEventBroker> eventBrokerMock;
        private readonly Mock<IEventEnvelopeBroker> eventEnvelopeBrokerMock;
        private readonly Mock<ISecurityAuditBroker> securityAuditBrokerMock;
        private readonly Mock<IEnvelopeIntegrityBroker> envelopeIntegrityBrokerMock;
        private readonly Mock<ILoggingBroker> loggingBrokerMock;
        private readonly I{Entity}Service {entity}Service;
        private SecurityContext ambientSecurityContext;

        public {Entity}ServiceTests()
        {
            this.storageBrokerMock = new Mock<IStorageBroker>();
            this.dateTimeBrokerMock = new Mock<IDateTimeBroker>();
            this.identifierBrokerMock = new Mock<IIdentifierBroker>();
            this.eventBrokerMock = new Mock<IEventBroker>();
            this.eventEnvelopeBrokerMock = new Mock<IEventEnvelopeBroker>();
            this.securityAuditBrokerMock = new Mock<ISecurityAuditBroker>();
            this.envelopeIntegrityBrokerMock = new Mock<IEnvelopeIntegrityBroker>();
            this.loggingBrokerMock = new Mock<ILoggingBroker>();

            // the ambient caller the envelope broker captures on the direct path — tests
            // override this field (before acting) to run as a different caller
            this.ambientSecurityContext = CreateAuthenticatedSecurityContext();

            this.eventEnvelopeBrokerMock.Setup(broker =>
                broker.CreateAsync(It.IsAny<{Entity}>()))
                    .Returns(({Entity} content) =>
                        new ValueTask<EventEnvelope<{Entity}>>(
                            new EventEnvelope<{Entity}>
                            {
                                Content = content,
                                SecurityContext = this.ambientSecurityContext,
                                Metadata = new EventMetadata { EventId = Guid.NewGuid() }
                            }));

            this.eventEnvelopeBrokerMock.Setup(broker =>
                broker.CreateNextAsync(
                    It.IsAny<EventEnvelope<{Entity}>>(),
                    It.IsAny<{Entity}>()))
                        .Returns((EventEnvelope<{Entity}> sourceEnvelope, {Entity} content) =>
                            new ValueTask<EventEnvelope<{Entity}>>(
                                new EventEnvelope<{Entity}>
                                {
                                    Content = content,
                                    SecurityContext = sourceEnvelope.SecurityContext,
                                    Metadata = new EventMetadata { EventId = Guid.NewGuid() }
                                }));

            // signature verification passes by default — the tests that care about a
            // TAMPERED envelope override this to false and assert the refusal
            this.envelopeIntegrityBrokerMock.Setup(broker =>
                broker.VerifyAsync(
                    It.IsAny<EventEnvelope<{Entity}>>(),
                    It.IsAny<string>(),
                    It.IsAny<EnvelopeDirection>()))
                        .ReturnsAsync(true);

            this.{entity}Service = new {Entity}Service(
                storageBroker: this.storageBrokerMock.Object,
                dateTimeBroker: this.dateTimeBrokerMock.Object,
                identifierBroker: this.identifierBrokerMock.Object,
                eventBroker: this.eventBrokerMock.Object,
                eventEnvelopeBroker: this.eventEnvelopeBrokerMock.Object,
                securityAuditBroker: this.securityAuditBrokerMock.Object,
                envelopeIntegrityBroker: this.envelopeIntegrityBrokerMock.Object,
                loggingBroker: this.loggingBrokerMock.Object);
        }

        private static Expression<Func<Xeption, bool>> SameExceptionAs(Xeption expectedException) =>
            actualException => actualException.SameExceptionAs(expectedException);

        private static SqlException GetSqlException() =>
            (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));

        private static string GetRandomString() =>
            new MnemonicString(wordCount: GetRandomNumber()).GetValue();

        private static string GetRandomStringWithLengthOf(int length)
        {
            string result = new MnemonicString(wordCount: 1, wordMinLength: length, wordMaxLength: length).GetValue();

            return result.Length > length ? result.Substring(0, length) : result;
        }

        private static int GetRandomNumber() =>
            new IntRange(min: 2, max: 10).GetValue();

        private static int GetRandomNegativeNumber() =>
            -1 * new IntRange(min: 2, max: 10).GetValue();

        private static DateTimeOffset GetRandomDateTimeOffset() =>
            new DateTimeRange(earliestDate: new DateTime()).GetValue();

        public static TheoryData<int> MinutesBeforeOrAfter()
        {
            int randomTimeInFuture = GetRandomNumber();
            int randomTimeInPast = GetRandomNegativeNumber();

            return new TheoryData<int>
            {
                randomTimeInFuture,
                randomTimeInPast
            };
        }

        // every security gate gets both shapes of "not signed in": no context at all
        // (the event path) and an unauthenticated one (the direct path)
        public static TheoryData<SecurityContext> UnauthenticatedSecurityContexts() =>
            new TheoryData<SecurityContext>
            {
                null,
                new SecurityContext { IsAuthenticated = false }
            };

        public static TheoryData<string[]> NonAdminRoleSets() =>
            new TheoryData<string[]>
            {
                new string[0],
                new[] { Roles.Reviewers }
            };

        public static TheoryData<Exception, Xeption> DependencyExceptions()
        {
            var operationCanceledException = new OperationCanceledException();
            var timeoutException = new TimeoutException("The dependency operation timed out.");
            var dbUpdateException = new DbUpdateException();

            return new TheoryData<Exception, Xeption>
            {
                {
                    operationCanceledException,
                    new Timeout{Entity}Exception(
                        message: "Failed {entity display} timeout error occurred, contact support.",
                        innerException: timeoutException,
                        data: timeoutException.Data)
                },
                {
                    dbUpdateException,
                    new FailedStorage{Entity}Exception(
                        message: "Failed {entity display} storage error occurred, contact support.",
                        innerException: dbUpdateException,
                        data: dbUpdateException.Data)
                }
            };
        }

        public static TheoryData<Exception, Xeption> DependencyValidationExceptions()
        {
            string someMessage = GetRandomString();
            var duplicateKeyException = new DuplicateKeyException(someMessage);
            var foreignKeyConstraintConflictException = new ForeignKeyConstraintConflictException(someMessage);

            var duplicateKeyWithUniqueIndexException =
                new DuplicateKeyWithUniqueIndexException(someMessage);

            return new TheoryData<Exception, Xeption>
            {
                {
                    duplicateKeyException,
                    new AlreadyExists{Entity}Exception(
                        message: "{Entity Display} already exists with the same Id.",
                        innerException: duplicateKeyException,
                        data: duplicateKeyException.Data)
                },
                {
                    foreignKeyConstraintConflictException,
                    new Invalid{Entity}ReferenceException(
                        message: "Invalid {entity display} reference error occurred.",
                        innerException: foreignKeyConstraintConflictException,
                        data: foreignKeyConstraintConflictException.Data)
                },
                {
                    duplicateKeyWithUniqueIndexException,
                    new AlreadyExists{Entity}Exception(
                        message: "{Entity Display} already exists, " +
                            "a uniqueness rule rejected the write.",
                        innerException: duplicateKeyWithUniqueIndexException,
                        data: duplicateKeyWithUniqueIndexException.Data)
                }
            };
        }

        private static {Entity} CreateRandom{Entity}() =>
            Create{Entity}Filler(dateTimeOffset: GetRandomDateTimeOffset()).Create();

        private static EventEnvelope<{Entity}> CreateRandom{Entity}RequestEnvelope(
            SecurityContext? securityContext = null) =>
            new EventEnvelope<{Entity}>
            {
                Content = new {Entity} { Id = Guid.NewGuid() },
                SecurityContext = securityContext ?? CreateAuthenticatedSecurityContext(Roles.Administrators),
                Metadata = new EventMetadata { EventId = Guid.NewGuid() }
            };

        private static SecurityContext CreateAuthenticatedSecurityContext(params string[] roles) =>
            new SecurityContext
            {
                IsAuthenticated = true,
                Roles = roles
            };

        private static {Entity} CreateRandomModify{Entity}(
            DateTimeOffset dateTimeOffset,
            string userId = "")
        {
            int randomDaysInPast = GetRandomNegativeNumber();
            {Entity} random{Entity} = Create{Entity}Filler(dateTimeOffset, userId).Create();
            random{Entity}.CreatedWhen = random{Entity}.CreatedWhen.AddDays(randomDaysInPast);

            return random{Entity};
        }

        private static IQueryable<{Entity}> CreateRandom{Entity}s()
        {
            return Create{Entity}Filler(dateTimeOffset: GetRandomDateTimeOffset())
                .Create(count: GetRandomNumber())
                .AsQueryable();
        }

        private static Filler<{Entity}> Create{Entity}Filler(
            DateTimeOffset dateTimeOffset,
            string userId = "")
        {
            userId = string.IsNullOrEmpty(userId) ? Guid.NewGuid().ToString() : userId;
            var filler = new Filler<{Entity}>();

            filler.Setup()
                .OnType<DateTimeOffset>().Use(dateTimeOffset)
                .OnType<DateTimeOffset?>().Use(dateTimeOffset)
                // IsDeleted gates every read and remove path, so it is pinned here rather
                // than drawn: a posture-sensitive test must never depend on the draw. Tests
                // that want a soft-deleted row set it explicitly.
                .OnProperty({entity} => {entity}.IsDeleted).Use(false)
                .OnProperty({entity} => {entity}.CreatedBy).Use(userId)
                .OnProperty({entity} => {entity}.UpdatedBy).Use(userId);

            return filler;
        }
    }
}

// {Entity}ServiceTests.Add.Logic.cs — the non-event path
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Force.DeepCloner;
using {Namespace}.Models.Configurations;
using {Namespace}.Models.Events;
using {Namespace}.Models.Events.Foundations;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.ProcessedEvents;
using {Namespace}.Models.Securities;
using Moq;

namespace {Namespace}.Tests.Unit.Services.Foundations.{Entity}s
{
    public partial class {Entity}ServiceTests
    {
        [Fact]
        public async Task ShouldAdd{Entity}Async()
        {
            // given
            this.ambientSecurityContext = CreateAuthenticatedSecurityContext(Roles.Administrators);
            DateTimeOffset randomDateTimeOffset = GetRandomDateTimeOffset();
            {Entity} random{Entity} = Create{Entity}Filler(randomDateTimeOffset).Create();
            {Entity} input{Entity} = random{Entity};
            {Entity} auditApplied{Entity} = input{Entity}.DeepClone();
            {Entity} storage{Entity} = auditApplied{Entity}.DeepClone();
            {Entity} expected{Entity} = storage{Entity}.DeepClone();

            // It.IsAny<SecurityContext>() on the audit setups, not the ambient overload —
            // the service passes the envelope's context, and there IS no other overload
            this.securityAuditBrokerMock.Setup(broker =>
                broker.ApplyAddAuditValuesAsync(input{Entity}, It.IsAny<SecurityContext>()))
                    .ReturnsAsync(auditApplied{Entity});

            this.securityAuditBrokerMock.Setup(broker =>
                broker.GetUserIdAsync(It.IsAny<SecurityContext>()))
                    .ReturnsAsync(auditApplied{Entity}.CreatedBy);

            this.dateTimeBrokerMock.Setup(broker =>
                broker.GetCurrentDateTimeOffsetAsync())
                    .ReturnsAsync(randomDateTimeOffset);

            this.storageBrokerMock.Setup(broker =>
                broker.Insert{Entity}Async(auditApplied{Entity}, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(storage{Entity});

            this.eventBrokerMock.Setup(broker =>
                broker.Publish{Entity}Async(
                    It.IsAny<EventEnvelope<{Entity}>>(),
                    {Entity}EventOperation.Added))
                    .Returns(new ValueTask<EventPublishResult<{Entity}>>(
                        new EventPublishResult<{Entity}>()));

            // when
            {Entity} actual{Entity} =
                await this.{entity}Service.Add{Entity}Async(
                    input{Entity},
                    TestContext.Current.CancellationToken);

            // then
            actual{Entity}.Should().BeEquivalentTo(expected{Entity});

            this.securityAuditBrokerMock.Verify(broker =>
                    broker.ApplyAddAuditValuesAsync(input{Entity}, It.IsAny<SecurityContext>()),
                Times.Once);

            this.securityAuditBrokerMock.Verify(broker =>
                    broker.GetUserIdAsync(It.IsAny<SecurityContext>()),
                Times.Once);

            this.storageBrokerMock.Verify(broker =>
                    broker.Insert{Entity}Async(auditApplied{Entity}, It.IsAny<CancellationToken>()),
                Times.Once);

            this.eventBrokerMock.Verify(broker =>
                broker.Publish{Entity}Async(
                    It.IsAny<EventEnvelope<{Entity}>>(),
                    {Entity}EventOperation.Added),
                Times.Once);

            // Three: once from IsNotRecentAsync in the add validation, and once inside each
            // of the two RecordEventProcessedAsync calls. VerifyNoOtherCalls() below counts
            // an invocation as accounted for only when a Verify() matched it — a Setup()
            // does not — so dropping this line makes the test fail rather than relax.
            // Recount it if the entity's validations read the clock a different number of times.
            this.dateTimeBrokerMock.Verify(broker =>
                    broker.GetCurrentDateTimeOffsetAsync(),
                Times.Exactly(3));

            // twice — once for the inbound request id, once for the outbound fact id
            this.storageBrokerMock.Verify(broker =>
                broker.InsertProcessedEventAsync(
                    It.Is<ProcessedEvent>(processedEvent =>
                        processedEvent.ReceiverName ==
                            EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            this.securityAuditBrokerMock.VerifyNoOtherCalls();
            this.dateTimeBrokerMock.VerifyNoOtherCalls();
            this.storageBrokerMock.VerifyNoOtherCalls();
            this.eventBrokerMock.VerifyNoOtherCalls();
            this.loggingBrokerMock.VerifyNoOtherCalls();
        }
    }
}

// {Entity}ServiceTests.OnAdding{Entity}.Logic.cs — the event path
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Force.DeepCloner;
using {Namespace}.Models.Configurations;
using {Namespace}.Models.Events;
using {Namespace}.Models.Events.Foundations;
using {Namespace}.Models.Foundations.{Entity}s;
using {Namespace}.Models.Foundations.ProcessedEvents;
using {Namespace}.Models.Securities;
using Moq;

namespace {Namespace}.Tests.Unit.Services.Foundations.{Entity}s
{
    public partial class {Entity}ServiceTests
    {
        [Fact]
        public async Task ShouldAdd{Entity}AndReplyOnAdding{Entity}EventAsync()
        {
            // given
            DateTimeOffset randomDateTimeOffset = GetRandomDateTimeOffset();
            {Entity} random{Entity} = Create{Entity}Filler(randomDateTimeOffset).Create();
            {Entity} input{Entity} = random{Entity};
            {Entity} auditApplied{Entity} = input{Entity}.DeepClone();
            {Entity} storage{Entity} = auditApplied{Entity}.DeepClone();
            {Entity} expected{Entity} = storage{Entity}.DeepClone();

            // the request envelope carries its OWN caller — note it is not the ambient
            // one; that is the whole point of the event path
            var requestEnvelope = new EventEnvelope<{Entity}>
            {
                Content = input{Entity},
                SecurityContext = CreateAuthenticatedSecurityContext(Roles.Administrators),
                Metadata = new EventMetadata { EventId = Guid.NewGuid() }
            };

            this.storageBrokerMock.Setup(broker =>
                broker.SelectProcessedEventExistsAsync(
                    requestEnvelope.Metadata.EventId,
                    EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName,
                    TestContext.Current.CancellationToken))
                        .ReturnsAsync(false);

            this.securityAuditBrokerMock.Setup(broker =>
                broker.ApplyAddAuditValuesAsync(input{Entity}, It.IsAny<SecurityContext>()))
                    .ReturnsAsync(auditApplied{Entity});

            this.securityAuditBrokerMock.Setup(broker =>
                broker.GetUserIdAsync(It.IsAny<SecurityContext>()))
                    .ReturnsAsync(auditApplied{Entity}.CreatedBy);

            this.dateTimeBrokerMock.Setup(broker =>
                broker.GetCurrentDateTimeOffsetAsync())
                    .ReturnsAsync(randomDateTimeOffset);

            this.storageBrokerMock.Setup(broker =>
                broker.Insert{Entity}Async(auditApplied{Entity}, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(storage{Entity});

            this.identifierBrokerMock.Setup(broker =>
                broker.GetIdentifierAsync())
                    .ReturnsAsync(Guid.NewGuid());

            this.eventBrokerMock.Setup(broker =>
                broker.Publish{Entity}Async(
                    It.IsAny<EventEnvelope<{Entity}>>(),
                    {Entity}EventOperation.Added))
                    .Returns(new ValueTask<EventPublishResult<{Entity}>>(
                        new EventPublishResult<{Entity}>()));

            // when
            EventEnvelope<{Entity}>? actualReplyEnvelope =
                await this.{entity}Service.OnAdding{Entity}Async(
                    requestEnvelope,
                    TestContext.Current.CancellationToken);

            // then
            actualReplyEnvelope.Should().NotBeNull();
            actualReplyEnvelope!.Content.Should().BeEquivalentTo(expected{Entity});

            this.storageBrokerMock.Verify(broker =>
                broker.SelectProcessedEventExistsAsync(
                    requestEnvelope.Metadata.EventId,
                    EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName,
                    TestContext.Current.CancellationToken),
                Times.Once);

            this.storageBrokerMock.Verify(broker =>
                broker.Insert{Entity}Async(auditApplied{Entity}, It.IsAny<CancellationToken>()),
                Times.Once);

            this.eventBrokerMock.Verify(broker =>
                broker.Publish{Entity}Async(
                    It.IsAny<EventEnvelope<{Entity}>>(),
                    {Entity}EventOperation.Added),
                Times.Once);

            // the INBOUND record, pinned to the request's own event id
            this.storageBrokerMock.Verify(broker =>
                broker.InsertProcessedEventAsync(
                    It.Is<ProcessedEvent>(processedEvent =>
                        processedEvent.EventId == requestEnvelope.Metadata.EventId
                            && processedEvent.ReceiverName ==
                                EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName),
                    TestContext.Current.CancellationToken),
                Times.Once);

            // Both records, matched on receiver name only. The outbound one carries a
            // DIFFERENT event id — CreateNextAsync mints a fresh one — so the id-filtered
            // verify above cannot account for it, and storageBrokerMock.VerifyNoOtherCalls()
            // would throw without this second, broader verify.
            this.storageBrokerMock.Verify(broker =>
                broker.InsertProcessedEventAsync(
                    It.Is<ProcessedEvent>(processedEvent =>
                        processedEvent.ReceiverName ==
                            EventBrokerIdentifiers.{Entity}OnAdding{Entity}SubscriptionName),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            // Only these three. securityAuditBrokerMock and dateTimeBrokerMock are
            // deliberately NOT asserted here: the shared do-work calls both, and this test
            // is about the handler's own behaviour — the dedup check, the reply envelope and
            // the published fact. Their calls are pinned by the direct-path Add test above.
            // Adding VerifyNoOtherCalls() for them here without matching Verify() calls
            // makes this test fail.
            this.storageBrokerMock.VerifyNoOtherCalls();
            this.eventBrokerMock.VerifyNoOtherCalls();
            this.loggingBrokerMock.VerifyNoOtherCalls();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// REMAINING TEST FILES — same pattern, one file per operation × concern.
//
// non-event path:
//   {Entity}ServiceTests.Add.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.RetrieveAll.{Logic,Exceptions}.cs
//   {Entity}ServiceTests.RetrieveById.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.Modify.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.RemoveById.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.HardRemoveById.{Logic,Validations,Exceptions}.cs
//
// event path — one trio per handler:
//   {Entity}ServiceTests.OnAdding{Entity}.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.OnModifying{Entity}.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.OnRemoving{Entity}ById.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.OnHardRemoving{Entity}ById.{Logic,Validations,Exceptions}.cs
//   {Entity}ServiceTests.OnRetrieving{Entity}ById.{Logic,Validations,Exceptions}.cs
//
// Every event-path Validations file must cover, at minimum:
//   • a null / contentless / metadata-less envelope is refused
//   • VerifyAsync returning false is refused (the TAMPERED envelope) — this is the
//     test that proves identity is not simply believed off the wire
//   • the security gate refuses each unauthorized caller shape, driven from the
//     UnauthenticatedSecurityContexts / NonAdminRoleSets theory data
//   • a request whose event id is already in ProcessedEvents replies null and writes
//     nothing (the dedup path)
//
// Unit tests mock the layer below, so a foundation validation tightened here will
// not fail any caller's suite. When a rule changes, check the callers by hand or
// add acceptance coverage — the green suite does not prove them.
// ─────────────────────────────────────────────────────────────────────────────
