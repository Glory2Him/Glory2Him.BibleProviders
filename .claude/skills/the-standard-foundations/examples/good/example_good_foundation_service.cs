// ---
// skill: the-standard-foundations
// type: example
// source-section: "2. Foundation Services"
// demonstrates: "ts-foundations-001, ts-foundations-002, ts-foundations-005, ts-foundations-006, ts-foundations-007, ts-foundations-008, ts-foundations-009, ts-foundations-010, ts-foundations-011, ts-foundations-012, ts-foundations-013, ts-foundations-014, ts-foundations-015"
// ---

// ────────────────────────────────────────────────────────────────────────────────
// One vertical slice — Add — through both entry paths. The full shape is in
// templates/Template_FoundationService.cs.
// ────────────────────────────────────────────────────────────────────────────────

// ✅ ts-foundations-008: interface named I{Entity}Service, and partial so the
//    event-facing surface lives in its own file
internal partial interface IStudentService
{
    ValueTask<Student> AddStudentAsync(Student student, CancellationToken cancellationToken = default);
}

// IStudentService.Substrate.cs — the event-facing half
internal partial interface IStudentService
{
    ValueTask<EventEnvelope<Student>?> OnAddingStudentAsync(
        EventEnvelope<Student> envelope,
        CancellationToken cancellationToken = default);
}

// ✅ ts-foundations-001 + ts-foundations-007: one entity, partial class split by concern
internal partial class StudentService : IStudentService
{
    private readonly IStorageBroker storageBroker;
    private readonly IDateTimeBroker dateTimeBroker;
    private readonly IIdentifierBroker identifierBroker;
    private readonly IEventBroker eventBroker;
    private readonly IEventEnvelopeBroker eventEnvelopeBroker;
    private readonly ISecurityAuditBroker securityAuditBroker;
    private readonly IEnvelopeIntegrityBroker envelopeIntegrityBroker;
    private readonly ILoggingBroker loggingBroker;

    // ── the non-event path ────────────────────────────────────────────────────
    // ✅ ts-foundations-013: no logic here. Guard the token, mint the envelope
    //    that captures the ambient caller, hand off to the shared do-work.
    public ValueTask<Student> AddStudentAsync(
        Student student,
        CancellationToken cancellationToken = default) =>
        TryCatch(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStudentIsNotNull(student);

            EventEnvelope<Student> envelope =
                await this.eventEnvelopeBroker.CreateAsync(content: student);

            return await DoAddStudentAsync(
                student: student,
                inboundEnvelope: envelope,
                cancellationToken: cancellationToken);
        });

    // ── the shared do-work: reached from BOTH paths ───────────────────────────
    private async ValueTask<Student> DoAddStudentAsync(
        Student student,
        EventEnvelope<Student> inboundEnvelope,
        CancellationToken cancellationToken)
    {
        // ✅ ts-foundations-010: the foundation gates the caller itself
        ValidateUserIsAllowedToWriteStudent(inboundEnvelope.SecurityContext);

        // ✅ ts-foundations-009: identity comes off the ENVELOPE, passed explicitly.
        //    There is no ambient overload to reach for — it was deleted.
        student = await this.securityAuditBroker
            .ApplyAddAuditValuesAsync(entity: student, securityContext: inboundEnvelope.SecurityContext);

        // ✅ ts-foundations-002: structural validation, after the audit stamp so the
        //    stamped values are what the rules check
        await ValidateOnAddStudentAsync(
            student: student,
            securityContext: inboundEnvelope.SecurityContext);

        Student addedStudent =
            await this.storageBroker.InsertStudentAsync(student, cancellationToken);

        // ✅ ts-foundations-014: record the INBOUND request id...
        await RecordEventProcessedAsync(
            envelope: inboundEnvelope,
            receiverName: EventBrokerIdentifiers.StudentOnAddingStudentSubscriptionName,
            cancellationToken: cancellationToken);

        EventEnvelope<Student> outboundEnvelope =
            await this.eventEnvelopeBroker.CreateNextAsync(
                sourceEnvelope: inboundEnvelope,
                content: addedStudent);

        await this.eventBroker.PublishStudentAsync(
            envelope: outboundEnvelope,
            operation: StudentEventOperation.Added);

        // ...and the OUTBOUND fact id, so the fact this call published cannot loop
        // back in and be applied a second time
        await RecordEventProcessedAsync(
            envelope: outboundEnvelope,
            receiverName: EventBrokerIdentifiers.StudentOnAddingStudentSubscriptionName,
            cancellationToken: cancellationToken);

        return addedStudent;
    }
}

// StudentService.Substrate.cs — the event path
internal partial class StudentService
{
    public ValueTask<EventEnvelope<Student>?> OnAddingStudentAsync(
        EventEnvelope<Student> envelope,
        CancellationToken cancellationToken = default) =>
        // ✅ ts-foundations-015: TryCatchSubstrate categorizes then ALWAYS rethrows
        TryCatchSubstrate(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ✅ ts-foundations-011: verify the signature HERE, in the receiver
            await ValidateStudentEventEnvelopeAsync(envelope, StudentEventOperation.Adding);

            // ✅ ts-foundations-014: dedup before acting; a duplicate replies null
            bool alreadyProcessed = await AlreadyProcessedAsync(
                envelope: envelope,
                receiverName: EventBrokerIdentifiers.StudentOnAddingStudentSubscriptionName,
                cancellationToken: cancellationToken);

            if (alreadyProcessed)
                return null;

            // ✅ ts-foundations-013: the SAME do-work the direct path runs — the two
            //    paths cannot diverge, because there is only one of them
            Student addedStudent = await DoAddStudentAsync(
                student: envelope.Content,
                inboundEnvelope: envelope,
                cancellationToken: cancellationToken);

            return await this.eventEnvelopeBroker.CreateNextAsync(
                sourceEnvelope: envelope,
                content: addedStudent);
        });
}

// StudentService.Validations.cs
internal partial class StudentService
{
    // ✅ ts-foundations-010: authenticated → global ban → the permission itself.
    //    The ban precedes the role check so a banned administrator cannot reach past it.
    private static void ValidateUserIsAllowedToWriteStudent(SecurityContext securityContext)
    {
        if (securityContext is null || securityContext.IsAuthenticated is false)
            throw new UnauthorizedStudentException(message: "The current user is not authenticated.");

        if (securityContext.Roles.Contains(Roles.ReadOnly))
            throw new UnauthorizedStudentException(message: "The current user is blocked from writing students.");

        if (securityContext.Roles.Contains(Roles.Administrators) is false)
            throw new UnauthorizedStudentException(message: "The current user is not allowed to write students.");
    }

    private async ValueTask ValidateOnAddStudentAsync(Student student, SecurityContext securityContext)
    {
        ValidateStudentIsNotNull(student);

        // ✅ ts-foundations-009: the acting id is resolved from the envelope's context,
        //    so the CreatedBy this rule pins is the actor the signature verified
        string currentUserId = await this.securityAuditBroker.GetUserIdAsync(securityContext);

        Validate(
            message: "Student is invalid, fix the errors and try again.",
            (Rule: IsInvalid(student.Id), Parameter: nameof(Student.Id)),
            (Rule: IsInvalid(student.CreatedBy), Parameter: nameof(Student.CreatedBy)),

            (Rule: IsNotSame(first: currentUserId, second: student.CreatedBy),
                Parameter: nameof(Student.CreatedBy)),

            (Rule: await IsNotRecentAsync(student.CreatedWhen), Parameter: nameof(Student.CreatedWhen)));
    }

    // ✅ ts-foundations-011: null-check first (a malformed event), then verify the
    //    signature against this handler's event name and the request direction.
    //    Without it, whoever can reach the address states their own roles and is believed.
    private async ValueTask ValidateStudentEventEnvelopeAsync(
        EventEnvelope<Student> envelope,
        StudentEventOperation operation)
    {
        if (envelope is null || envelope.Content is null || envelope.Metadata is null)
        {
            throw new InvalidStudentEventException(
                message: "Invalid student event. The event envelope, its content and metadata are required.");
        }

        string eventName = $"{nameof(Student)}{operation}";

        bool isSignatureValid = await this.envelopeIntegrityBroker.VerifyAsync(
            envelope, eventName, EnvelopeDirection.Request);

        if (isSignatureValid is false)
        {
            throw new InvalidStudentEventException(
                message: "Invalid student event. Integrity verification failed.");
        }
    }
}

// StudentService.cs — the read posture
internal partial class StudentService
{
    // ✅ ts-foundations-012: a row the caller may not see answers NOT-FOUND. An
    //    authorization error would confirm it exists; the real reason is logged
    //    server-side only.
    private async ValueTask<Student> DoRetrieveStudentByIdAsync(
        Guid studentId,
        EventEnvelope<Student> inboundEnvelope,
        CancellationToken cancellationToken)
    {
        ValidateOnRetrieveStudentById(studentId);

        Student maybeStudent =
            await this.storageBroker.SelectStudentByIdAsync(studentId, cancellationToken);

        ValidateStorageStudent(maybeStudent, studentId);

        SecurityContext? securityContext = inboundEnvelope.SecurityContext;

        if (securityContext is null || securityContext.IsAuthenticated is false)
        {
            await this.loggingBroker.LogWarningAsync(
                message: $"Student read denied. Student {studentId} is visible to authenticated " +
                    "callers only and the caller is not authenticated; reported as not found.");

            throw new NotFoundStudentException(message: $"Student not found with id: {studentId}.");
        }

        return maybeStudent;
    }
}
