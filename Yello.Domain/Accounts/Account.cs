namespace Yello.Domain.Accounts;

/// <summary>
/// A person's identity in Yello: an email address, and the credential they authenticate with.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>Users</c> table in this architecture</b> - the entity is <c>Account</c>. A
/// <c>User</c> is an Account acting inside a specific Space, which is what a
/// <see cref="Memberships.Membership"/> expresses. PRD section 2's Glossary fixes both words and
/// <c>epics.md:50</c> calls a synonym "a discipline violation in every story written below".
/// </para>
/// <para>
/// <b><see cref="PasswordHash"/> is nullable, and that is load-bearing rather than defensive.</b>
/// <c>harness-constraints.md:63-66</c> records "an Account is created with an email address
/// <i>and a password</i>" as one of the four assumptions OAuth sign-in will break, and
/// <c>SPEC.md:241</c> schedules that change to fire "once the identity epic has shipped". An
/// OAuth Account has no password at all, so nothing may assume one exists on every Account -
/// which is AC7 in a sentence. Making the column non-nullable would be the cheapest way to
/// harden exactly what is known to be changing.
/// </para>
/// <para>
/// <b><see cref="NormalizedEmailAddress"/> exists so uniqueness is decided by a stated rule
/// rather than by a collation.</b> Nothing in the SPEC, the PRD, the addendum, the spine, the
/// epics or the readiness report says whether email comparison is case-insensitive or how the
/// index is collated - so story 1.3 recorded the answer as a decision rather than inheriting a
/// framework default: comparison is case-insensitive, performed by upper-casing with the
/// invariant culture into this column, and the unique index is on this column. The address the
/// person typed is preserved verbatim in <see cref="EmailAddress"/> and is what any future
/// correspondence would use.
/// </para>
/// <para>
/// <b>The uniqueness index cannot become a soft-delete tombstone.</b> FR-3 (<c>prd.md:146</c>)
/// requires that a deleted Account's address "can be used to register a new Account, and that
/// new Account inherits no Membership, no Space and no history". Account deletion is story 5.4's
/// and is a hard delete; there is deliberately no <c>DeletedAt</c> column here for a later story
/// to soft-delete into, because a tombstone that keeps the address occupied would contradict
/// FR-3 while every test in this story still passed.
/// </para>
/// <para>
/// <b>Creating one of these trips PRD section 6.4's data-protection gate.</b> The PRD claims no
/// data-protection posture "while the operator is the only data subject" and gives the gate a
/// testable trigger - the first Account created by anyone other than the operator - which
/// <c>epics.md:940-949</c> states makes the PRD non-compliant until amended. The remediation
/// assigned the gate itself to <b>story 1.10</b> as two acceptance criteria
/// (<c>readiness report:1386-1398</c>), so it is cross-referenced here rather than implemented.
/// </para>
/// </remarks>
public sealed class Account
{
    /// <summary>
    /// The Account's identity. Generated application-side and sequentially - see
    /// <see cref="IIdentifierGenerator"/> for why it is neither a database identity nor
    /// <c>Guid.NewGuid()</c>.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The address exactly as the person typed it, preserved for display and correspondence.
    /// Uniqueness is decided by <see cref="NormalizedEmailAddress"/>, never by this.
    /// </summary>
    public required string EmailAddress { get; init; }

    /// <summary>
    /// The upper-cased form the unique index is built on. See the class remarks for why the
    /// normalisation rule is stated here rather than left to a collation.
    /// </summary>
    public required string NormalizedEmailAddress { get; init; }

    /// <summary>
    /// The name this person is shown by, and the name their Personal Space is derived from.
    /// </summary>
    /// <remarks>
    /// Collected at registration by Lee's decision of 2026-08-28, which resolved the
    /// contradiction story 1.3 raised: three documents require a display name for the Personal
    /// Space ("Ravi's Space") while five fix registration at exactly two fields, and nothing
    /// anywhere defined the attribute. Deriving it from the email local part was the alternative
    /// and was rejected - it yields "lee.sanderson's Space" rather than "Lee's Space", and it
    /// leaves UX-DR34 with no display name for the Membership rows epic 4 renders or the avatar
    /// initials <c>EXPERIENCE.md:210</c> derives from them.
    /// <para>
    /// Not nullable, and not an OAuth hazard: every provider returns a display name, so this
    /// hardens nothing that <c>harness-constraints.md:63-66</c> says is going to move.
    /// </para>
    /// </remarks>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The password, stored only under a deliberately slow one-way function - or <c>null</c> for
    /// an Account that authenticates some other way. AC5, AC7.
    /// </summary>
    /// <remarks>
    /// Settable rather than <c>init</c>-only because story 1.4 owns the rehash-on-verify path:
    /// <c>VerifyHashedPassword</c> returns <c>SuccessRehashNeeded</c> when the iteration count
    /// embedded in the stored hash is below the configured one, and the upgrade writes a new hash
    /// to this property on the next successful sign-in. That mechanism is what makes NFR-6's
    /// "work factor tunable without re-registering existing Accounts" true, so the shape that
    /// admits it belongs here even though this story never performs the upgrade.
    /// </remarks>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// When the Account was created, in UTC.
    /// </summary>
    /// <remarks>
    /// <c>DateTimeOffset</c>, never <c>DateTime</c> (AR-34). <c>DateTimeOffset.Now</c> is a banned
    /// API at build; the value comes from <see cref="TimeProvider"/> so a test can control it.
    /// </remarks>
    public required DateTimeOffset CreatedAt { get; init; }
}
