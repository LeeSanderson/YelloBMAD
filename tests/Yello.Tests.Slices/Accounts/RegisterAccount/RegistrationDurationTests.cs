using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Domain.Accounts;
using Yello.Domain.Spaces;
using Yello.Infrastructure.Identity;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// AC2's hardest clause: the duplicate path is identical to the new-address path
/// <i>in duration</i>. AD-23, and blocker B3.
/// </summary>
/// <remarks>
/// <para>
/// The method itself is <see cref="DurationIndistinguishability"/> in
/// <c>Yello.Tests.Shared</c> - written once, where stories 1.6 and 1.9 can reuse it, because
/// <c>test-design-architecture.md:113</c> assigns B3 to "stories 1.3 and 1.6" and it blocks P0
/// test I-7. Its sample size, statistic, tolerance and measurement point are documented there,
/// along with the honest statement of what it cannot detect.
/// </para>
/// <para>
/// <b>This runs at Yello's REAL work factor, unlike every other test in this folder.</b> The
/// others drop to the framework default to keep the suite quick, and that is right for them
/// because they assert behaviour. Here the duration is the subject, so using a cheaper hash would
/// measure something the product does not do.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
public sealed class RegistrationDurationTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string Password = "a-password-nobody-else-uses-1!";

    /// <summary>
    /// AC2: identical in duration, because the hash is performed anyway rather than skipped.
    /// </summary>
    [Fact]
    public async Task Registering_a_known_address_takes_as_long_as_registering_a_new_one()
    {
        var database = Available();

        // The known address is registered once up front; every sample in that arm then takes the
        // duplicate path.
        var known = RandomAddress();
        await RegisterAsync(database, known, hash: true);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => RegisterAsync(database, RandomAddress(), hash: true),
            () => RegisterAsync(database, known, hash: true));

        Assert.True(verdict.AreIndistinguishable, verdict.Describe());
    }

    /// <summary>
    /// The planted oracle, kept as a test rather than recorded as a one-off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TESTING-CONVENTIONS.md:93</c>: "An absence assertion must be validated against a planted
    /// signal, or it is not a test." The assertion above says two durations do not differ, and on
    /// its own it would pass just as happily if the method could not detect a difference at all -
    /// if the tolerance were too wide, the sample too small, or the statistic wrong.
    /// </para>
    /// <para>
    /// So this plants the exact defect AD-23 exists to prevent - a registration that skips the
    /// hash - and requires the method to call the two paths apart. Keeping it as a permanent test
    /// rather than a note in a story record is the difference between a validation that was true
    /// once and one that stays true: if a later story widens the tolerance to quiet a flake, this
    /// fails.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_method_detects_a_registration_that_skips_the_hash()
    {
        var database = Available();

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => RegisterAsync(database, RandomAddress(), hash: true),
            () => RegisterAsync(database, RandomAddress(), hash: false));

        Assert.False(
            verdict.AreIndistinguishable,
            "The duration method did not detect a registration that skipped the password hash, " +
            "which is the defect AD-23 exists to prevent. Its sibling test - that the duplicate " +
            $"and new-address paths are indistinguishable - therefore proves nothing. {verdict.Describe()}");
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }

    private static string RandomAddress() => $"person-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// One registration, optionally with the password hash planted out.
    /// </summary>
    private static async Task RegisterAsync(RegistrationDatabase database, string address, bool hash)
    {
        await using var context = database.CreateContext();

        IPasswordHasher hasher = hash
            ? new IdentityPasswordHasher(new PasswordHasher<HashedCredential>(
                Options.Create(new PasswordHasherOptions
                {
                    CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                    IterationCount = PasswordWorkFactor.IterationCount,
                })))
            : new SkippedHasher();

        var handler = new RegisterAccountHandler(
            hasher,
            new AccountRegistrationStore(context),
            new SuffixNaming(),
            new SequentialGuidIdentifierGenerator(),
            TimeProvider.System);

        await handler.HandleAsync(
            new RegisterAccountCommand("Ravi-" + Guid.NewGuid().ToString("N"), address, Password),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The planted defect: a "hasher" that does no work.
    /// </summary>
    /// <remarks>
    /// It still returns something hash-shaped, so the only thing it changes is the time taken -
    /// which is precisely the signal the method under test has to be able to see.
    /// </remarks>
    private sealed class SkippedHasher : IPasswordHasher
    {
        public string Hash(string password) => "not-a-hash";
    }

    private sealed class SuffixNaming : IPersonalSpaceNaming
    {
        public string NameFor(string displayName) => displayName + "'s Space";
    }
}
