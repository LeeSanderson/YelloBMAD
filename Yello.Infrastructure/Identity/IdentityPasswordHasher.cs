using Microsoft.AspNetCore.Identity;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Identity;

/// <summary>
/// <see cref="IPasswordHasher"/> over ASP.NET Core Identity's
/// <see cref="PasswordHasher{TUser}"/>. AD-1, AC5.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is still the whole of Identity that Yello wires.</b> AD-1
/// (<c>ARCHITECTURE-SPINE.md:75</c>) says Identity is for the Account store, password hashing and
/// nothing else. Story 1.4 added authentication and took <i>no</i> Identity builder to do it:
/// there is no <c>AddIdentity</c>, no <c>AddIdentityCore</c>, no <c>IdentityDbContext</c>, no
/// <c>UserManager&lt;&gt;</c>, no <c>SignInManager&lt;&gt;</c> and no <c>MapIdentityApi</c>. The
/// Session is a plain cookie-authentication ticket issued by the composition root; the only
/// Identity type in the solution is the hasher this class wraps. See
/// <c>SessionCookiePolicy</c> for why that shape was chosen over
/// <c>AddIdentityCookies</c>.
/// </para>
/// <para>
/// <b>The work factor is set explicitly rather than inherited.</b> Identity's default is 100,000
/// iterations; leaving it implicit would make NFR-6's "the architecture's call" a framework
/// default nobody chose. See <see cref="PasswordWorkFactor"/> for the number, the measurement it
/// came from and the hardware it was measured on. (This pointed at <c>PasswordHashingOptions</c>
/// until code review on 2026-09-10, which is not a type in this solution.)
/// </para>
/// <para>
/// <b>IdentityV3, and why the format matters more than the algorithm.</b> The encoded hash
/// carries its own format marker, iteration count, 128-bit salt and 256-bit subkey, so raising
/// the configured count never invalidates a stored hash - <c>VerifyHashedPassword</c> returns
/// <c>SuccessRehashNeeded</c>, and <see cref="Verify"/> passes that through so
/// <c>AuthenticateAccountHandler</c> can rewrite the hash on the next successful sign-in. That is
/// the mechanism NFR-6's "tunable without re-registering existing Accounts" rests on.
/// </para>
/// <para>
/// <b><see cref="Verify"/> does the work whether or not there is a hash to check against, and
/// that is this class's half of AD-23.</b> See the dummy-hash field's remarks below.
/// </para>
/// </remarks>
/// <param name="hasher">Identity's hasher, configured by the composition root.</param>
internal sealed class IdentityPasswordHasher(IPasswordHasher<HashedCredential> hasher)
    : IPasswordHasher
{
    /// <summary>
    /// A password nobody can submit, hashed once, so that an address holding no credential still
    /// costs a full verification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the whole of AC4's duration clause on the unknown-address arm.</b> Without it
    /// <see cref="Verify"/> would have nothing to spend ~273 ms on when the store returned
    /// <c>null</c>, and the two failure causes would be two different durations no matter how
    /// identical their responses were.
    /// </para>
    /// <para>
    /// <b>Made at the configured work factor, because it is made by the same hasher.</b> A
    /// constant baked into source would be pinned to whatever count was current the day it was
    /// written, and would silently stop matching the real path the moment
    /// <c>PasswordWorkFactor</c> moved - a duration leak that appears at a configuration change
    /// and is invisible in the diff that causes it.
    /// </para>
    /// <para>
    /// <b><c>Lazy</c> rather than eager, and it costs one slow call rather than one slow
    /// startup.</b> The container resolves this class on first use, so an eager hash in the
    /// constructor would land on whichever request resolved it - a registration, as often as a
    /// sign-in. Deferring it puts the cost on the first <i>verification</i> instead, which is the
    /// path that needs it, and <c>DurationIndistinguishability</c> takes a warm-up sample of each
    /// arm outside its measurements for exactly this class of first-call cost.
    /// </para>
    /// </remarks>
    private readonly Lazy<string> _dummyHash = new(
        () => hasher.HashPassword(HashedCredential.Unread, "there-is-no-account-for-this-address"),
        isThreadSafe: true);

    /// <inheritdoc />
    public string Hash(string password) => hasher.HashPassword(HashedCredential.Unread, password);

    /// <inheritdoc />
    public PasswordVerification Verify(string? storedHash, string password)
    {
        var usable = !string.IsNullOrEmpty(storedHash);

        // The work happens either way. Note the ordering: the subject is CHOSEN before the
        // verification and the outcome is DISCARDED after it, so there is no path through this
        // method that performs less work than another.
        var outcome = VerifyOrFail(usable ? storedHash! : _dummyHash.Value, password);

        return usable ? outcome : PasswordVerification.Failed;
    }

    /// <summary>
    /// Runs Identity's verification, treating an unreadable stored hash as a failure rather than
    /// as an exception.
    /// </summary>
    /// <remarks>
    /// <c>VerifyHashedPassword</c> decodes the stored value before it compares anything, so a hash
    /// that is not base64 or not one of the two supported formats throws rather than returning
    /// <c>Failed</c>. Nothing in this solution can write such a value - every hash comes from
    /// <see cref="Hash"/> - so this catch is for a row corrupted or hand-edited outside the
    /// application. It is reported as an ordinary failed sign-in because that is what it is from
    /// the outside; the alternative is a 500 that tells an unauthenticated caller their address
    /// exists and its row is broken.
    /// <para>
    /// <b>The one duration asymmetry in this class, stated rather than hidden:</b> this path
    /// returns early and is therefore fast. It is unreachable for any Account this product created,
    /// and an attacker cannot reach it for an address they do not already control the storage of.
    /// </para>
    /// </remarks>
    private PasswordVerification VerifyOrFail(string storedHash, string password)
    {
        try
        {
            return Map(hasher.VerifyHashedPassword(HashedCredential.Unread, storedHash, password));
        }
        catch (FormatException)
        {
            return PasswordVerification.Failed;
        }
    }

    private static PasswordVerification Map(PasswordVerificationResult result) => result switch
    {
        PasswordVerificationResult.Success => PasswordVerification.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
        _ => PasswordVerification.Failed,
    };
}

/// <summary>
/// The type argument <see cref="PasswordHasher{TUser}"/> requires and never reads.
/// </summary>
/// <remarks>
/// <para>
/// <c>PasswordHasher&lt;TUser&gt;</c> is generic over the account type and uses it for nothing:
/// <c>HashPassword</c> reads only the password. So the argument has to be <i>something</i>, and
/// the two tempting choices are both worse than this one. Passing <c>null!</c> makes correctness
/// depend on an implementation detail of a framework class - true today, unannounced if it
/// changes. Using <c>Account</c> requires constructing one before the hash exists, which is
/// backwards: the hash is what the Account is built <i>with</i>, and the duplicate-registration
/// path hashes without ever having an Account at all.
/// </para>
/// <para>
/// A dedicated type says what is actually true - the parameter is structural, not meaningful -
/// and costs one class.
/// </para>
/// </remarks>
internal sealed class HashedCredential
{
    /// <summary>
    /// The single instance passed to the parameter the hasher never reads.
    /// </summary>
    /// <remarks>
    /// One instance rather than one per call: the hasher does not look at it, so allocating a
    /// fresh object per registration would be waste with no meaning attached. It also keeps the
    /// class non-empty, which S2094 requires and which is fair - a type that carries nothing at
    /// all usually wants to be an interface.
    /// </remarks>
    internal static HashedCredential Unread { get; } = new();
}
