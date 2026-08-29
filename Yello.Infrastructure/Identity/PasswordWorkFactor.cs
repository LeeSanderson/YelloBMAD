namespace Yello.Infrastructure.Identity;

/// <summary>
/// NFR-6's work factor: the number, where it came from, and what bounds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>NFR-6 says the work factor "is the architecture's call, not this document's" - and the
/// architecture never made it.</b> <c>test-design-qa.md:486</c> assigns the choice to story 1.3
/// by name and records it as "Currently unspecified". So this constant is a decision, not a
/// default, and it is stated here rather than inline in the composition root so there is one
/// place to change and one place to read.
/// </para>
/// <para>
/// <b>Registration is the one write in Yello that is required to be slow.</b> NFR-5 budgets
/// writes at 500 ms p95 server-side, and AD-23 requires the hash to run even on the duplicate
/// path - because the hash <i>is</i> the duration that makes the two paths indistinguishable.
/// The two requirements pull opposite ways, and <c>quality-budgets.md:48</c> records that NFR-5's
/// own measurement basis, warm or cold, is itself unresolved and owned by the spine.
/// </para>
/// <para>
/// <b>Measured before choosing, on Lee's instruction of 2026-08-28.</b> On a 12th Gen Intel
/// Core i7-12700H (14C/20T, Windows 11, Debug build), after a 20-second burn-in and with 60
/// interleaved samples per candidate: <b>100,000 iterations gave p50 120.1 ms / p95 145.9 ms</b>
/// and <b>220,000 gave p50 272.9 ms / p95 297.7 ms</b>. Cost is linear in the iteration count.
/// The surrounding database work - one transaction, the session-context call and six row inserts
/// - is single-digit milliseconds, so the hash is effectively the whole request.
/// </para>
/// <para>
/// <b>The finding that settles the tension: both requirements hold at once.</b> At 220,000 the
/// server-side p95 is about 300 ms, inside NFR-5's 500 ms write budget with roughly 200 ms to
/// spare - so registration is <i>bounded</i> by NFR-5 and meets it, and needs no exemption. That
/// is worth stating plainly because the story anticipated having to choose between the two.
/// </para>
/// <para>
/// <b>One measurement artefact, recorded because it would mislead anyone repeating this.</b>
/// Whichever candidate was measured first read about 2.5x too fast - 220,000 measured first gave
/// a p50 of 102 ms against 273 ms for the same count measured later in the same process. That is
/// CPU turbo before sustained load pulls the clock down, not a property of the algorithm. An
/// early run without a burn-in produced 45 ms for 100,000, which is not a usable figure and was
/// discarded.
/// </para>
/// <para>
/// <b>The open caveat, and who owns it.</b> This is a fast laptop P-core; an Azure vCPU is
/// typically slower. If the deploy target runs more than about 1.7x slower than this machine,
/// 220,000 crosses the 500 ms budget. <b>Story 1.10 owns the deploy target and re-measures
/// there</b> - and because the count is embedded in every stored hash, lowering it is a
/// configuration change that invalidates nothing.
/// </para>
/// <para>
/// <b>Raising it later costs nothing to existing Accounts.</b> The iteration count is embedded in
/// each stored hash, and <c>VerifyHashedPassword</c> returns <c>SuccessRehashNeeded</c> when the
/// stored count is below the configured one - so the upgrade happens on the next successful
/// sign-in, which is story 1.4's path. That is the whole of NFR-6's "tunable without
/// re-registering existing Accounts", and <c>PasswordWorkFactorTests</c> asserts the mechanism
/// directly rather than trusting this paragraph.
/// </para>
/// </remarks>
public static class PasswordWorkFactor
{
    /// <summary>
    /// The PBKDF2 iteration count Yello hashes at.
    /// </summary>
    /// <remarks>
    /// See the class remarks. This is set explicitly rather than inherited so that
    /// <see cref="FrameworkDefaultIterationCount"/> can never become the answer by accident.
    /// </remarks>
    public static int IterationCount => ChosenIterationCount;

    /// <summary>
    /// ASP.NET Core Identity's own default, and the floor this story would not go below.
    /// </summary>
    /// <remarks>
    /// 100,000 with HMAC-SHA512 since .NET 7, raised from SHA256/10,000. Recorded so the chosen
    /// number can be read as "above the framework default" rather than as an unanchored figure.
    /// </remarks>
    public static int FrameworkDefaultIterationCount => IdentityDefaultIterationCount;

    /// <summary>
    /// OWASP's current recommendation for PBKDF2 with HMAC-SHA512, verified 2026-08-28 against
    /// the Password Storage Cheat Sheet.
    /// </summary>
    /// <remarks>
    /// The SHA-256 figure is 600,000 and does not apply: IdentityV3 uses HMAC-SHA512, and
    /// comparing against the SHA-256 number would set a work factor roughly three times too high
    /// for the algorithm actually in use.
    /// </remarks>
    public static int OwaspSha512Recommendation => OwaspSha512IterationCount;

    // Private constants so the public surface stays properties - S2339 refuses a public const,
    // because a constant is copied into every consumer at compile time and a raised work factor
    // has to reach an assembly that was not rebuilt. These are the only place the digits appear,
    // which is what S109 asks for.
    private const int ChosenIterationCount = 220_000;
    private const int IdentityDefaultIterationCount = 100_000;
    private const int OwaspSha512IterationCount = 220_000;
}
