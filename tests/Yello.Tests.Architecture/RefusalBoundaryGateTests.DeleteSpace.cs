using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.3's structural gates for deleting a Space: the client offers deletion exactly where the matrix permits
/// <c>DeleteSpace</c> (AC1, UX-DR27); the deletion and its counts read both declare <c>DeleteSpace</c> (AC9, Q2 (a));
/// the deletion is the one route whose effect destroys its own idempotency scope, and it says so with a marker (Q1 (a));
/// and the only bulk writes of a <c>Space</c> are the rename and the deletion of the resolved Space, both in
/// <c>SpaceStore</c> (AC10, the AD-24 amendment of 2026-10-31). Written by the story 3.3 ATDD run, 2026-10-31.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds and activated by the story 3.3 dev phase.</b> Product types are found by name or by
/// metadata: <c>PermitsDeleteSpace</c> through <see cref="ClientRolePredicate"/>, the counts read by
/// <c>SpaceRoutes.DeletionSummary</c>, and Q1's marker as any endpoint metadata whose type name contains <c>Destroys</c>
/// (it is <c>DestroysItsSpace</c>).
/// </para>
/// <para>
/// <b>Gates left green and unchanged by this run</b>, which the dev phase keeps green: <c>The_clients_Space_settings_predicate_is_the_union_of_its_sections</c>
/// (an Owner-only deletion section adds no Role to the union, so it passes before and after - widen its expected union
/// to name <c>DeleteSpace</c> when the section lands), <c>The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space</c>
/// (it names callers, not methods, so a deletion inside <c>SpaceStore</c> passes it; amend its message to cite the
/// 2026-10-31 amendment), and both both-keys gates in <c>SpaceSwitcherGateTests</c> (Q1 (a) sets the <c>SpaceId</c> key
/// alone).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private static string DeletionSummaryPattern => Yello.Contracts.Spaces.SpaceRoutes.DeletionSummary;

    /// <summary>
    /// AC1, UX-DR27, Task 6 - the client's predicate for offering deletion agrees with the capability matrix for every
    /// Role: Owner only (<c>DeleteSpace</c> is <c>[Yes, No, No, No]</c>). An Admin has no affordance at all.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-7")]
    public void The_clients_delete_Space_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("DeleteSpace");

        Assert.Equal([Role.Owner], Enum.GetValues<Role>().Where(role => RoleCapabilityMatrix.Decide(role, Capability.DeleteSpace) != CapabilityDecision.Denied));

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.DeleteSpace) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's delete-Space predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// AC9, FR-7, Q2 (a), Task 4 - <c>DELETE</c> on the active Space's route declares exactly <c>DeleteSpace</c>, and so
    /// does the counts read (<c>GET</c>), so the pipeline - not the endpoint - refuses an Admin, a Member and a Viewer.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-11")]
    public void The_deletion_and_its_counts_read_declare_DeleteSpace()
    {
        var endpoints = RouteTable();

        var deletion = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == Yello.Contracts.Spaces.SpaceRoutes.Active
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true);
        var summary = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == DeletionSummaryPattern
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);

        Assert.Equal([Capability.DeleteSpace], deletion.Metadata.OfType<RequiredCapability>().Select(required => required.Capability));
        Assert.Equal([Capability.DeleteSpace], summary.Metadata.OfType<RequiredCapability>().Select(required => required.Capability));
    }

    /// <summary>
    /// AC9, AD-18, Q1 (a), Task 4 - exactly one endpoint carries the marker that its effect destroys its own idempotency
    /// scope (so the pipeline validates and looks up its key but records nothing): the Space deletion. A marker on any other
    /// route would silently drop that route's replay protection. Plant: remove the marker, and this turns red; put it on
    /// the rename, and this turns red.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    public void Only_the_deletion_route_carries_the_marker_that_it_destroys_its_own_Space()
    {
        var marked = RouteTable()
            .Where(endpoint => endpoint.Metadata.Any(metadata => metadata.GetType().Name.Contains("Destroys", StringComparison.Ordinal)))
            .Select(endpoint => $"{string.Join(',', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}")
            .ToList();

        Assert.Equal([$"DELETE {Yello.Contracts.Spaces.SpaceRoutes.Active}"], marked);
    }

    /// <summary>
    /// AC10, AD-24 (amendment of 2026-10-31), Task 3 - <c>Space</c> carries no row-level security policy, so its bulk writes
    /// are named: <c>SpaceStore</c> alone calls <c>ExecuteUpdateAsync</c> (the rename) and <c>ExecuteDeleteAsync</c> (the
    /// deletion) on <c>Space</c>, and nothing else bulk-writes it. Read from the compiled IL, as
    /// <see cref="The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space"/> is, but by method as well as caller.
    /// Plant: an <c>ExecuteDeleteAsync</c> on <c>Spaces</c> in another type turns this and the 3.1 gate red.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "NFR-1")]
    public void The_only_bulk_writes_of_a_Space_are_the_rename_and_the_deletion_in_SpaceStore()
    {
        const string space = "Yello.Domain.Spaces.Space";
        string[] bulk = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

        var writes = new List<string>();

        foreach (var assembly in ProductionAssemblies.All.Where(assembly => assembly != ProductionAssemblies.AppHost))
        {
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly.Location);

            writes.AddRange(module.GetTypes()
                .SelectMany(type => type.Methods
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Select(instruction => instruction.Operand)
                    .OfType<Mono.Cecil.GenericInstanceMethod>()
                    .Where(called => bulk.Contains(called.Name, StringComparer.Ordinal)
                        && called.GenericArguments.Any(argument => argument.FullName == space))
                    .Select(called => $"{OutermostName(type)}.{called.Name}")));
        }

        Assert.Equal(
            ["Yello.Infrastructure.Persistence.SpaceStore.ExecuteDeleteAsync", "Yello.Infrastructure.Persistence.SpaceStore.ExecuteUpdateAsync"],
            writes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }
}
