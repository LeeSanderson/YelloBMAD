using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using Yello.Application.Accounts.AuthenticateAccount;
using Yello.Domain.Accounts;
using Yello.Infrastructure.Identity;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Accounts.AuthenticateAccount;

/// <summary>
/// NFR-6's "work factor tunable without re-registering existing Accounts", proved against a real
/// datastore rather than against a fake.
/// </summary>
/// <remarks>
/// <para>
/// <b>Story 1.3 proved the mechanism and could not prove the consequence.</b> Its
/// <c>Raising_the_work_factor_leaves_an_existing_hash_valid_and_asks_for_a_rehash</c> shows that
/// <c>VerifyHashedPassword</c> answers <c>SuccessRehashNeeded</c> - but nothing in the product
/// could act on that answer, because there was no sign-in path. This story makes it reachable, and
/// this is the assertion that the upgrade actually <i>lands in the column</i>. Without it, "tunable
/// without re-registering" would still be a property of the framework rather than of Yello.
/// </para>
/// <para>
/// <b>The Account is written directly rather than through registration</b>, because registration
/// hashes at the configured work factor and the whole point here is a hash made at a lower one.
/// <c>Account</c> carries no row-level security policy - <c>deferred-work.md</c> records that, and
/// the AD-24 amendment due before Epic 3 owns the question - so no session context is needed to
/// insert one, and this test writes nothing that does need one.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-6")]
[Trait("Requirement", "FR-2")]
public sealed class RehashIntegrationTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string Password = "a-password-nobody-else-uses-1!";

    /// <summary>
    /// A hash made below the configured work factor is rewritten, in the datastore, on the next
    /// successful sign-in.
    /// </summary>
    /// <remarks>
    /// <b>Three assertions, and the third is what makes the first two more than a value changing.</b>
    /// The stored hash differs from the planted one; it verifies the same password; and it no longer
    /// asks to be rehashed. Without the third, a rehash that wrote a hash at the <i>same</i> low
    /// work factor would pass - the value would have changed, the password would still verify, and
    /// the upgrade would never converge.
    /// </remarks>
    [Fact]
    public async Task A_hash_made_at_a_lower_work_factor_is_rewritten_in_the_datastore()
    {
        var database = Available();
        var address = RandomAddress();
        var accountId = Guid.CreateVersion7();

        var weakHasher = HasherAt(PasswordWorkFactor.FrameworkDefaultIterationCount);
        var plantedHash = weakHasher.Hash(Password);

        await using (var context = database.CreateContext())
        {
            context.Accounts.Add(new Account
            {
                Id = accountId,
                EmailAddress = address,
                NormalizedEmailAddress = EmailAddressNormalisation.Normalise(address),
                DisplayName = "Ravi",
                PasswordHash = plantedHash,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The hash is below the configured count, so the real hasher asks for a rewrite. Asserted
        // rather than assumed: if this were Success, the test below would pass for the wrong reason.
        var currentHasher = HasherAt(PasswordWorkFactor.IterationCount);

        Assert.Equal(
            PasswordVerification.SuccessRehashNeeded,
            currentHasher.Verify(plantedHash, Password));

        await using (var context = database.CreateContext())
        {
            var authenticated = await new AuthenticateAccountHandler(
                    currentHasher,
                    new AccountCredentialStore(context))
                .HandleAsync(
                    new AuthenticateAccountCommand(address, Password),
                    TestContext.Current.CancellationToken);

            Assert.NotNull(authenticated);
            Assert.Equal(accountId, authenticated.AccountId);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Accounts
                .AsNoTracking()
                .Where(account => account.Id == accountId)
                .Select(account => account.PasswordHash)
                .SingleAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(stored);
            Assert.NotEqual(plantedHash, stored);

            // The same password, and no longer asking to be rewritten - so the upgrade converges
            // rather than rewriting the same weak hash on every sign-in.
            Assert.Equal(PasswordVerification.Success, currentHasher.Verify(stored, Password));
        }
    }

    /// <summary>
    /// A hash already at the configured work factor is not rewritten.
    /// </summary>
    /// <remarks>
    /// The other half of convergence, and the one that keeps an ordinary sign-in from performing a
    /// second slow hash and a write on every request.
    /// </remarks>
    [Fact]
    public async Task A_current_hash_is_left_exactly_as_it_was()
    {
        var database = Available();
        var address = RandomAddress();
        var accountId = Guid.CreateVersion7();

        var currentHasher = HasherAt(PasswordWorkFactor.IterationCount);
        var storedHash = currentHasher.Hash(Password);

        await using (var context = database.CreateContext())
        {
            context.Accounts.Add(new Account
            {
                Id = accountId,
                EmailAddress = address,
                NormalizedEmailAddress = EmailAddressNormalisation.Normalise(address),
                DisplayName = "Ravi",
                PasswordHash = storedHash,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            await new AuthenticateAccountHandler(currentHasher, new AccountCredentialStore(context))
                .HandleAsync(
                    new AuthenticateAccountCommand(address, Password),
                    TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var afterwards = await context.Accounts
                .AsNoTracking()
                .Where(account => account.Id == accountId)
                .Select(account => account.PasswordHash)
                .SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(storedHash, afterwards);
        }
    }

    /// <summary>
    /// An Account that holds no password at all fails uniformly, against a real row.
    /// </summary>
    /// <remarks>
    /// <c>harness-constraints.md:65</c> requires NFR-6 to "tolerate Accounts that hold no password
    /// at all, rather than treating that as an invalid state", because an OAuth Account has none -
    /// and OAuth sign-in is the selected mid-flight change, scheduled to fire once this epic ships.
    /// Asserted against the database as well as against a fake, because "the column is nullable" and
    /// "a null in the column is handled" are different claims.
    /// </remarks>
    [Fact]
    public async Task An_account_stored_with_no_password_fails_rather_than_throwing()
    {
        var database = Available();
        var address = RandomAddress();

        await using (var context = database.CreateContext())
        {
            context.Accounts.Add(new Account
            {
                Id = Guid.CreateVersion7(),
                EmailAddress = address,
                NormalizedEmailAddress = EmailAddressNormalisation.Normalise(address),
                DisplayName = "Ravi",
                PasswordHash = null,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var authenticated = await new AuthenticateAccountHandler(
                    HasherAt(PasswordWorkFactor.IterationCount),
                    new AccountCredentialStore(context))
                .HandleAsync(
                    new AuthenticateAccountCommand(address, Password),
                    TestContext.Current.CancellationToken);

            Assert.Null(authenticated);
        }
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }

    private static string RandomAddress() => $"person-{Guid.NewGuid():N}@example.test";

    private static IdentityPasswordHasher HasherAt(int iterationCount) =>
        new(new PasswordHasher<HashedCredential>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = iterationCount,
        })));
}
