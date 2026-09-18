namespace Yello.Client.Pages;

/// <summary>
/// The prefixes that attach a server failure code to the field it is about, on the sign-in
/// surface.
/// </summary>
/// <remarks>
/// <para>
/// The same convention <see cref="RegistrationFields"/> records, and a separate list for the same
/// reason <see cref="SignInPhase"/> is a separate enum: the two surfaces do not have the same
/// fields, and a shared list would silently accept a display-name code on a form that has no
/// display name.
/// </para>
/// <para>
/// <b>These cover the <c>400</c> only.</b> The <c>401</c> - the one that matters on this surface -
/// carries no codes at all and is rendered from a single string keyed on the problem
/// <c>type</c>, because AD-23 leaves nothing to attach to a field.
/// </para>
/// <para>
/// <b>Asserted rather than trusted.</b>
/// <c>LocalisationGateTests.Every_sign_in_failure_code_has_a_message_and_a_field</c> lives in the
/// architecture suite - the one project that references both <c>Yello.Application</c> and
/// <c>Yello.Client</c> - and checks every code <c>AuthenticateAccountFailure</c> can emit against
/// these prefixes and against <c>ClientCopy.resx</c>. The registration gate beside it reflects over
/// <c>RegisterAccountFailure</c> specifically and does not reach these.
/// </para>
/// </remarks>
public static class SignInFields
{
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
    public static IReadOnlyList<string> All { get; } = [EmailAddress, Password];
}
