namespace Yello.Domain.Accounts;

/// <summary>
/// The one rule that decides whether two email addresses are the same Account. FR-1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recorded as a decision, because nothing upstream made one.</b> Story 1.3 searched the SPEC,
/// its companions, the PRD, the addendum, the architecture spine, the epics and the readiness
/// report: none of them states whether email comparison is case-insensitive, or how the
/// uniqueness index is collated. ASP.NET Core Identity's <c>NormalizedEmail</c> would have
/// supplied case-insensitivity by default - but an inherited framework default is not a recorded
/// decision, and this is the comparison FR-1's entire uniqueness guarantee rests on.
/// </para>
/// <para>
/// <b>The decision: comparison is case-insensitive, and normalisation is explicit rather than
/// collational.</b> Trim, then upper-case with the invariant culture, into
/// <see cref="Account.NormalizedEmailAddress"/> - which is the column the unique index is built
/// on. Two properties follow that a case-insensitive collation would not have given:
/// </para>
/// <list type="bullet">
///   <item><description>
///     The rule is <i>readable</i>, in one function, rather than being a property of the
///     database that a restore onto a differently-collated server could change silently.
///   </description></item>
///   <item><description>
///     It is stable under AD-15's <c>Latin1_General_100_BIN2</c>, the binary collation story 2.6
///     owns and which <c>ALTER DATABASE ... COLLATE</c> cannot reverse on Azure SQL. Under a
///     binary collation a case-insensitive index is not available at all, so a design that
///     depended on one would have collided with a decision already taken.
///   </description></item>
/// </list>
/// <para>
/// <b>Upper-case rather than lower-case</b>: CA1308 prefers it, because lower-casing is lossy in
/// scripts where upper-casing is not.
/// </para>
/// <para>
/// <b>Kept soft.</b> <c>harness-constraints.md:63</c> marks "an Account is unique by email
/// address" as "the load-bearing one ... a Glossary-level claim, so it reaches every artifact" -
/// the assumption OAuth sign-in is most likely to break, since a provider may return a different
/// address or none. Nothing here treats the address as the Account's identity: the identity is
/// <see cref="Account.Id"/>, and this is a uniqueness rule applied to one column.
/// </para>
/// </remarks>
public static class EmailAddressNormalisation
{
    /// <summary>
    /// The comparable form of an email address.
    /// </summary>
    /// <param name="emailAddress">The address as the person typed it.</param>
    /// <returns>The value the unique index is built on.</returns>
    public static string Normalise(string emailAddress) =>
        emailAddress.Trim().ToUpperInvariant();
}
