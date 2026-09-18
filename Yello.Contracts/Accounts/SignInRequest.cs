namespace Yello.Contracts.Accounts;

/// <summary>
/// The sign-in submission, on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two fields.</b> No "remember me" - that is a Session-lifetime decision wearing a checkbox,
/// and nothing in any artifact states a Session lifetime for it to vary.
/// </para>
/// <para>
/// <b>In <c>Yello.Contracts</c> so the client that posts it and the Host that binds it read one
/// declaration.</b> The client cannot reference <c>Yello.Application</c> (the ring table gives it
/// Contracts and Merge only), so the command the slice takes and the body the wire carries are
/// necessarily two types; this is the one both sides can see.
/// </para>
/// </remarks>
/// <param name="EmailAddress">The address the Account is found by.</param>
/// <param name="Password">The password, in the only place it exists in clear.</param>
public sealed record SignInRequest(string EmailAddress, string Password);
