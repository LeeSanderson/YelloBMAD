using System.Collections;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for removing a Membership and leaving a Space: the Membership routes and the one capability each
/// declares, the leave route's identity rule kept to one write, the removal authority's transcription of the matrix, the
/// single-row read's contract, the Owner's problem type and its copy, and the client's Remove and Leave controls.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each gate finds its subject by name or by shape</b> (route pattern, reflection, the resource file, the Razor file), so
/// a subject built under another name turns it red. The names are: a public static method on
/// <c>RoleCapabilityMatrix</c> taking a <see cref="Role"/> whose answer has <c>Removable</c> and <c>HeldBy</c>;
/// <c>MembershipCapabilities.Removable(viewerRole, targetRole, isYou)</c> in the client; <c>LeaveSpaceSection.razor</c>. A
/// rename must be made here too.
/// </para>
/// <para>
/// <b>Existing gates this work must keep green, unchanged:</b> <c>Only_the_pipeline_sets_the_acting_authority</c> (the
/// removal set's writer is the pipeline),
/// <c>A_Qualified_cell_reaches_no_endpoint_without_its_target_rule</c> (it now finds <c>RemoveMembership</c>, so the
/// removal slice's name must contain it), both Role-read gates, <c>Only_the_deletion_route_carries_the_marker_that_it_destroys_its_own_Space</c>
/// (the leave route is not marked), and <c>No_production_code_introduces_a_cache</c>.
/// <see cref="The_Membership_routes_declare_one_capability_each_and_serve_both_surfaces"/> replaced the gate that held every
/// Membership route to <c>ChangeMembershipRole</c>.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string OwnMembershipPattern = "/api/v1/spaces/{spaceId:guid}/membership";

    /// <summary>
    /// The Membership routes are exactly these four, each with its one capability, none Session-only, none consulting a
    /// bound: the list and the single-row read under <c>ChangeMembershipRole</c> (both carry an address), the Role change
    /// under <c>ChangeMembershipRole</c>, the removal under <c>RemoveMembership</c>, and the leave under
    /// <c>ReadProjectsAndTasks</c>, because leaving is decided by the caller's identity and every Role may do it. A Token
    /// reaches every one of them.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-23")]
    public void The_Membership_routes_declare_one_capability_each_and_serve_both_surfaces()
    {
        var expected = new Dictionary<string, Capability>(StringComparer.Ordinal)
        {
            [$"GET {MembershipsPattern}"] = Capability.ChangeMembershipRole,
            [$"GET {MembershipPattern}"] = Capability.ChangeMembershipRole,
            [$"PATCH {MembershipPattern}"] = Capability.ChangeMembershipRole,
            [$"DELETE {MembershipPattern}"] = Capability.RemoveMembership,
            [$"DELETE {OwnMembershipPattern}"] = Capability.ReadProjectsAndTasks,
        };
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is MembershipsPattern or MembershipPattern or OwnMembershipPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), routes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetValues<Capability>().Length, Enum.GetNames<Capability>().Distinct(StringComparer.Ordinal).Count());

        foreach (var (key, endpoint) in routes)
        {
            Assert.True(
                endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability == expected[key],
                $"{key} declares {endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability}; expected {expected[key]}.");
            Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint");
            Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>());
        }
    }

    /// <summary>
    /// The removal authority transcribes the matrix's "Remove a Membership" cells and its boundaries: the Owner may remove
    /// Admins, Members and Viewers and must still hold Owner; an Admin may remove Members and Viewers ("Admins cannot modify
    /// each other") and must still hold Owner or Admin; a Member and a Viewer may remove no one. <b>Owner is in no
    /// <c>Removable</c> set for any actor.</b> Found by shape: a public static method on <c>RoleCapabilityMatrix</c> taking a
    /// <see cref="Role"/> whose answer has <c>Removable</c> and <c>HeldBy</c> collections of <see cref="Role"/>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AR-12")]
    [Trait("Assumption", "PRD-12-4")]
    public void The_removal_rules_agree_with_the_matrix_transcription()
    {
        (Role Actor, Role[] Removable, Role[] HeldBy)[] transcription =
        [
            (Role.Owner, [Role.Admin, Role.Member, Role.Viewer], [Role.Owner]),
            (Role.Admin, [Role.Member, Role.Viewer], [Role.Owner, Role.Admin]),
            (Role.Member, [], []),
            (Role.Viewer, [], []),
        ];

        foreach (var (actor, removable, heldBy) in transcription)
        {
            var (declaredRemovable, declaredHeldBy) = RemovalRuleOf(actor);

            Assert.True(removable.Order().SequenceEqual(declaredRemovable.Order()), $"{actor} may remove [{string.Join(", ", declaredRemovable)}]; the transcription says [{string.Join(", ", removable)}].");
            Assert.True(heldBy.Order().SequenceEqual(declaredHeldBy.Order()), $"{actor} must still hold one of [{string.Join(", ", declaredHeldBy)}]; the transcription says [{string.Join(", ", heldBy)}].");
            Assert.DoesNotContain(Role.Owner, declaredRemovable);
        }

        Assert.Equal(CapabilityDecision.Qualified, RoleCapabilityMatrix.Decide(Role.Admin, Capability.RemoveMembership));
    }

    /// <summary>
    /// The client's Remove control, <c>MembershipCapabilities.Removable(viewerRole, targetRole, isYou)</c>, agrees with the
    /// removal rules for every (viewer, target) pair, and the caller's own row never carries Remove (it leaves through the
    /// Leave control): an Admin sees Remove on Member and Viewer rows only, the Owner on every row but its own, a Member and
    /// a Viewer on none. The Owner's row carries no Remove for anyone.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-14")]
    [Trait("Assumption", "PRD-12-4")]
    public void The_clients_Remove_control_agrees_with_the_removal_rules_for_every_pair()
    {
        var removable = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name == "Removable" && method.GetParameters().Length == 3);

        Assert.True(removable is not null, "No static Removable(viewerRole, targetRole, isYou) in Yello.Client.");

        // Every (viewer, target) pair as another's row, and each Role's own row.
        var rows = Enum.GetValues<Role>()
            .SelectMany(viewer => Enum.GetValues<Role>().Select(target => (Viewer: viewer, Target: target, IsYou: false)))
            .Concat(Enum.GetValues<Role>().Select(role => (Viewer: role, Target: role, IsYou: true)));

        foreach (var (viewer, target, isYou) in rows)
        {
            var expected = !isYou && (viewer, target) is (Role.Owner, not Role.Owner) or (Role.Admin, Role.Member or Role.Viewer);
            var shown = removable.Invoke(null, [StableCode.Of(viewer), StableCode.Of(target), isYou]) is true;

            Assert.True(shown == expected, $"{StableCode.Of(viewer)} viewing a {StableCode.Of(target)} row (isYou: {isYou}) shows Remove: {shown}; the removal rules say {expected}.");
        }
    }

    /// <summary>
    /// The list row and the single-row read carry <c>InSpaceNow</c> (a bool) beside the five members they had, and nothing
    /// else; the Role change's answer stays exactly its four members, with no address and no presence.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "AD-23")]
    public void The_Membership_row_carries_InSpaceNow_and_the_Role_change_answer_does_not()
    {
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.MembershipResponse");
        var changed = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.MembershipRoleChangeResponse");

        Assert.True(response is not null && changed is not null, "MembershipResponse or MembershipRoleChangeResponse is not declared in Yello.Contracts.Memberships.");
        Assert.Equal(["DisplayName", "EmailAddress", "Id", "InSpaceNow", "IsYou", "Role"], PropertyNames(response));
        Assert.Equal(typeof(bool), response.GetProperty("InSpaceNow")?.PropertyType);
        Assert.Equal(["DisplayName", "Id", "IsYou", "Role"], PropertyNames(changed));
    }

    /// <summary>
    /// <c>ownership-held</c> is declared in <c>ProblemTypes</c> and has client copy of its own in <c>ClientCopy.resx</c>, with
    /// a comment, saying the Owner leaves by an accepted Ownership Offer or by deleting the Space - no third exit. The Remove
    /// and Leave copy the client shows have entries too.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-14")]
    public void The_ownership_held_problem_type_is_declared_and_has_client_copy()
    {
        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null) as string)
            .ToHashSet(StringComparer.Ordinal);

        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => ((string?)data.Element("value") ?? string.Empty, (string?)data.Element("comment") ?? string.Empty), StringComparer.Ordinal);

        Assert.Contains("urn:yello:problem:ownership-held", declared);
        Assert.True(copy.TryGetValue("ownership-held", out var entry), "ClientCopy.resx has no entry for ownership-held.");
        Assert.Equal("The Owner leaves by having an Ownership Offer accepted, or by deleting the Space.", entry.Item1);
        Assert.False(string.IsNullOrWhiteSpace(entry.Item2), "The ownership-held entry carries no comment.");
        Assert.Contains(copy.Values, value => value.Item1 == "Admins can remove Members and Viewers only.");
        Assert.Contains(copy.Values, value => value.Item1 == "You left the Space.");
    }

    /// <summary>
    /// The Remove and Leave controls an Account lacks are absent, never disabled: neither the Membership row and section nor
    /// the Leave section carries a <c>disabled</c> or <c>aria-disabled</c> attribute.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public void No_Remove_or_Leave_control_is_disabled()
    {
        foreach (var name in new[] { "MembershipSection.razor", "MembershipRow.razor", "LeaveSpaceSection.razor" })
        {
            var file = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Components", name));

            Assert.True(file.Exists, $"{name} does not exist in Yello.Client/Components.");
            Assert.False(DisabledAttribute.IsMatch(File.ReadAllText(file.FullName)), $"{name} carries a disabled attribute; a missing capability is absent, never disabled (UX-DR27).");
        }
    }

    /// <summary>
    /// The Role sets <c>RoleCapabilityMatrix</c> declares for removal by <paramref name="actor"/>, found by shape.
    /// </summary>
    private static (IReadOnlyList<Role> Removable, IReadOnlyList<Role> HeldBy) RemovalRuleOf(Role actor)
    {
        var method = typeof(RoleCapabilityMatrix)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(candidate => candidate.GetParameters() is [{ ParameterType: var only }] && only == typeof(Role)
                && candidate.ReturnType.GetProperty("Removable") is not null && candidate.ReturnType.GetProperty("HeldBy") is not null);

        Assert.True(method is not null, "RoleCapabilityMatrix declares no public static method taking a Role whose answer has Removable and HeldBy.");

        var rule = method.Invoke(null, [actor]);

        IReadOnlyList<Role> Read(string name) =>
            rule is null
                ? []
                : [.. Assert.IsAssignableFrom<IEnumerable>(method.ReturnType.GetProperty(name)!.GetValue(rule) ?? Array.Empty<Role>()).Cast<Role>()];

        return (Read("Removable"), Read("HeldBy"));
    }
}
