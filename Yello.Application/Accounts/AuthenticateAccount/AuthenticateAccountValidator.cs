namespace Yello.Application.Accounts.AuthenticateAccount;

/// <summary>
/// Structural validation of an <see cref="AuthenticateAccountCommand"/>: is this a well-formed
/// submission at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule here reads only the submitted values, and that is the property AD-23 needs.</b>
/// Nothing in this class touches the datastore, so no outcome of it can vary with whether an
/// Account exists for the address. That is the whole distinction story 1.3 drew and this story
/// inherits: a rule that depends only on the input cannot be a probe for stored state, while a
/// rule that depends on stored state must never return before the hash.
/// </para>
/// <para>
/// <b>A validator that rejects before the verification reintroduces the branch by another
/// route.</b> The rules below are deliberately thinner than
/// <c>RegisterAccountValidator</c>'s - there is no length ceiling on the address and no format
/// check - and the reason is precisely AD-23. Registration may refuse a malformed address because
/// the refusal says nothing about what is stored. Sign-in may too, in principle, but every rule
/// added here is another 400 that a well-formed attempt does not receive, and the set of addresses
/// that reach the uniform 401 shrinks with each one. Kept to presence alone: an empty field is not
/// an authentication attempt, and everything that is one costs the same.
/// </para>
/// <para>
/// <b>There is deliberately no password-strength policy and no length ceiling that a real
/// password could reach.</b> <see cref="PasswordMaxLength"/> is defence against an unbounded
/// request body rather than against a password, exactly as registration's is - PBKDF2's cost is
/// driven by the iteration count, not by input length.
/// </para>
/// <para>
/// <b>Hand-written rather than FluentValidation.</b> That library is not in AR-1's pinned stack
/// and adding a package is an architecture edit; two fields do not justify one.
/// </para>
/// <para>
/// The failure codes are stable machine-readable strings, not prose (AR-34). The wording a person
/// reads comes from a resource on the client.
/// </para>
/// </remarks>
public static class AuthenticateAccountValidator
{
    /// <summary>
    /// A ceiling rather than a strength rule: it exists so an unbounded body cannot make the
    /// deliberately-slow verification a denial-of-service lever. Matches registration's, so a
    /// password that could be registered can always be used to sign in.
    /// </summary>
    private const int PasswordMaxLength = 4096;

    /// <summary>
    /// RFC 5321 caps an email path at 256 octets including the angle brackets, leaving 254 for the
    /// address. Matches the column, and matches registration's ceiling for the same reason as
    /// above: an address that could be registered must be usable to sign in.
    /// </summary>
    private const int EmailAddressMaxLength = 254;

    /// <summary>
    /// Checks a submission and returns the codes for whatever is wrong with it.
    /// </summary>
    /// <param name="command">The submission.</param>
    /// <returns>
    /// One code per failed rule, empty when the submission is well-formed. Every rule is
    /// evaluated rather than stopping at the first, so a person correcting the form is told
    /// everything at once instead of discovering the next problem after each attempt.
    /// </returns>
    public static IReadOnlyList<string> Validate(AuthenticateAccountCommand command)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(command.EmailAddress))
        {
            failures.Add(AuthenticateAccountFailure.EmailAddressRequired);
        }
        else if (command.EmailAddress.Trim().Length > EmailAddressMaxLength)
        {
            failures.Add(AuthenticateAccountFailure.EmailAddressTooLong);
        }
        else
        {
            // Present and within the column. Deliberately NOT checked for plausibility: a
            // malformed address cannot match a stored one, so the uniform 401 already answers it,
            // and a 400 here would answer a subset of non-existent addresses differently from the
            // rest.
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            failures.Add(AuthenticateAccountFailure.PasswordRequired);
        }
        else if (command.Password.Length > PasswordMaxLength)
        {
            failures.Add(AuthenticateAccountFailure.PasswordTooLong);
        }
        else
        {
            // Present and bounded. No strength policy - see the class remarks.
        }

        return failures;
    }
}

/// <summary>
/// The stable machine-readable codes <see cref="AuthenticateAccountValidator"/> reports.
/// </summary>
/// <remarks>
/// <para>
/// <b>None of these can distinguish a known address from an unknown one</b>, which is what keeps
/// validation compatible with AD-23. Every one of them is a statement about the submission.
/// </para>
/// <para>
/// <b>There is no code here for "those credentials are wrong", and there must never be one.</b>
/// That outcome is a <c>401</c> carrying one stable problem <c>type</c> and no <c>errors</c>
/// extension at all - see <c>AuthenticateAccountEndpoint</c>. A code would give the client
/// something to branch on, and the single string
/// <c>EXPERIENCE.md:272</c> fixes - "Email or password is incorrect." - exists precisely so there
/// is nothing to branch on.
/// </para>
/// <para>
/// <b>Covered by a build gate.</b>
/// <c>LocalisationGateTests.Every_sign_in_failure_code_has_a_message_and_a_field</c> reflects over
/// this class and checks each code against <c>ClientCopy.resx</c> and <c>SignInFields</c>. The
/// registration gate beside it reflects over <c>RegisterAccountFailure</c> specifically, so it
/// does not reach these - an uncovered code would otherwise ship with no message and no gate
/// complaining.
/// </para>
/// </remarks>
public static class AuthenticateAccountFailure
{
    /// <summary>
    /// No email address was submitted.
    /// </summary>
    public static string EmailAddressRequired => "email-address-required";

    /// <summary>
    /// The email address exceeds RFC 5321's limit, so no stored address can equal it.
    /// </summary>
    public static string EmailAddressTooLong => "email-address-too-long";

    /// <summary>
    /// No password was submitted.
    /// </summary>
    public static string PasswordRequired => "password-required";

    /// <summary>
    /// The password exceeds the request ceiling.
    /// </summary>
    public static string PasswordTooLong => "password-too-long";
}
