namespace Yello.Domain.Accounts;

/// <summary>
/// Turns a password into the only form of it Yello ever stores, and checks one against that
/// form. AC5 (story 1.3), AC1 and AC4 (story 1.4).
/// </summary>
/// <remarks>
/// <para>
/// A port declared here and implemented in <c>Yello.Infrastructure</c>, because the
/// implementation is ASP.NET Core Identity's <c>PasswordHasher&lt;T&gt;</c> and neither
/// <c>Yello.Domain</c> nor <c>Yello.Application</c> may reference an ASP.NET Core type - a rule
/// Gate B enforces against compiled bytecode rather than against review.
/// </para>
/// <para>
/// <b><see cref="Verify"/> arrived with story 1.4, which is authentication.</b> Story 1.3 left
/// this interface deliberately one-way and recorded why: "a port that offered it here would
/// invite this story to grow a sign-in path that no acceptance criterion asks for". That
/// reasoning held for 1.3 and is spent - 1.4's AC1 is a sign-in path, so the member exists now.
/// </para>
/// <para>
/// <b>Hashing is not conditional, and callers must not make it so.</b> AD-23 requires the
/// duplicate-registration path to be indistinguishable from the new-address path in duration as
/// well as in status, body and shape, and the hash is where essentially all of that duration
/// lives. A caller that skips this for a known address reintroduces the timing oracle the
/// decision exists to close - as does a server-side password-policy rejection that returns before
/// reaching here.
/// </para>
/// <para>
/// <b>Verification is not conditional either, and that is why <see cref="Verify"/> takes a
/// nullable hash rather than being called only when one exists.</b> AD-23's uniformity covers
/// sign-in as squarely as registration: an unknown address must cost what a known one costs, or
/// the two failure causes are told apart by a stopwatch. Giving the parameter a null to accept
/// is what lets the implementation do the work anyway; a signature taking a non-null hash would
/// push every caller into the <c>if (account is null) return Fail();</c> that AD-23 exists to
/// catch.
/// </para>
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password under the configured work factor.
    /// </summary>
    /// <param name="password">The password as submitted. Never stored, never logged.</param>
    /// <returns>
    /// The encoded hash, which carries its own format marker, salt and iteration count - the
    /// property NFR-6's "tunable without re-registering existing Accounts" rests on.
    /// </returns>
    string Hash(string password);

    /// <summary>
    /// Checks a submitted password against a stored hash, performing the work whether or not
    /// there is a hash to check it against.
    /// </summary>
    /// <param name="storedHash">
    /// The stored hash, or <c>null</c> when there is none - because no Account holds the address,
    /// or because the Account holds no password at all. <c>Account.PasswordHash</c> is nullable and
    /// <c>harness-constraints.md:65</c> requires NFR-6 to "tolerate Accounts that hold no password
    /// at all, rather than treating that as an invalid state", so a null here is an ordinary
    /// argument rather than a programming error.
    /// </param>
    /// <param name="password">The password as submitted. Never stored, never logged.</param>
    /// <returns>
    /// Three states rather than two. <see cref="PasswordVerification.SuccessRehashNeeded"/> is what
    /// makes NFR-6's "work factor tunable without re-registering existing Accounts" real: the
    /// stored hash carries the count it was made at, and a correct password hashed below the
    /// configured count is still correct - it just wants rewriting.
    /// </returns>
    PasswordVerification Verify(string? storedHash, string password);
}

/// <summary>
/// What checking a password against a stored hash concluded.
/// </summary>
/// <remarks>
/// <b>Three states, not a <c>bool</c>.</b> Collapsing the last two would silently discard NFR-6's
/// upgrade path - the count embedded in an old hash would never be raised, and the work factor
/// would be "tunable" only for Accounts registered after the change. The distinction is also
/// invisible to the caller's <i>response</i>: both success states answer identically on the wire,
/// which is AD-23, and only the stored hash differs afterwards.
/// </remarks>
public enum PasswordVerification
{
    /// <summary>
    /// The password does not match, or there was no hash to match it against. <b>One value for
    /// both</b>, so no caller can tell an unknown address from a wrong password by reading this.
    /// </summary>
    Failed,

    /// <summary>
    /// The password matches, and the stored hash is already at the configured work factor.
    /// </summary>
    Success,

    /// <summary>
    /// The password matches, and the stored hash was made at a lower work factor than the one
    /// configured now. The caller rewrites it - see <c>AuthenticateAccountHandler</c>.
    /// </summary>
    SuccessRehashNeeded,
}
