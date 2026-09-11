// ---
// skill: the-standard-foundations
// type: example
// source-section: "2. Foundation Services"
// demonstrates: "ts-foundations-001 violation, ts-foundations-003 violation, ts-foundations-004 violation, ts-foundations-006 violation, ts-foundations-009 violation, ts-foundations-010 violation, ts-foundations-011 violation, ts-foundations-012 violation, ts-foundations-013 violation, ts-foundations-014 violation"
// ---

// ❌ VIOLATION ts-foundations-001 + ts-foundations-003: calls another service AND manages two entities
internal class StudentService : IStudentService
{
    private readonly IStorageBroker storageBroker;
    private readonly ICourseService courseService; // ❌ foundation services depend only on brokers
    private readonly ISecurityAuditBroker securityAuditBroker;

    public async ValueTask<Student> AddStudentAsync(Student student)
    {
        // ❌ VIOLATION ts-foundations-004: business enrichment from another entity
        var defaultCourse = await this.courseService.RetrieveDefaultCourseAsync();
        student.CourseId = defaultCourse.Id;

        // ❌ VIOLATION ts-foundations-009: the AMBIENT overload. This reads a
        //    ClaimsPrincipal captured in the broker's CONSTRUCTOR, which is correct only
        //    while the instance was built inside the request it stamps for. On the event
        //    path the ambient principal is whoever published the triggering fact — not the
        //    actor the signature verified — so CreatedBy silently records the wrong person.
        //    Nothing fails. The generated code compiles and its tests pass.
        //    (These overloads no longer exist, so this is now a compile error.)
        student = await this.securityAuditBroker.ApplyAddAuditValuesAsync(student);
        string currentUserId = await this.securityAuditBroker.GetUserIdAsync();

        // ❌ VIOLATION ts-foundations-010: no security gate at all — assumes an
        //    orchestration above already checked. An exposer can bind here directly.

        // ❌ VIOLATION ts-foundations-006: no exception wrapping — raw broker exceptions leak
        return await this.storageBroker.InsertStudentAsync(student);
    }

    // ❌ VIOLATION ts-foundations-013: the event path re-implements the work instead of
    //    calling the same DoXAsync the direct path uses. The two will drift, and the
    //    security gate that exists on one path will be forgotten on the other.
    public async ValueTask<EventEnvelope<Student>?> OnAddingStudentAsync(
        EventEnvelope<Student> envelope)
    {
        // ❌ VIOLATION ts-foundations-011: the envelope's signature is never verified.
        //    Anyone who can put a message on this address declares their own SecurityContext
        //    — including Roles = [Administrators] — and is believed.

        // ❌ VIOLATION ts-foundations-014: no ProcessedEvents check, so a replayed or
        //    duplicated delivery inserts the student twice; and no record afterwards, so
        //    the fact this call publishes can loop back in and be applied again.
        Student added = await this.storageBroker.InsertStudentAsync(envelope.Content);

        return new EventEnvelope<Student> { Content = added };
    }

    public async ValueTask<Student> RetrieveStudentByIdAsync(Guid studentId)
    {
        Student maybeStudent = await this.storageBroker.SelectStudentByIdAsync(studentId);

        // ❌ VIOLATION ts-foundations-012: an authorization error CONFIRMS the row exists.
        //    A caller who may not see it must be told not-found, with the real reason
        //    logged server-side only.
        if (maybeStudent.OwnerId != currentUserId)
            throw new UnauthorizedStudentException("You are not allowed to view this student.");

        return maybeStudent;
    }
}
