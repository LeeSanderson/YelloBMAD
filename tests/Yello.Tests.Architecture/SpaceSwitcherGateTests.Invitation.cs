using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.3's AD-2 / AD-24 gates: the named units of work that set the <c>SpaceId</c> key, now four with the acceptance
/// unit of work (Lee's Q2 (a)), which sets that key alone; and the dated spine amendment that names it (AC12, Task 2), with
/// the additive C1 note on AD-24's 2026-10-29 amendment. Written by the story 4.3 ATDD run, 2026-11-01.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds, activated by the dev phase.</b> The acceptance unit of work is found by shape - the
/// one <c>SpaceId</c>-key setter beyond the three named today whose name contains <c>Invitation</c> - because its name
/// (<c>InvitationScope</c> is the story's example) is the dev phase's to choose.
/// </para>
/// <para>
/// <b>Unchanged and still the guard:</b> <c>No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys</c>
/// and <c>Exactly_one_creation_unit_of_work_sets_both_session_context_keys</c>: the acceptance unit sets no
/// <c>AccountId</c> key, so both stay green unchanged (plant: the acceptance unit also setting <c>AccountId</c>).
/// </para>
/// </remarks>
public sealed partial class SpaceSwitcherGateTests
{
    private const string RegistrationStoreTypeName = "Yello.Infrastructure.Persistence.AccountRegistrationStore";

    /// <summary>
    /// AC12, AD-2, AD-24, Lee's Q2 (a) - the <c>SpaceId</c> session key is set by exactly the named units of work: the
    /// Space resolver (from the route's Space and a Membership), registration (the new Personal Space), Space creation (the
    /// new Space), and the one acceptance unit of work (the Space the token resolved to, server-side). The acceptance unit
    /// sets no <c>AccountId</c> key. Plant 12: a second <c>SpaceId</c>-key setter type.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-6")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-24")]
    public void The_SpaceId_key_is_set_by_exactly_the_named_units_of_work()
    {
        var spaceKeySetters = CallersOf(SessionContextTypeName, "SetAsync");
        var accountKeySetters = CallersOf(SessionContextTypeName, "SetAccountAsync");

        string[] named = [RegistrationStoreTypeName, SpaceResolverTypeName, SpaceCreationUnitOfWorkTypeName];

        // Vacuity guard: the three named today are found.
        Assert.All(named, setter => Assert.Contains(setter, spaceKeySetters));

        var acceptance = Assert.Single(spaceKeySetters.Except(named, StringComparer.Ordinal));

        Assert.Contains("Invitation", acceptance, StringComparison.Ordinal);
        Assert.DoesNotContain(acceptance, accountKeySetters);
        Assert.Equal(4, spaceKeySetters.Count);
    }

    /// <summary>
    /// AC12, Task 2, AD-2, AD-24, AD-28, Lee's Q1 (a) and Q2 (a) - the spine carries a dated amendment by story 4.3 naming the
    /// acceptance unit of work: it sets the <c>SpaceId</c> key from the Space the token resolved to on the server (never a
    /// client-supplied value), it is not an Account-scoped surface, and the token index is a named variance beside
    /// <c>ApiToken.BoundSpaceId</c>; and AD-24's section carries an additive note correcting its 2026-10-29 amendment (C1):
    /// registration-while-accepting is two units of work. AD-24's rule line stands. Sibling of
    /// <c>The_AD_24_amendment_names_Space_creation_and_states_its_reason</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-24")]
    [Trait("Requirement", "AD-28")]
    public void The_spine_amendment_names_the_acceptance_unit_of_work_and_corrects_AD_24()
    {
        Assert.True(ArchitectureSpine.Exists, $"{ArchitectureSpine.FullName} is missing.");

        var spine = File.ReadAllText(ArchitectureSpine.FullName);
        var amendment = spine.Split('\n').Where(line => line.Contains("Amendment (2026-", StringComparison.Ordinal) && line.Contains("story 4.3", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(amendment);

        var text = string.Join('\n', amendment);

        string[] required = ["`SpaceId`", "token", "Account-scoped", "BoundSpaceId", "client-supplied"];

        Assert.All(required, phrase => Assert.Contains(phrase, text, StringComparison.Ordinal));

        var start = spine.IndexOf("### AD-24", StringComparison.Ordinal);
        var end = spine.IndexOf("\n### ", start + 1, StringComparison.Ordinal);
        var ad24 = end < 0 ? spine[start..] : spine[start..end];

        Assert.Contains("Exactly two surfaces are Account-scoped", ad24, StringComparison.Ordinal);
        Assert.Contains("story 4.3", ad24, StringComparison.Ordinal);
        Assert.Contains("two units of work", ad24, StringComparison.Ordinal);
    }
}
