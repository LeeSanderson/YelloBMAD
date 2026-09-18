namespace Yello.Application.Accounts.AuthenticateAccount;

/// <summary>
/// Everything sign-in is given: an email address and a password.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two fields, and no more.</b> No "remember me" - nothing in the PRD, the spec set, the spine,
/// the epics or the UX artefacts mentions one, and it is a Session-lifetime decision wearing a
/// checkbox. No password-reset path either: no FR, CAP or journey anywhere names password reset,
/// and adding one invents a requirement - the same reasoning that kept email verification out of
/// story 1.3.
/// </para>
/// <para>
/// <b>The address is how the Account is <i>found</i>, and that is all it is.</b>
/// <c>harness-constraints.md:63</c> marks "an Account is unique by email address" as the
/// load-bearing assumption OAuth sign-in will break, so nothing downstream of this record may
/// treat the address as identity: the Session carries <c>Account.Id</c>, no claim carries an
/// address, and no route segment does either.
/// </para>
/// <para>
/// <b>The password is a plain <c>string</c></b>, for the reasons
/// <c>RegisterAccountCommand</c> already records: <c>SecureString</c> is documented as not
/// providing meaningful protection on .NET Core, and the real controls are that the value reaches
/// exactly one method, that no log statement takes this command as a parameter, and that the
/// endpoint returns no body.
/// </para>
/// </remarks>
/// <param name="EmailAddress">The address the Account is found by.</param>
/// <param name="Password">The password, in the only place it exists in clear.</param>
public sealed record AuthenticateAccountCommand(string EmailAddress, string Password);
