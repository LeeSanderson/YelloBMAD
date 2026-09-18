namespace Yello.Domain.Accounts;

/// <summary>
/// The two things authentication does to stored state: read one Account's credential, and rewrite
/// its hash when the work factor has moved. FR-2, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>A second port rather than a member on <see cref="IAccountRegistrationStore"/>.</b> That one
/// exists because AD-22 makes the whole registration a single transaction and offering its parts
/// separately would let a caller commit two rows of three. Authentication has no such invariant:
/// it reads one row and, rarely, writes one column. Bolting it on would widen a port whose shape
/// is load-bearing for a use case that does not need the shape.
/// </para>
/// <para>
/// <b>The read returns a <see cref="AccountCredential"/>, not an <see cref="Account"/>.</b> The
/// slice needs an identity and a hash, and nothing else - not the display name, not the created
/// instant, and above all not the email address, which
/// <c>harness-constraints.md:63</c> marks as the assumption OAuth sign-in is most likely to
/// break. Handing the slice the whole entity would let the address reach a Session, a claim or a
/// route by the ordinary route of it simply being there.
/// </para>
/// <para>
/// <b>No method here can distinguish "no Account" from "an Account with no password".</b> Both
/// arrive as a <see cref="AccountCredential.PasswordHash"/> of <c>null</c> or as no record at all,
/// and the slice treats them identically - which is what AD-23 needs and what keeps an OAuth
/// Account, which has no password, from failing differently.
/// </para>
/// </remarks>
public interface IAccountCredentialStore
{
    /// <summary>
    /// Finds the credential for a normalised email address.
    /// </summary>
    /// <param name="normalizedEmailAddress">
    /// The output of <see cref="EmailAddressNormalisation.Normalise"/>, which is the column the
    /// unique index is built on. Passing the address as typed would miss the Account whenever the
    /// casing differed.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The credential, or <c>null</c> when no Account holds the address.</returns>
    Task<AccountCredential?> FindByEmailAddressAsync(
        string normalizedEmailAddress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rewrites one Account's stored hash at the current work factor.
    /// </summary>
    /// <remarks>
    /// Reached only after a <i>successful</i> verification, so it cannot leak whether an address
    /// exists: the failure path never arrives here. See <c>AuthenticateAccountHandler</c>.
    /// </remarks>
    /// <param name="accountId">The Account whose hash is being upgraded.</param>
    /// <param name="passwordHash">The new hash, made at the configured work factor.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task RehashAsync(Guid accountId, string passwordHash, CancellationToken cancellationToken);
}

/// <summary>
/// One Account's identity and the credential it authenticates with, and nothing else.
/// </summary>
/// <param name="AccountId">The Account's identity - what a Session carries.</param>
/// <param name="PasswordHash">
/// The stored hash, or <c>null</c> for an Account that authenticates some other way. Nullable
/// because <c>Account.PasswordHash</c> is, and for the same reason: an OAuth Account holds no
/// password, and treating that as corruption is the hardening
/// <c>harness-constraints.md:64-66</c> forbids.
/// </param>
public sealed record AccountCredential(Guid AccountId, string? PasswordHash);
