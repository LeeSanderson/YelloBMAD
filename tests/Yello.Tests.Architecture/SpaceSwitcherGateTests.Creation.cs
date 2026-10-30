using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.1's AD-24 gates: the one named creation unit of work that sets both session-context keys (Q1 (a), decided
/// by Lee 2026-10-29), and the AD-24 amendment that names it, recorded rather than left implicit (AC7).
/// </summary>
/// <remarks>
/// <para>
/// <b>RED-PHASE SCAFFOLDS (story 3.1 ATDD run, 2026-10-29).</b> Both are skipped with their AC. The creation step does
/// not exist and the spine has not been amended.
/// </para>
/// <para>
/// <b>Successor, not replacement.</b> <c>No_caller_sets_both_session_context_keys</c> stays active and green today;
/// Task 9 amends it to admit exactly the one caller this file names, and must keep a plant of a second both-keys
/// caller failing. <see cref="Exactly_one_creation_unit_of_work_sets_both_session_context_keys"/> is that amended
/// rule written first, without guessing the step's type name: it asks for exactly one type in the intersection, and
/// that the type is neither the Space resolver nor the switcher's directory.
/// </para>
/// </remarks>
public sealed partial class SpaceSwitcherGateTests
{
    private const string SpaceResolverTypeName = "Yello.Infrastructure.Persistence.MembershipActiveSpaceResolver";

    private static readonly FileInfo ArchitectureSpine = new(Path.Combine(
        RepositoryLayout.Root.FullName,
        "_bmad-output",
        "planning-artifacts",
        "architecture",
        "architecture-YelloBMAD-2026-08-17",
        "ARCHITECTURE-SPINE.md"));

    private static readonly FileInfo ExperienceSpine = new(Path.Combine(
        RepositoryLayout.Root.FullName,
        "_bmad-output",
        "planning-artifacts",
        "ux-designs",
        "ux-YelloBMAD-2026-08-18",
        "EXPERIENCE.md"));

    /// <summary>
    /// AC7, AC11, Q1 (a), AD-2, AD-24 - exactly one production type sets both the <c>SpaceId</c> and the <c>AccountId</c>
    /// keys: the named Space creation unit of work, which counts the caller's own Memberships and inserts the new
    /// Space's rows in one transaction. The resolver still sets only <c>SpaceId</c>; the <c>AccountId</c> key is set by
    /// exactly the switcher's directory and that one creation unit of work.
    /// </summary>
    [Fact(Skip = "red: AC7 - no creation unit of work sets both keys yet (story 3.1 Task 4, Q1 (a))")]
    [Trait("Requirement", "AR-6")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-25")]
    public void Exactly_one_creation_unit_of_work_sets_both_session_context_keys()
    {
        var spaceKeySetters = CallersOf(SessionContextTypeName, "SetAsync");
        var accountKeySetters = CallersOf(SessionContextTypeName, "SetAccountAsync");

        // Vacuity guard: the two existing single-key callers are still found.
        Assert.Contains(SpaceResolverTypeName, spaceKeySetters);
        Assert.Contains(AccountDirectoryTypeName, accountKeySetters);

        var both = spaceKeySetters.Intersect(accountKeySetters, StringComparer.Ordinal).ToList();
        var creation = Assert.Single(both);

        Assert.NotEqual(SpaceResolverTypeName, creation);
        Assert.NotEqual(AccountDirectoryTypeName, creation);
        Assert.Equal(
            new[] { AccountDirectoryTypeName, creation }.Order(StringComparer.Ordinal),
            accountKeySetters.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC7, AR-29, Task 2 - AD-24 carries a dated amendment naming the third Account-scoped unit of work, Space
    /// creation and its own Membership count, and stating the reason in AC7's words: the count is the caller's own,
    /// and the amendment exists so that an unauthorised read is never solved by disabling row-level security, opening
    /// a second connection, or inventing a bypass. It also names the delete-Account-refused read (FR-3, 5.4) and Q5's
    /// decision on the <c>Space</c> and <c>Account</c> policies. The amendment is additive: AD-24's rule line stands.
    /// <c>EXPERIENCE.md</c>'s Spaces-per-Account row says it is recorded.
    /// </summary>
    [Fact(Skip = "red: AC7 - the AD-24 amendment is not recorded yet (story 3.1 Task 2)")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public void The_AD_24_amendment_names_Space_creation_and_states_its_reason()
    {
        Assert.True(ArchitectureSpine.Exists, $"{ArchitectureSpine.FullName} is missing.");

        var spine = File.ReadAllText(ArchitectureSpine.FullName);
        var start = spine.IndexOf("### AD-24", StringComparison.Ordinal);

        Assert.True(start >= 0, "AD-24's heading is not in the spine.");

        var end = spine.IndexOf("\n### ", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? spine[start..] : spine[start..end];

        // Additive: the decision's own heading still says two.
        Assert.Contains("Exactly two surfaces are Account-scoped", section, StringComparison.Ordinal);

        string[] required =
        [
            "Amendment (2026-10-29, story 3.1)",
            "Space creation",
            "row-level security",
            "second connection",
            "bypass",
            "FR-3",
        ];

        Assert.All(required, phrase => Assert.Contains(phrase, section, StringComparison.Ordinal));
        Assert.Contains("`Space`", section, StringComparison.Ordinal);
        Assert.Contains("`Account`", section, StringComparison.Ordinal);

        Assert.True(ExperienceSpine.Exists, $"{ExperienceSpine.FullName} is missing.");
        Assert.Contains("Recorded in AD-24, 2026-10-29", File.ReadAllText(ExperienceSpine.FullName), StringComparison.Ordinal);
    }
}
