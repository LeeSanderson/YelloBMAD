using Microsoft.AspNetCore.Identity;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Identity;

/// <summary>
/// <see cref="IPasswordHasher"/> over ASP.NET Core Identity's
/// <see cref="PasswordHasher{TUser}"/>. AD-1, AC5.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the whole of Identity that Yello wires.</b> AD-1 (<c>ARCHITECTURE-SPINE.md:75</c>)
/// says Identity is for the Account store, password hashing and nothing else - and at story 1.3
/// there is no authentication yet, so "nothing else" is currently everything else. There is no
/// <c>AddIdentity</c>, no <c>AddIdentityCore</c>, no <c>IdentityDbContext</c>, no
/// <c>UserManager&lt;&gt;</c> and no <c>SignInManager&lt;&gt;</c>: the composition root registers
/// this one hasher. Story 1.4 owns authentication and adds what it needs then.
/// </para>
/// <para>
/// <b>The work factor is set explicitly rather than inherited.</b> Identity's default is 100,000
/// iterations; leaving it implicit would make NFR-6's "the architecture's call" a framework
/// default nobody chose. See <c>PasswordHashingOptions</c> for the number, the measurement it
/// came from and the hardware it was measured on.
/// </para>
/// <para>
/// <b>IdentityV3, and why the format matters more than the algorithm.</b> The encoded hash
/// carries its own format marker, iteration count, 128-bit salt and 256-bit subkey, so raising
/// the configured count never invalidates a stored hash - <c>VerifyHashedPassword</c> returns
/// <c>SuccessRehashNeeded</c> and story 1.4 rewrites the hash on the next successful sign-in.
/// That is the mechanism NFR-6's "tunable without re-registering existing Accounts" rests on, and
/// this story's obligation is to store hashes in a form that admits it.
/// </para>
/// </remarks>
/// <param name="hasher">Identity's hasher, configured by the composition root.</param>
internal sealed class IdentityPasswordHasher(IPasswordHasher<HashedCredential> hasher)
    : IPasswordHasher
{
    /// <inheritdoc />
    public string Hash(string password) => hasher.HashPassword(HashedCredential.Unread, password);
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
