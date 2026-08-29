namespace Yello.Application.Accounts.RegisterAccount;

/// <summary>
/// Structural validation of a <see cref="RegisterAccountCommand"/>: is this a well-formed
/// submission at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule here reads only the submitted values, and that is the property AD-23 needs.</b>
/// Nothing in this class touches the datastore, so no outcome of it can vary with whether an
/// Account already exists for the address. That is the precise reason validating before hashing
/// is safe here while a <i>password-policy</i> rejection would not be: the story warns that "a
/// server-side password-policy rejection that returns before the hash is performed reintroduces
/// the same branch by another route", and the distinction is exactly this one - a rule that
/// depends only on the input cannot be a probe for stored state, and a rule that depends on
/// stored state must never return before the hash.
/// </para>
/// <para>
/// <b>There is deliberately no password-strength policy.</b> No FR, NFR or acceptance criterion
/// asks for one; the client can refuse a weak password before it is ever submitted, which is
/// free and safe; and a server-side one buys nothing here while adding a second early-return
/// path through the one endpoint whose whole contract is that it has no branches.
/// </para>
/// <para>
/// <b>Hand-written rather than FluentValidation.</b> That library is not in AR-1's pinned stack
/// and adding a package is an architecture edit; three fields do not justify one.
/// </para>
/// <para>
/// The failure codes are stable machine-readable strings, not prose. AR-34 requires errors to be
/// RFC 9457 <c>application/problem+json</c> with a stable <c>type</c>, and prose is never the
/// contract - the wording a person reads comes from a resource on the client.
/// </para>
/// </remarks>
public static class RegisterAccountValidator
{
    /// <summary>
    /// RFC 5321 caps an email path at 256 octets including the angle brackets, leaving 254 for
    /// the address. Matches the column.
    /// </summary>
    private const int EmailAddressMaxLength = 254;

    /// <summary>
    /// Matches the display-name column.
    /// </summary>
    private const int DisplayNameMaxLength = 128;

    /// <summary>
    /// A ceiling rather than a strength rule: it exists so an unbounded body cannot make the
    /// deliberately-slow hash a denial-of-service lever. PBKDF2's cost is driven by the iteration
    /// count rather than by input length, so this is defence against the request rather than
    /// against the password.
    /// </summary>
    private const int PasswordMaxLength = 4096;

    /// <summary>
    /// Checks a submission and returns the codes for whatever is wrong with it.
    /// </summary>
    /// <param name="command">The submission.</param>
    /// <returns>
    /// One code per failed rule, empty when the submission is well-formed. Every rule is
    /// evaluated rather than stopping at the first, so a person correcting the form is told
    /// everything at once instead of discovering the next problem after each attempt.
    /// </returns>
    public static IReadOnlyList<string> Validate(RegisterAccountCommand command)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            failures.Add(RegisterAccountFailure.DisplayNameRequired);
        }
        else if (command.DisplayName.Trim().Length > DisplayNameMaxLength)
        {
            failures.Add(RegisterAccountFailure.DisplayNameTooLong);
        }
        else
        {
            // Present and within the column. There is nothing else to check: a display name is
            // whatever a person calls themselves, in any script, and a "reasonable name" rule
            // would refuse real ones.
        }

        if (string.IsNullOrWhiteSpace(command.EmailAddress))
        {
            failures.Add(RegisterAccountFailure.EmailAddressRequired);
        }
        else if (command.EmailAddress.Trim().Length > EmailAddressMaxLength)
        {
            failures.Add(RegisterAccountFailure.EmailAddressTooLong);
        }
        else if (!IsPlausibleEmailAddress(command.EmailAddress.Trim()))
        {
            failures.Add(RegisterAccountFailure.EmailAddressMalformed);
        }
        else
        {
            // Plausible. Deliberately NOT checked for existence here - that is stored state, and
            // reading it before the hash would be the exact branch AD-23 exists to close.
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            failures.Add(RegisterAccountFailure.PasswordRequired);
        }
        else if (command.Password.Length > PasswordMaxLength)
        {
            failures.Add(RegisterAccountFailure.PasswordTooLong);
        }
        else
        {
            // Present and bounded. No strength policy, deliberately - see the class remarks.
        }

        return failures;
    }

    /// <summary>
    /// A deliberately shallow check: one <c>@</c>, something either side of it, and no
    /// whitespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Shallow on purpose.</b> RFC 5322's grammar admits quoted local parts, comments and
    /// bracketed address literals, and every "full" validating regular expression in circulation
    /// rejects addresses that genuinely deliver. Since Yello sends no mail at all - there is no
    /// email verification anywhere in the contract, and adding one would breach AD-23 by
    /// creating an out-of-band enumeration oracle - the only thing a stricter rule could achieve
    /// here is refusing a real person's real address.
    /// </para>
    /// <para>
    /// Written as a scan rather than a regular expression so there is no pattern to backtrack:
    /// this runs on unauthenticated input, before any cost is incurred.
    /// </para>
    /// </remarks>
    private static bool IsPlausibleEmailAddress(string candidate)
    {
        var at = candidate.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at == candidate.LastIndexOf('@')
            && at < candidate.Length - 1
            && !candidate.Any(char.IsWhiteSpace);
    }
}

/// <summary>
/// The stable machine-readable codes <see cref="RegisterAccountValidator"/> reports.
/// </summary>
/// <remarks>
/// <b>These are the contract; the prose beside them on screen is not.</b> AR-34 requires a stable
/// <c>type</c> on every problem response, and the client maps each code to a localised resource -
/// so a translator changing the wording cannot change the contract, and a new locale needs no
/// server change. Note that none of these codes can distinguish a known address from an unknown
/// one, which is what keeps validation compatible with AD-23.
/// </remarks>
public static class RegisterAccountFailure
{
    /// <summary>
    /// No display name was submitted.
    /// </summary>
    public static string DisplayNameRequired => "display-name-required";

    /// <summary>
    /// The display name exceeds what the column stores.
    /// </summary>
    public static string DisplayNameTooLong => "display-name-too-long";

    /// <summary>
    /// No email address was submitted.
    /// </summary>
    public static string EmailAddressRequired => "email-address-required";

    /// <summary>
    /// The email address exceeds RFC 5321's limit.
    /// </summary>
    public static string EmailAddressTooLong => "email-address-too-long";

    /// <summary>
    /// The email address has no plausible local part and domain.
    /// </summary>
    public static string EmailAddressMalformed => "email-address-malformed";

    /// <summary>
    /// No password was submitted.
    /// </summary>
    public static string PasswordRequired => "password-required";

    /// <summary>
    /// The password exceeds the request ceiling.
    /// </summary>
    public static string PasswordTooLong => "password-too-long";
}
