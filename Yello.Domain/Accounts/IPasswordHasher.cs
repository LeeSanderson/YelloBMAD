namespace Yello.Domain.Accounts;

/// <summary>
/// Turns a password into the only form of it Yello ever stores. AC5.
/// </summary>
/// <remarks>
/// <para>
/// A port declared here and implemented in <c>Yello.Infrastructure</c>, because the
/// implementation is ASP.NET Core Identity's <c>PasswordHasher&lt;T&gt;</c> and neither
/// <c>Yello.Domain</c> nor <c>Yello.Application</c> may reference an ASP.NET Core type - a rule
/// Gate B enforces against compiled bytecode rather than against review.
/// </para>
/// <para>
/// <b>The interface is deliberately one-way.</b> There is no <c>Verify</c> member: verification
/// is authentication, which is story 1.4's, and a port that offered it here would invite this
/// story to grow a sign-in path that no acceptance criterion asks for.
/// </para>
/// <para>
/// <b>Hashing is not conditional, and callers must not make it so.</b> AD-23 requires the
/// duplicate-registration path to be indistinguishable from the new-address path in duration as
/// well as in status, body and shape, and the hash is where essentially all of that duration
/// lives. A caller that skips this for a known address reintroduces the timing oracle the
/// decision exists to close - as does a server-side password-policy rejection that returns before
/// reaching here.
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
}
