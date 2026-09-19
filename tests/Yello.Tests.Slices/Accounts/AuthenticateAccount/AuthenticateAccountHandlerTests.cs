using Xunit;
using Yello.Application.Accounts.AuthenticateAccount;
using Yello.Domain.Accounts;

namespace Yello.Tests.Slices.Accounts.AuthenticateAccount;

/// <summary>
/// The authentication slice: what it verifies, what it writes, and what it refuses to tell its
/// caller. FR-2, AD-23 / AR-28, NFR-6.
/// </summary>
/// <remarks>
/// No container and no database. The store is a fake that records what it was asked, because what
/// is under test is the ORDER and the UNCONDITIONALITY of the calls rather than what SQL Server
/// does with them - and those are exactly the properties a database would hide.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-2")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
[Trait("Requirement", "NFR-6")]
public sealed class AuthenticateAccountHandlerTests
{
    private const string Password = "a-password-nobody-else-uses-1!";

    /// <summary>
    /// AC1: a correct credential yields the Account's identity, and nothing else.
    /// </summary>
    [Fact]
    public async Task A_correct_password_authenticates_the_account_it_belongs_to()
    {
        var accountId = Guid.NewGuid();
        var store = new FakeCredentialStore(new AccountCredential(accountId, "stored-hash"));
        var hasher = new FakeHasher(PasswordVerification.Success);

        var authenticated = await new AuthenticateAccountHandler(hasher, store)
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.NotNull(authenticated);
        Assert.Equal(accountId, authenticated.AccountId);
    }

    /// <summary>
    /// AC4: the two causes are one value here, so nothing downstream can tell them apart.
    /// </summary>
    /// <remarks>
    /// The two arms are compared to <i>each other</i> rather than each to <c>null</c>. An assertion
    /// that both are null would still pass if the handler grew a richer result type whose two
    /// failure shapes differed - which is the way this defect would actually arrive, since a
    /// <c>null</c> is only uninformative while it stays a <c>null</c>.
    /// </remarks>
    [Fact]
    public async Task An_unknown_address_and_a_wrong_password_produce_the_same_result()
    {
        var unknown = await new AuthenticateAccountHandler(
                new FakeHasher(PasswordVerification.Failed),
                new FakeCredentialStore(credential: null))
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        var wrongPassword = await new AuthenticateAccountHandler(
                new FakeHasher(PasswordVerification.Failed),
                new FakeCredentialStore(new AccountCredential(Guid.NewGuid(), "stored-hash")))
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.Equal(unknown, wrongPassword);
    }

    /// <summary>
    /// AC4's duration clause, at the point it is decided: an unknown address still reaches the
    /// verifier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the structural half of AC4, and it is the one that can be asserted without a
    /// stopwatch.</b> <c>PasswordVerificationDurationTests</c> measures the consequence; this asserts the cause,
    /// and it fails instantly and deterministically where a timing test needs 21 samples and a
    /// tolerance. The defect it catches is one line - <c>if (credential is null) return null;</c>
    /// above the verification - which is exactly what AD-23 exists to prevent and exactly what a
    /// developer tidying this handler would write.
    /// </para>
    /// <para>
    /// It also asserts the <i>argument</i>: the verifier is handed the null hash rather than being
    /// called with something else to keep it busy. A port whose null-handling is the duration
    /// guarantee has to actually receive the null.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_unknown_address_still_reaches_the_verifier()
    {
        var hasher = new FakeHasher(PasswordVerification.Failed);

        await new AuthenticateAccountHandler(hasher, new FakeCredentialStore(credential: null))
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.Equal(1, hasher.VerifyCalls);
        Assert.Null(hasher.LastVerifiedHash);
    }

    /// <summary>
    /// An Account holding no password at all fails uniformly - it does not throw, and it is not
    /// treated as corruption. NFR-6, and the OAuth assumption this story must leave soft.
    /// </summary>
    /// <remarks>
    /// <c>harness-constraints.md:64-66</c> requires NFR-6 to "tolerate Accounts that hold no
    /// password at all, rather than treating that as an invalid state", because an OAuth Account
    /// has none. This is the case that would otherwise be discovered by the OAuth story as a
    /// <c>NullReferenceException</c> on a sign-in path.
    /// </remarks>
    [Fact]
    public async Task An_account_with_no_password_fails_through_the_same_uniform_path()
    {
        var hasher = new FakeHasher(PasswordVerification.Failed);

        var result = await new AuthenticateAccountHandler(
                hasher,
                new FakeCredentialStore(new AccountCredential(Guid.NewGuid(), PasswordHash: null)))
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.Null(result);

        // And it cost the same: the verifier was still reached, with the null it is built to take.
        Assert.Equal(1, hasher.VerifyCalls);
        Assert.Null(hasher.LastVerifiedHash);
    }

    /// <summary>
    /// NFR-6: a hash made at a lower work factor is rewritten on the next successful sign-in - and
    /// the write actually reaches the store.
    /// </summary>
    /// <remarks>
    /// Story 1.3 proved that a raised work factor leaves an existing hash valid and asks for a
    /// rehash; nothing anywhere acted on that answer, so "tunable without re-registering existing
    /// Accounts" was a mechanism with no consumer. This is the assertion that it has one.
    /// </remarks>
    [Fact]
    public async Task A_hash_made_at_a_lower_work_factor_is_rewritten_on_success()
    {
        var accountId = Guid.NewGuid();
        var store = new FakeCredentialStore(new AccountCredential(accountId, "old-hash"));
        var hasher = new FakeHasher(PasswordVerification.SuccessRehashNeeded);

        var authenticated = await new AuthenticateAccountHandler(hasher, store)
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.NotNull(authenticated);
        Assert.Equal([(accountId, FakeHasher.NewHash)], store.Rehashes);
    }

    [Fact]
    public async Task A_hash_already_at_the_configured_work_factor_is_left_alone()
    {
        var store = new FakeCredentialStore(new AccountCredential(Guid.NewGuid(), "current-hash"));

        await new AuthenticateAccountHandler(new FakeHasher(PasswordVerification.Success), store)
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.Empty(store.Rehashes);
    }

    /// <summary>
    /// A failed sign-in writes nothing at all - which is what keeps the rehash off the path AC4
    /// compares.
    /// </summary>
    /// <remarks>
    /// The rehash is a second slow hash, so a failure path that performed one would be
    /// distinguishable in duration from a failure path that did not - and whether it did would
    /// depend on whether the address existed. That is the oracle by a slower route, and this is the
    /// assertion that closes it.
    /// </remarks>
    [Fact]
    public async Task A_failed_sign_in_writes_nothing()
    {
        var store = new FakeCredentialStore(new AccountCredential(Guid.NewGuid(), "stored-hash"));
        var hasher = new FakeHasher(PasswordVerification.Failed);

        await new AuthenticateAccountHandler(hasher, store)
            .HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.Empty(store.Rehashes);
        Assert.Equal(0, hasher.HashCalls);
    }

    /// <summary>
    /// The address is normalised before it is looked up, so casing does not decide who can sign in.
    /// </summary>
    /// <remarks>
    /// The stored unique index is built on the normalised column, so a lookup by the address as
    /// typed would simply miss the Account whenever the casing differed - which is a sign-in that
    /// fails for a correct password, indistinguishably from a wrong one, and so would be nearly
    /// impossible to diagnose from the outside.
    /// </remarks>
    [Fact]
    public async Task The_address_is_normalised_before_it_is_looked_up()
    {
        var store = new FakeCredentialStore(credential: null);

        await new AuthenticateAccountHandler(new FakeHasher(PasswordVerification.Failed), store)
            .HandleAsync(
                new AuthenticateAccountCommand("  Ravi@Example.Test  ", Password),
                TestContext.Current.CancellationToken);

        Assert.Equal(
            [EmailAddressNormalisation.Normalise("Ravi@Example.Test")],
            store.Lookups);
    }

    private static AuthenticateAccountCommand Command() =>
        new($"person-{Guid.NewGuid():N}@example.test", Password);

    /// <summary>
    /// A hasher that records what it was asked and answers a fixed verdict.
    /// </summary>
    private sealed class FakeHasher(PasswordVerification verdict) : IPasswordHasher
    {
        internal const string NewHash = "rehashed-at-the-current-work-factor";

        public int VerifyCalls { get; private set; }

        public int HashCalls { get; private set; }

        public string? LastVerifiedHash { get; private set; }

        public string Hash(string password)
        {
            HashCalls++;

            return NewHash;
        }

        public PasswordVerification Verify(string? storedHash, string password)
        {
            VerifyCalls++;
            LastVerifiedHash = storedHash;

            return verdict;
        }
    }

    private sealed class FakeCredentialStore(AccountCredential? credential) : IAccountCredentialStore
    {
        public List<string> Lookups { get; } = [];

        public List<(Guid AccountId, string PasswordHash)> Rehashes { get; } = [];

        public Task<AccountCredential?> FindByEmailAddressAsync(
            string normalizedEmailAddress,
            CancellationToken cancellationToken)
        {
            Lookups.Add(normalizedEmailAddress);

            return Task.FromResult(credential);
        }

        public Task RehashAsync(Guid accountId, string passwordHash, CancellationToken cancellationToken)
        {
            Rehashes.Add((accountId, passwordHash));

            return Task.CompletedTask;
        }
    }
}
