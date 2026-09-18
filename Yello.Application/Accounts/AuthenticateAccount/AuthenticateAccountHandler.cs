using Yello.Domain.Accounts;

namespace Yello.Application.Accounts.AuthenticateAccount;

/// <summary>
/// The one slice that checks a credential. FR-2, AD-23 / AR-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>It verifies a credential and returns an Account identity or nothing.</b> It does not issue a
/// cookie, touch <c>HttpContext</c>, or know what a Session is - <c>Yello.Application</c> may not
/// reference ASP.NET Core, enforced twice: by the ring gate's exact-equality table and by the
/// <c>Microsoft.AspNetCore.</c> package-prefix ban. Cookie issuance is
/// <c>Yello.Host</c>'s, in the composition root, where AD-7 requires it to be configured once.
/// </para>
/// <para>
/// <b>The verification is unconditional, exactly as <c>RegisterAccountHandler</c>'s hash is.</b>
/// An unknown address still performs a full verification against a dummy hash of the same work
/// factor, because <see cref="IPasswordHasher.Verify"/> takes a nullable hash and does the work
/// anyway. <c>if (account is null) return Fail();</c> is the defect AD-23 exists to catch, and the
/// only reason it is not written below is that there is nothing here to write it against: the read
/// and the verification are two statements with no branch between them.
/// </para>
/// <para>
/// <b>Nothing here shapes a response, and that is deliberate.</b>
/// <c>harness-constraints.md:66</c> warns that AD-23's guarantee "must hold identically on the
/// OAuth path, which is a new disclosure surface", so the uniformity - status, body, shape and
/// duration - is implemented in the endpoint over this slice's single <c>null</c>, not inside the
/// password comparator. A guarantee that lived in the password branch would not exist on any other
/// credential path.
/// </para>
/// <para>
/// <b>Nothing cross-cutting is re-implemented here</b> (AR-3). No authorisation, no Space
/// resolution, no refusal recording, no idempotency, no rate limit and no bound check. Stories 1.5
/// and 1.6 build the pipeline that owns them; a limiter bolted into this slice is precisely what
/// that rule forbids, and <c>deferred-work.md</c> records the exposure against 1.6 by name.
/// </para>
/// </remarks>
/// <param name="passwordHasher">Checks the credential, and makes the upgraded hash when one is due.</param>
/// <param name="credentialStore">Reads the stored credential, and writes the upgraded hash.</param>
public sealed class AuthenticateAccountHandler(
    IPasswordHasher passwordHasher,
    IAccountCredentialStore credentialStore)
{
    /// <summary>
    /// Authenticates a submission, or does not - and never says which of the two reasons applied.
    /// </summary>
    /// <param name="command">The submission, already validated.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// The authenticated Account, or <c>null</c>. <b>One <c>null</c> for both causes</b>: an
    /// unknown address and a known address with a wrong password are the same value here, so no
    /// caller can tell them apart however carefully it reads this result.
    /// </returns>
    public async Task<AuthenticatedAccount?> HandleAsync(
        AuthenticateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var credential = await credentialStore
            .FindByEmailAddressAsync(
                EmailAddressNormalisation.Normalise(command.EmailAddress),
                cancellationToken)
            .ConfigureAwait(false);

        // UNCONDITIONAL, AND NOT GUARDED BY THE LINE ABOVE. This is where essentially all of a
        // sign-in's duration lives - measured at ~273 ms against single-digit milliseconds for the
        // indexed read - so a `credential is null` check placed before it is the timing oracle
        // AD-23 exists to close. Verify takes a nullable hash so that there is nothing to guard:
        // a null costs a full verification against a dummy hash of the same work factor.
        var verification = passwordHasher.Verify(credential?.PasswordHash, command.Password);

        if (credential is null || verification is PasswordVerification.Failed)
        {
            return null;
        }

        if (verification is PasswordVerification.SuccessRehashNeeded)
        {
            // NFR-6's "work factor tunable without re-registering existing Accounts", made real.
            // Story 1.3 proved the mechanism - its integration suite raises the work factor and
            // asserts that an existing hash stays valid and asks to be rewritten - but nothing
            // could act on that answer until now. This is the line that makes it reachable, and
            // Account.PasswordHash was left settable rather than init-only specifically for it.
            //
            // A SECOND HASH, ON THE SUCCESS PATH ONLY. It cannot leak existence: the failure path
            // returned above and never arrives here, so the two causes AC4 compares still cost the
            // same. What it does mean is that a successful sign-in with an out-of-date hash costs
            // roughly twice a failed one - which AC4 does not ask to be equal, and deliberately so.
            await credentialStore
                .RehashAsync(
                    credential.AccountId,
                    passwordHasher.Hash(command.Password),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new AuthenticatedAccount(credential.AccountId);
    }
}

/// <summary>
/// The Account a Session will identify, and nothing else about it.
/// </summary>
/// <remarks>
/// <b>One field, and it is not the email address.</b> AC1 requires a Session to identify the
/// Account "and carry no Role, no Space and no capability of its own", and the cheapest way to
/// keep that true is for the only thing that reaches Session issuance to be an identity. AD-1 is
/// the same rule stated upstream: "Role is never read from a cookie, a claim, an API Token payload,
/// or any cache outliving the request". There is nothing here for one to be read from.
/// </remarks>
/// <param name="AccountId">The authenticated Account's identity.</param>
public sealed record AuthenticatedAccount(Guid AccountId);
