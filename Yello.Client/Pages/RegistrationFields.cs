namespace Yello.Client.Pages;

/// <summary>
/// The prefixes that attach a server failure code to the field it is about.
/// </summary>
/// <remarks>
/// <para>
/// The Host's failure codes are named <c>{field}-{problem}</c> - <c>email-address-malformed</c>,
/// <c>password-required</c> - so a code can be shown under the control it concerns rather than
/// only in the summary region. These are the field halves.
/// </para>
/// <para>
/// <b>Why the codes are not simply shared.</b> They are declared in
/// <c>Yello.Application.Accounts.RegisterAccount.RegisterAccountFailure</c>, and
/// <c>Yello.Client</c> cannot reference <c>Yello.Application</c> - the ring table allows the
/// client only <c>Yello.Contracts</c> and <c>Yello.Merge</c>. Moving the codes into
/// <c>Yello.Contracts</c> would not help either: <c>Yello.Application</c>'s row permits
/// <c>Yello.Domain</c> and nothing else, so the validator could not read them there. Either fix
/// is an edit to the ring table, which is an architecture decision rather than a convenience, and
/// this story does not make one for a naming convention.
/// </para>
/// <para>
/// <b>The convention is asserted rather than trusted.</b>
/// <c>RegistrationFailureCodeTests</c> lives in the architecture suite - the one project that
/// references both <c>Yello.Application</c> and <c>Yello.Client</c> - and checks that every code
/// the validator can emit begins with one of the prefixes below. So a renamed code fails the
/// build here rather than silently detaching a message from its field.
/// </para>
/// <para>
/// <b>Failing to match is degraded, not broken.</b> A code that matches no prefix still appears
/// in the error region, because that region lists every code returned. What is lost is only the
/// message under the specific control.
/// </para>
/// </remarks>
public static class RegistrationFields
{
    /// <summary>
    /// The display-name field.
    /// </summary>
    public static string DisplayName => "display-name";

    /// <summary>
    /// The email-address field.
    /// </summary>
    public static string EmailAddress => "email-address";

    /// <summary>
    /// The password field.
    /// </summary>
    public static string Password => "password";

    /// <summary>
    /// Every prefix, for the gate that checks the server's codes against them.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [DisplayName, EmailAddress, Password];
}
