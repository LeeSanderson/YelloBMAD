namespace Yello.Application.Accounts.RegisterAccount;

/// <summary>
/// Everything registration is given: a display name, an email address and a password.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three fields, and no more.</b> UJ-1's climax and the mockup's negative-constraints block
/// rule out a plan picker, a team-size question, a confirm-password field, a terms checkbox, a
/// CAPTCHA and any onboarding step. Adding one is a specification violation rather than a product
/// improvement.
/// </para>
/// <para>
/// <b>The display name is the third field by Lee's decision of 2026-08-28</b>, which confirmed
/// PRD section 12's first assumption rather than revising it. FR-1 as written names two fields;
/// three documents require a display name for the Personal Space and UX-DR34 needs one for every
/// Membership rendering from Epic 4. See <c>PersonalSpaceName</c> for the full contradiction and
/// how it was resolved.
/// </para>
/// <para>
/// <b>The password is a plain <c>string</c> and is never stored, logged or echoed.</b> A
/// <c>SecureString</c> would be theatre on this platform - it is documented as not providing
/// meaningful protection on .NET Core - and the real controls are elsewhere: the value reaches
/// exactly one method (<c>IPasswordHasher.Hash</c>), no log statement takes the command as a
/// parameter, and the endpoint returns no body at all.
/// </para>
/// </remarks>
/// <param name="DisplayName">The name this person is shown by, and the Space is named from.</param>
/// <param name="EmailAddress">The address the Account is unique by.</param>
/// <param name="Password">The password, in the only place it exists in clear.</param>
public sealed record RegisterAccountCommand(
    string DisplayName,
    string EmailAddress,
    string Password);
