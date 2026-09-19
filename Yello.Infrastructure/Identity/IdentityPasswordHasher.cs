using System.Text;
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
    /// <b><c>Lazy</c> rather than eager, and the composition root forces it at startup.</b> The
    /// container resolves this class on first use, so an eager hash in the constructor would land on
    /// whichever request resolved it - a registration, as often as a sign-in. Laziness alone,
    /// however, put a measurable cost on the first unknown-address verification in each process:
    /// that request performs a full <c>HashPassword</c> (~273 ms) <i>in addition to</i> the
    /// verification that follows, while a known address on the same cold process pays one. One ~2x
    /// sample per process, on the exact axis AC4 requires to be indistinguishable, and
    /// <c>DurationIndistinguishability</c>'s per-arm warm-up is precisely why no test could see it -
    /// the warm-up hides the only occurrence. Found at code review on 2026-09-19. <see cref="Warm"/>
    /// is called by <c>Yello.Host</c>'s composition root so the first real request finds this
    /// materialised; the field stays <c>Lazy</c> so that a caller who forgets is merely slow rather
    /// than wrong.
    /// </para>
    /// </remarks>
    private readonly Lazy<string> _dummyHash = new(
        () => hasher.HashPassword(HashedCredential.Unread, Normalise("there-is-no-account-for-this-address")),
        isThreadSafe: true);

    /// <inheritdoc />
    public string Hash(string password) =>
        hasher.HashPassword(HashedCredential.Unread, Normalise(password));

    /// <inheritdoc />
    public PasswordVerification Verify(string? storedHash, string password)
    {
        var usable = !string.IsNullOrEmpty(storedHash);

        // The work happens either way. Note the ordering: the subject is CHOSEN before the
        // verification and the outcome is DISCARDED after it, so there is no path through this
        // method that performs less work than another.
        var outcome = VerifyOrFail(usable ? storedHash! : _dummyHash.Value, Normalise(password));

        return usable ? outcome : PasswordVerification.Failed;
    }

    /// <summary>
    /// Materialises the dummy hash, so that the first unknown-address verification in this process
    /// is not the request that pays for it.
    /// </summary>
    /// <remarks>
    /// Idempotent and thread-safe - <c>Lazy</c> with <c>isThreadSafe: true</c> guarantees the
    /// factory runs once however many callers arrive together. Costs one full hash (~273 ms) at
    /// startup, which is the trade this deliberately takes: one slow startup rather than one slow
    /// request on the path AC4 measures.
    /// </remarks>
    internal void Warm() => _ = _dummyHash.Value;

    /// <summary>
    /// The exact bytes that reach PBKDF2, for both hashing and verification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NFKC, per NIST SP 800-63B §5.1.1.2.</b> A password typed with a composed accent on macOS
    /// (NFD: <c>e</c> + U+0301) and re-typed on Windows (NFC: U+00E9) is the same password to the
    /// person and two different byte sequences to PBKDF2, so verification fails and the person meets
    /// the deliberately uninformative uniform 401 forever - with no password reset anywhere in epic
    /// 1 to recover through. <c>EmailAddressNormalisation</c> has composed the other credential
    /// field since story 1.3 for exactly this reason; the reasoning was never extended to this one.
    /// Found at code review on 2026-09-19.
    /// </para>
    /// <para>
    /// <b>Why this had to be taken now rather than recorded.</b> Normalising changes the bytes fed
    /// to PBKDF2, so it invalidates any stored hash whose password carries combining characters -
    /// and that is a migration nobody can perform, because the hash is one-way and the plaintext is
    /// gone. Story 1.10 has not run, so there are no production Accounts and the change costs
    /// nothing today. It becomes permanent and unfixable the moment Yello deploys.
    /// </para>
    /// <para>
    /// <b>Compatibility composition rather than <c>FormC</c>.</b> NFKC also folds full-width forms
    /// and ligatures, so visually identical passwords typed through different input methods agree.
    /// It costs a little entropy and buys cross-platform reliability, which is the trade NIST makes
    /// and the reason this differs from the email path's <c>FormC</c>.
    /// </para>
    /// </remarks>
    /// <param name="password">The password as it was typed.</param>
    /// <returns>The normalised password.</returns>
    private static string Normalise(string password) => password.Normalize(NormalizationForm.FormKC);

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
