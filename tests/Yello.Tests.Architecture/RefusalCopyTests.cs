using System.Globalization;
using System.Resources;
using Xunit;
using Yello.Client.Localisation;
using Yello.Client.Refusals;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;

namespace Yello.Tests.Architecture;

/// <summary>
/// The client half of the refusal contract: which of its own strings a refusal renders with, and
/// that every code the Host can send has one. AC9; UX-DR32, UX-DR35.
/// </summary>
/// <remarks>
/// Here rather than in a browser suite because this is the only test project that references
/// <c>Yello.Client</c> (the <c>SessionSeamTests</c> precedent). What a browser would add - that the
/// two not-found pages render identically - follows from both resolving to one key, which is what is
/// asserted; the browser case itself is ledgered (story 1.6 Task 10).
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR32")]
[Trait("Requirement", "UX-DR35")]
[Trait("Requirement", "AR-34")]
public sealed class RefusalCopyTests
{
    private static readonly ResourceManager Copy = new(ClientCopy.ResourceName, typeof(ClientCopy).Assembly);

    /// <summary>
    /// Every not-found - an unroutable address, the boundary's <c>not-available</c>, and a problem the
    /// client does not recognise - renders with one and the same resource, so no client-side branch
    /// can tell the causes apart.
    /// </summary>
    [Fact]
    public void Every_not_found_cause_renders_the_same_refusal()
    {
        ProblemResponse?[] causes =
        [
            null,
            new ProblemResponse(ProblemTypes.NotAvailable, Errors: null),
            new ProblemResponse("urn:yello:problem:something-new", Errors: null),
            new ProblemResponse(ProblemTypes.BoundReached, Errors: null),
        ];

        // Anchored: a 403 resolves to something else, so the agreement below is not a function that
        // ignores its input.
        Assert.NotEqual(
            RefusalCopy.NotAvailableKey,
            RefusalCopy.For(new ProblemResponse(ProblemTypes.InsufficientRole, null, Capability: "manage-tasks")).Key);

        Assert.All(causes, cause => Assert.Equal(new RefusalMessage(RefusalCopy.NotAvailableKey, Limit: null), RefusalCopy.For(cause)));

        Assert.Equal("Not available.", Copy.GetString(RefusalCopy.NotAvailableKey, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A <c>403</c> renders the capability statement keyed on its code - "Viewers cannot edit Tasks." -
    /// and a bound refusal states its number.
    /// </summary>
    [Fact]
    public void A_role_refusal_names_the_capability_and_a_bound_refusal_states_its_number()
    {
        var role = RefusalCopy.For(new ProblemResponse(ProblemTypes.InsufficientRole, null, Capability: StableCode.Of(Capability.ManageTasks)));
        var bound = RefusalCopy.For(new ProblemResponse(ProblemTypes.BoundReached, null, Bound: StableCode.Of(Bound.MembershipsPerSpace), Limit: 100));

        Assert.Equal("Viewers cannot edit Tasks.", Copy.GetString(role.Key, CultureInfo.InvariantCulture));

        Assert.Equal(100, bound.Limit);
        Assert.Equal(
            "This Space has 100 Memberships, the maximum.",
            string.Format(CultureInfo.InvariantCulture, Copy.GetString(bound.Key, CultureInfo.InvariantCulture)!, bound.Limit));
    }

    /// <summary>
    /// A <c>403</c> whose capability code the client has no copy for - a server newer than a cached
    /// client - renders the generic capability statement, never the bare code.
    /// </summary>
    [Fact]
    public void A_capability_code_without_client_copy_renders_the_generic_capability_statement()
    {
        static bool HasCopy(string key) => Copy.GetString(key, CultureInfo.InvariantCulture) is not null;

        // Anchored: a known code keeps its own statement through the same overload.
        Assert.Equal(
            StableCode.Of(Capability.ManageTasks),
            RefusalCopy.For(new ProblemResponse(ProblemTypes.InsufficientRole, null, Capability: StableCode.Of(Capability.ManageTasks)), HasCopy).Key);

        var unknown = RefusalCopy.For(
            new ProblemResponse(ProblemTypes.InsufficientRole, null, Capability: "a-capability-from-a-newer-server"),
            HasCopy);

        Assert.Equal(RefusalCopy.RoleDoesNotPermitKey, unknown.Key);
        Assert.NotNull(Copy.GetString(unknown.Key, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Every capability and every bound the Host can name in a refusal has client copy behind its
    /// code, so the interface never renders a bare code.
    /// </summary>
    /// <remarks>
    /// The codes are derived from the enums, not listed, so a capability or bound added tomorrow is
    /// covered by this test without anyone extending it.
    /// </remarks>
    [Fact]
    public void Every_capability_and_bound_code_has_client_copy()
    {
        var codes = Enum.GetValues<Capability>().Select(StableCode.Of)
            .Concat(Enum.GetValues<Bound>().Select(StableCode.Of))
            .ToList();

        Assert.Equal(Enum.GetValues<Capability>().Length + Enum.GetValues<Bound>().Length, codes.Count);

        var missing = codes.Where(code => Copy.GetString(code, CultureInfo.InvariantCulture) is null).ToList();

        Assert.True(
            missing.Count == 0,
            "The Host can send these codes and ClientCopy.resx has no entry for them, so the refusal "
            + "page would render the code itself: " + string.Join(", ", missing));
    }
}
