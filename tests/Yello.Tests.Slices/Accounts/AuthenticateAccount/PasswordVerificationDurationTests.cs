using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;
using Yello.Application.Accounts.AuthenticateAccount;
using Yello.Domain.Accounts;
using Yello.Infrastructure.Identity;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Accounts.AuthenticateAccount;

/// <summary>
/// AC4's hardest clause, measured where the duration actually lives: an unknown address costs what
/// a wrong password costs. AD-23 / AR-28, and blocker B3.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO DATABASE FIXTURE, AND THAT IS THE POINT OF THIS CLASS.</b> xunit constructs a class
/// fixture before any test in the class runs, so a container-backed class fails entirely when the
/// runtime is down - including cases that never touch a database. Story 1.3 split
/// <c>DurationIndistinguishabilityTests</c> from <c>RegistrationDurationTests</c> for exactly this
/// reason, and the story asks for the same split here. The whole of AC4's duration guarantee is
/// decided by <see cref="IdentityPasswordHasher.Verify"/> doing the work when there is no hash to
/// check, and that needs no rows.
/// </para>
/// <para>
/// <b>At Yello's real work factor, unlike most tests in this folder.</b> The others drop to a
/// cheaper hash because they assert behaviour; here the duration <i>is</i> the subject, so a
/// cheaper hash would measure something the product does not do.
/// </para>
/// <para>
/// The method is <see cref="DurationIndistinguishability"/> in <c>Yello.Tests.Shared</c> - 21
/// samples per arm, interleaved, the median, 20% of the slower median as the tolerance, measured
/// server-side. Its four decisions and the honest statement of what it cannot detect are
/// documented there. It is used rather than improvised, per
/// <c>TESTING-CONVENTIONS.md:189</c>.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
[Trait("Requirement", "FR-2")]
[Collection(nameof(PasswordDurationCollection))]
public sealed class PasswordVerificationDurationTests
{
    private const string CorrectPassword = "a-password-nobody-else-uses-1!";
    private const string WrongPassword = "a-different-password-entirely-2?";

    /// <summary>
    /// AC4: verifying against no stored hash takes as long as verifying against one that does not
    /// match.
    /// </summary>
    [Fact]
    public async Task An_unknown_address_costs_what_a_wrong_password_costs()
    {
        var hasher = RealHasher();
        var storedHash = hasher.Hash(CorrectPassword);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => VerifyAsync(hasher, storedHash, WrongPassword),
            () => VerifyAsync(hasher, storedHash: null, WrongPassword));

        Assert.True(verdict.AreIndistinguishable, verdict.Describe());
    }

    /// <summary>
    /// The planted oracle, kept as a permanent test rather than recorded as a one-off run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TESTING-CONVENTIONS.md:101</c>: "An absence assertion must be validated against a planted
    /// signal, or it is not a test." The assertion above says two durations do not differ, and on
    /// its own it would pass just as happily if the method could not detect a difference at all.
    /// </para>
    /// <para>
    /// So this plants the exact defect AD-23 exists to prevent on this path - a verifier that
    /// returns early when there is no stored hash, which is
    /// <c>if (account is null) return Fail();</c> written one layer down - and requires the method
    /// to tell the two arms apart. Keeping it as a test rather than a note is what stops a later
    /// story widening the tolerance to quiet a flake and silently disarming the assertion beside
    /// it.
    /// </para>
    /// <para>
    /// <b>What this plant does NOT protect, stated as story 1.3's equivalent learned to state
    /// it:</b> the planted arm skips the verification entirely, so the two medians differ by
    /// roughly 99% - five times the 20% tolerance. It proves the method can see something enormous
    /// and says nothing about where the line is.
    /// <c>DurationIndistinguishabilityTests</c> is what pins the constants and the boundary.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_method_detects_a_verifier_that_returns_early_when_there_is_no_hash()
    {
        var hasher = RealHasher();
        var storedHash = hasher.Hash(CorrectPassword);
        var planted = new ShortCircuitingHasher(hasher);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => VerifyAsync(planted, storedHash, WrongPassword),
            () => VerifyAsync(planted, storedHash: null, WrongPassword));

        Assert.False(
            verdict.AreIndistinguishable,
            "The duration method did not detect a verifier that skips the work when there is no "
            + "stored hash, which is the timing oracle AD-23 exists to close. Its sibling test - "
            + "that an unknown address and a wrong password are indistinguishable - therefore "
            + $"proves nothing. {verdict.Describe()}");
    }

    /// <summary>
    /// The handler hands the verifier a null and lets it do the work, rather than branching first.
    /// </summary>
    /// <remarks>
    /// The two tests above measure the <i>hasher</i>. This one closes the gap above it: a handler
    /// that checked for null before calling would make both arms above irrelevant, because the
    /// unknown-address arm would never reach the code they measure. Asserted structurally here and
    /// by <c>AuthenticateAccountHandlerTests</c> rather than by a second stopwatch, because a
    /// branch is a fact about the code and does not need 21 samples to establish.
    /// </remarks>
    [Fact]
    public async Task The_handler_reaches_the_verifier_for_an_address_that_does_not_exist()
    {
        var hasher = new CountingHasher(RealHasher());

        var result = await new AuthenticateAccountHandler(hasher, new EmptyCredentialStore())
            .HandleAsync(
                new AuthenticateAccountCommand("nobody@example.test", WrongPassword),
                TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(1, hasher.VerifyCalls);
    }

    /// <summary>
    /// The work factor may not be raised until the timing oracle that a raise opens has a remedy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS IS A TRIPWIRE, NOT A MEASUREMENT, AND IT GUARDS THE ONE THING THE DURATION SUITE
    /// CANNOT SEE.</b> PBKDF2's cost follows the iteration count embedded in the STORED hash. The
    /// dummy hash an unknown address is verified against is minted by the same hasher at the
    /// CONFIGURED count. Those are equal only while every stored hash is already current - which is
    /// precisely the condition <c>SuccessRehashNeeded</c> and the whole rehash path exist to
    /// violate.
    /// </para>
    /// <para>
    /// Raise <see cref="PasswordWorkFactor.IterationCount"/> and every Account that has not signed
    /// in since verifies at the OLD count while an unknown address pays the NEW one - so a known
    /// address answers measurably FASTER, which is AD-23 and AC4 inverted. It does not decay,
    /// because the rehash only fires on the success path: an attacker probing with wrong passwords
    /// never upgrades the row. Account enumeration by stopwatch, for as long as the migration window
    /// lasts.
    /// </para>
    /// <para>
    /// <b>Why a constant rather than a duration comparison.</b>
    /// <see cref="A_legacy_factor_hash_costs_less_than_the_dummy_and_that_is_the_oracle"/> below
    /// measures the asymmetry and is skipped, because it fails today by design. This one passes
    /// today and fails the moment somebody changes the number - which is the only moment that
    /// matters, and the moment nothing else in the suite notices. Added at code review on
    /// 2026-09-19; the remedy is recorded in <c>deferred-work.md</c> under this story's review.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_work_factor_has_not_been_raised_without_closing_the_timing_oracle()
    {
        Assert.Equal(220_000, PasswordWorkFactor.IterationCount);
    }

    /// <summary>
    /// The oracle itself, measured. Skipped because it fails today, deliberately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a demonstration rather than a guard, and un-skipping it is the acceptance test for
    /// the remedy.</b> It plants a stored hash at the framework default (100,000) - the arrangement
    /// <c>RehashIntegrationTests</c> already uses, which existed all along and which no duration
    /// test ever borrowed - and compares verifying it against the unknown-address arm at the
    /// configured 220,000. The two differ by roughly 2.2x, eleven times the method's own 20%
    /// tolerance.
    /// </para>
    /// <para>
    /// <b>It is skipped rather than deleted or inverted</b> because a red suite is not a record and
    /// an inverted assertion ("the arms DO differ") would have to be inverted back by whoever fixes
    /// this - and would pass for the wrong reason the day a fixed-cost budget makes them equal. Skip
    /// means: this is the behaviour we know we have, named, with the ledger entry beside it.
    /// </para>
    /// </remarks>
    [Fact(Skip = "Fails by design: demonstrates the work-factor timing oracle. Remove the Skip when "
        + "the remedy lands - see deferred-work.md, code review of story 1.4 (2026-09-19).")]
    public async Task A_legacy_factor_hash_costs_less_than_the_dummy_and_that_is_the_oracle()
    {
        var current = RealHasher();
        var legacy = HasherAt(PasswordWorkFactor.FrameworkDefaultIterationCount);
        var legacyHash = legacy.Hash(CorrectPassword);

        var verdict = await DurationIndistinguishability.CompareAsync(
            () => VerifyAsync(current, legacyHash, WrongPassword),
            () => VerifyAsync(current, storedHash: null, WrongPassword));

        Assert.True(
            verdict.AreIndistinguishable,
            "A stored hash at an older work factor costs less to verify than the dummy hash an "
            + "unknown address is checked against, so the two causes AC4 requires to be "
            + $"indistinguishable are not. {verdict.Describe()}");
    }

    private static IdentityPasswordHasher RealHasher() => HasherAt(PasswordWorkFactor.IterationCount);

    private static IdentityPasswordHasher HasherAt(int iterationCount) =>
        new(new PasswordHasher<HashedCredential>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = iterationCount,
        })));

    private static Task VerifyAsync(IPasswordHasher hasher, string? storedHash, string password)
    {
        _ = hasher.Verify(storedHash, password);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The planted defect: a verifier that does no work when there is no hash to check against.
    /// </summary>
    /// <remarks>
    /// It still answers <c>Failed</c>, which is the correct verdict - so the only thing it changes
    /// is the time taken, which is precisely the signal the method under test has to be able to
    /// see.
    /// </remarks>
    private sealed class ShortCircuitingHasher(IPasswordHasher inner) : IPasswordHasher
    {
        public string Hash(string password) => inner.Hash(password);

        public PasswordVerification Verify(string? storedHash, string password) =>
            storedHash is null
                ? PasswordVerification.Failed
                : inner.Verify(storedHash, password);
    }

    private sealed class CountingHasher(IPasswordHasher inner) : IPasswordHasher
    {
        public int VerifyCalls { get; private set; }

        public string Hash(string password) => inner.Hash(password);

        public PasswordVerification Verify(string? storedHash, string password)
        {
            VerifyCalls++;

            return inner.Verify(storedHash, password);
        }
    }

    private sealed class EmptyCredentialStore : IAccountCredentialStore
    {
        public Task<AccountCredential?> FindByEmailAddressAsync(
            string normalizedEmailAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccountCredential?>(null);

        public Task RehashAsync(Guid accountId, string passwordHash, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

/// <summary>
/// The password-verification duration cases run alone. Sharing the CPU with the rest of the slice
/// suite pushed the median gap past the 20% tolerance on full runs (story 2.7 verification,
/// 2026-10-28) while the class passed alone 3/3 - the CLAUDE.md timing-test rule.
/// </summary>
[CollectionDefinition(nameof(PasswordDurationCollection), DisableParallelization = true)]
public sealed class PasswordDurationCollection;
