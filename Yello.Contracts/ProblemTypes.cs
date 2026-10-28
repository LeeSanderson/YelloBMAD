namespace Yello.Contracts;

/// <summary>
/// The stable <c>type</c> values Yello's RFC 9457 problem responses carry. AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>The <c>type</c> is the contract; the <c>title</c> and <c>detail</c> beside it are not.</b>
/// A client decides what to do from the value here and renders wording from its own resources, so
/// changing the prose in a problem response can never change behaviour and a new locale needs no
/// server change.
/// </para>
/// <para>
/// <b>A URN rather than an <c>https://</c> URL.</b> RFC 9457 wants a URI, and it does not have to
/// dereference - but an <c>https://</c> type reads as a promise that a page exists there, and
/// Yello has no documentation domain. A URN is a URI, is stable, is obviously not a fetch target,
/// and claims no DNS name the project does not own.
/// </para>
/// <para>
/// In <c>Yello.Contracts</c> so the client matches on the same constant the Host emits, rather
/// than on a string literal copied into a component.
/// </para>
/// </remarks>
public static class ProblemTypes
{
    /// <summary>
    /// The registration submission was not well-formed. Carries an <c>errors</c> extension
    /// listing the failed rules by their stable codes.
    /// </summary>
    /// <remarks>
    /// <b>This never means "that address is taken".</b> A duplicate registration answers
    /// <c>204 No Content</c> exactly as a new one does (AD-23); the only thing this type reports
    /// is a malformed submission, which depends on what was sent and never on what is stored.
    /// </remarks>
    public static string InvalidRegistration => "urn:yello:problem:invalid-registration";

    /// <summary>
    /// The sign-in submission was not well-formed. Carries an <c>errors</c> extension listing the
    /// failed rules by their stable codes.
    /// </summary>
    /// <remarks>
    /// <b>This never means "those credentials are wrong".</b> It reports a submission that is not
    /// an authentication attempt at all - a missing field, or a value longer than any stored one
    /// could be - and every one of its codes depends only on what was sent. A wrong password and an
    /// unknown address both answer <see cref="AuthenticationFailed"/>, identically.
    /// </remarks>
    public static string InvalidSignIn => "urn:yello:problem:invalid-sign-in";

    /// <summary>
    /// Authentication did not succeed. <b>One type for every cause</b>, carried by both the
    /// unknown-address and the wrong-password paths, and by a request made with a Session that has
    /// expired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This constant is AD-23 on the wire, and its single-valued-ness is the contract.</b> AC4
    /// requires the two failure causes to be identical "in status, body, shape and duration", and
    /// the surest way to keep a client from branching on the cause is to send it nothing to branch
    /// on. There is deliberately no <c>errors</c> extension on this response and no second type
    /// beside it: a client that wanted to say "no account found" could not discover it here.
    /// </para>
    /// <para>
    /// The wording a person reads is one string for every cause - "Email or password is
    /// incorrect." (<c>EXPERIENCE.md:272</c>) - resolved from a resource on the client, keyed on
    /// this type.
    /// </para>
    /// </remarks>
    public static string AuthenticationFailed => "urn:yello:problem:authentication-failed";

    /// <summary>
    /// A state-changing request arrived without a valid anti-forgery token. AR-14 / AD-7.
    /// </summary>
    /// <remarks>
    /// <b>A <c>400</c>, not a <c>403</c>.</b> AD-3 draws the 403/404 line at the Space boundary
    /// "and nowhere else", and this request never reached one - it is malformed in the sense that
    /// matters, being a request the caller did not assemble correctly. Nothing is recorded in the
    /// audit store for it either: AD-20 scopes <c>AccessRefusal</c> to "every 403 and every
    /// Space-boundary 404", with exactly two kinds, and this is neither.
    /// </remarks>
    public static string AntiforgeryTokenMissing => "urn:yello:problem:antiforgery-token-missing";

    /// <summary>
    /// What was asked for is not available to this caller. <b>One type for every <c>404</c> the
    /// product answers</b>: a Space that does not exist, a Space the caller holds no Membership in,
    /// and an <c>/api</c> path that matches no route. AD-3; FR-15.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A <c>404</c>, never a <c>403</c>, and never distinguished.</b> AD-1
    /// (<c>ARCHITECTURE-SPINE.md:75</c>): "A request whose Space context cannot be resolved - no
    /// such Space, or no Membership in it - is refused with 404 (AD-3), never defaulted and never
    /// distinguished." Answering differently for the two would tell a caller which Space ids exist.
    /// The bodies are byte-identical, and the two causes take indistinguishable time.
    /// </para>
    /// <para>
    /// <b>Not "space not found", deliberately.</b> Story 1.5 answered with a Space-specific type.
    /// Story 1.6 made it the one not-found type for everything, so that a future not-found inside
    /// the caller's own Space - a Task id that matches nothing - cannot differ in body from a
    /// boundary refusal (AC1's "an identifier matching no resource at all").
    /// </para>
    /// <para>
    /// The client renders "Not available." from its own resources, the same page for every cause.
    /// </para>
    /// </remarks>
    public static string NotAvailable => "urn:yello:problem:not-available";

    /// <summary>
    /// The caller is a member of the Space, and its Role does not permit what the endpoint does.
    /// Carries a <c>capability</c> extension: the stable code of the capability refused. FR-16;
    /// AD-3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Capability-shaped, never object-shaped.</b> The client renders "Viewers cannot edit
    /// Tasks." from a resource keyed on the capability code, never "you do not have permission to
    /// view this Task", which would confirm the Task exists. The body carries no resource id, no
    /// title and no Space name.
    /// </para>
    /// <para>
    /// <b>Produced only at the capability check, and never converted.</b> A <c>404</c> is produced
    /// only at Space resolution; no handler turns one into the other.
    /// </para>
    /// </remarks>
    public static string InsufficientRole => "urn:yello:problem:insufficient-role";

    /// <summary>
    /// An NFR-8 bound was reached. Carries a <c>bound</c> extension (the bound's stable code) and a
    /// <c>limit</c> extension (its value). AD-25 / AR-30.
    /// </summary>
    /// <remarks>
    /// A <c>409</c>, because <c>403</c> and <c>404</c> are reserved by AD-3 for the Space boundary.
    /// A bound degrades visibly, never silently: the client states the number plainly. Not an
    /// <c>AccessRefusal</c>; AD-20 records exactly two kinds and this is neither.
    /// </remarks>
    public static string BoundReached => "urn:yello:problem:bound-reached";

    /// <summary>
    /// Too many requests to a password-hashing endpoint. Answered <c>429</c> with
    /// <c>Retry-After</c>. AD-18.
    /// </summary>
    /// <remarks>
    /// <b>Uniform, like the answers it protects.</b> The limiter never partitions on the submitted
    /// address, and the body is identical for a known and an unknown one - a limiter that refused
    /// them differently would reintroduce the oracle AD-23 closed. API only; there is no browser
    /// surface for it.
    /// </remarks>
    public static string RateLimited => "urn:yello:problem:rate-limited";

    /// <summary>
    /// An API Token issuance whose name is not 1-100 characters once trimmed. Answered <c>400</c>.
    /// Story 1.8, open question 8.
    /// </summary>
    /// <remarks>
    /// The client renders its own words for it, keyed on this <c>type</c> (AR-34).
    /// </remarks>
    public static string InvalidApiToken => "urn:yello:problem:invalid-api-token";

    /// <summary>
    /// A Project name that is not 1-100 characters once trimmed. Answered <c>400</c>. Story 2.1, open
    /// question 3.
    /// </summary>
    /// <remarks>
    /// <b>Never "that name is taken".</b> Project names are not unique within a Space; this reports only
    /// what was sent.
    /// </remarks>
    public static string InvalidProjectName => "urn:yello:problem:invalid-project-name";

    /// <summary>
    /// A Task title that is not 1-200 characters once trimmed. Answered <c>400</c>; nothing is written.
    /// Story 2.2, open question 3 (a).
    /// </summary>
    /// <remarks>
    /// <b>Never "that title is taken", and never a statement about the Project.</b> Titles are not unique,
    /// and a Project that matches nothing answers <see cref="NotAvailable"/>; this reports only what was sent.
    /// </remarks>
    public static string InvalidTaskTitle => "urn:yello:problem:invalid-task-title";

    /// <summary>
    /// A Task edit whose Status is not one of the Task's own Project's effective set. Answered <c>400</c>;
    /// nothing is written. Story 2.3, open questions 5 (a) and 13 (a).
    /// </summary>
    /// <remarks>
    /// <b>Never "that Status belongs to another Space", never "no such Status", and never the id sent.</b> A
    /// random id and another Space's real Status id answer the same bytes, so the refusal cannot be used to
    /// learn that an id exists elsewhere (I-13). It is not a <c>404</c>: the Task is present, and a
    /// <c>404</c> would say it was not.
    /// </remarks>
    public static string InvalidTaskStatus => "urn:yello:problem:invalid-task-status";

    /// <summary>
    /// A Task edit whose due date is not an ISO 8601 instant with an offset between the start of 2000 and the
    /// end of 2100. Answered <c>400</c>; nothing is written. Story 2.3, open question 4 (a).
    /// </summary>
    /// <remarks>
    /// <b>Never a statement about the Task or its current due date</b>; this reports only what was sent.
    /// <c>null</c> is not this refusal - it clears the due date.
    /// </remarks>
    public static string InvalidDueDate => "urn:yello:problem:invalid-due-date";

    /// <summary>
    /// A Task edit body that cannot be read as a JSON object, or that names none of the title, the Status, the
    /// due date and the Labels, or whose <c>labelIds</c> is not an array or holds more entries than any set could.
    /// Answered <c>400</c>; nothing is written. Story 2.3, open question 1 (a); story 2.4, open question 6. Also a move
    /// body (story 2.6) that cannot be read, leaves out <c>statusId</c> or <c>afterTaskId</c>, names an
    /// <c>afterTaskId</c> that is neither an id nor <c>null</c>, or carries a <c>positionKey</c> - a move is a Task edit
    /// by its own route.
    /// </summary>
    /// <remarks>
    /// <b>Never a statement about the Task</b>, and never a list of what the body did name. The product's own
    /// client always sends at least one member, so only a hand-written request meets it.
    /// </remarks>
    public static string InvalidTaskEdit => "urn:yello:problem:invalid-task-edit";

    /// <summary>
    /// A Label name that is not 1-50 characters once trimmed. Answered <c>400</c>; nothing is written. Story 2.4,
    /// open question 2.
    /// </summary>
    /// <remarks>
    /// <b>Never "that name is taken"</b> - that is <see cref="LabelNameTaken"/>. This reports only what was sent.
    /// </remarks>
    public static string InvalidLabelName => "urn:yello:problem:invalid-label-name";

    /// <summary>
    /// A Label colour that is not one of the palette's keys - a colour value such as <c>#D2592D</c> included.
    /// Answered <c>400</c>; nothing is written. Story 2.4, open question 3.
    /// </summary>
    /// <remarks>
    /// <b>Never a list of the keys, and never the value sent</b>; the client offers the palette and nothing else.
    /// </remarks>
    public static string InvalidLabelColour => "urn:yello:problem:invalid-label-colour";

    /// <summary>
    /// The Space already holds a Label of that name, compared ignoring case. Answered <c>409</c>; nothing is
    /// written. Story 2.4, open question 2.
    /// </summary>
    /// <remarks>
    /// <b>Never the name, the existing Label's id or its colour</b>, and never a statement about another Space: names
    /// are unique per Space, so the same name in another Space is defined without conflict.
    /// </remarks>
    public static string LabelNameTaken => "urn:yello:problem:label-name-taken";

    /// <summary>
    /// The Label is applied to at least one Task, so it is not deleted until those applications are removed.
    /// Answered <c>409</c>; nothing is deleted. Story 2.4, AC3, open question 11.
    /// </summary>
    /// <remarks>
    /// <b>Never how many Tasks carry it, and never their titles or ids.</b> It is not "the Label does not exist" - a
    /// Label that matches nothing answers <see cref="NotAvailable"/>.
    /// </remarks>
    public static string LabelInUse => "urn:yello:problem:label-in-use";

    /// <summary>
    /// A Task edit whose <c>labelIds</c> names a Label that is not one of the Space's - unknown, another Space's,
    /// repeated, not an id, or deleted while the edit ran. Answered <c>400</c>; nothing is written. Story 2.4, AC10,
    /// AC11.
    /// </summary>
    /// <remarks>
    /// <b>Never which entry, never the id sent, and never "that Label belongs to another Space".</b> A random id and
    /// another Space's real Label id answer the same bytes (I-13). It is not a <c>404</c>: the Task is present.
    /// </remarks>
    public static string InvalidTaskLabels => "urn:yello:problem:invalid-task-labels";

    /// <summary>
    /// A move whose position is not one: the Task to land after is not a Task in the destination column of the moved
    /// Task's Project - absent, deleted, moved away, in another Project or column, another Space's, or the moved Task
    /// itself - or no position key fits between it and its successor. Answered <c>409</c>; nothing is written. Story 2.6,
    /// open questions 3 and 10.
    /// </summary>
    /// <remarks>
    /// <b>Never whether the neighbour exists, where it is, or that it belongs to another Space</b>: a random id and
    /// another Space's real Task id answer the same bytes (I-13). It is not a <c>404</c> - the moved Task is present - and
    /// never a renumber of the column's other Tasks (AD-15). The person chooses the position again.
    /// </remarks>
    public static string TaskPositionUnavailable => "urn:yello:problem:task-position-unavailable";

    /// <summary>
    /// A move into a gap where no position key of at most 128 characters fits between its neighbours. Answered
    /// <c>409</c>; nothing is written. Story 2.9, AC18, open question 11 (a).
    /// </summary>
    /// <remarks>
    /// <b>Not <see cref="TaskPositionUnavailable"/></b>: the neighbour is there, and choosing the same position again would
    /// be refused again, so the person is asked to choose another position. Never a renumber of the column (AD-15).
    /// </remarks>
    public static string TaskPositionFull => "urn:yello:problem:task-position-full";

    /// <summary>
    /// A Board column page whose <c>after</c> cursor is not a position key - empty, longer than 128 characters, or holding
    /// a character outside the key alphabet. Answered <c>400</c>. Story 2.9, AC9.
    /// </summary>
    /// <remarks>
    /// <b>Never a statement about a stored key</b>: it depends only on the cursor as sent. A well-formed key no Task holds
    /// is served - the page after it.
    /// </remarks>
    public static string InvalidBoardCursor => "urn:yello:problem:invalid-board-cursor";

    /// <summary>
    /// An <c>Idempotency-Key</c> header that is empty or longer than
    /// <see cref="Idempotency.MaxKeyLength"/> characters. Answered <c>400</c>; nothing is applied.
    /// </summary>
    /// <remarks>
    /// <b>Never a statement about a stored key.</b> It depends only on the header as sent.
    /// </remarks>
    public static string InvalidIdempotencyKey => "urn:yello:problem:invalid-idempotency-key";

    /// <summary>
    /// An <c>Idempotency-Key</c> the same Account already used in this Space for a different request.
    /// Answered <c>422</c>; nothing is applied. AD-18; story 2.1, open question 7.
    /// </summary>
    /// <remarks>
    /// <b>Never a replay, and never a new request.</b> Treating it as either would silently defeat the
    /// guarantee the key exists to give. Scoped to (Space, Account): another Account's key, or the same
    /// key in another Space, is never compared with it.
    /// </remarks>
    public static string IdempotencyKeyReused => "urn:yello:problem:idempotency-key-reused";

    /// <summary>
    /// An <c>Idempotency-Key</c> whose first use committed while this request with the same key was
    /// still running. Answered <c>409</c>, rolled back; a retry is then answered with the first use's
    /// response. Story 2.1, open question 7.
    /// </summary>
    /// <remarks>
    /// <b>Never "a limit was reached"</b> - that is <see cref="BoundReached"/>, also a <c>409</c>.
    /// </remarks>
    public static string IdempotencyKeyInFlight => "urn:yello:problem:idempotency-key-in-flight";

    /// <summary>
    /// <c>503</c>: the database is paused, resuming or unreachable. Uniform for every route, carries
    /// <c>Retry-After</c>, and admits nothing (story 1.10 AC14).
    /// </summary>
    public static string ServiceUnavailable => "urn:yello:problem:service-unavailable";
}
