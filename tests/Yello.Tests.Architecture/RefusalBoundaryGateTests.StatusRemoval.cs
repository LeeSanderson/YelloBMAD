using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of removing a Status from a Project: the remove and add-back routes declare the capability the
/// matrix gives them and serve both surfaces; a Status leaves a Project through the remove route alone, whose request
/// carries its destination; the two new refusals have stable types; the delta kind names <c>Remove</c> and every kind fits
/// its column; and the delta's order has a unique backstop.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are named as strings</b> - the remove request, the two problem types, the <c>Remove</c> kind - because none
/// exists yet, so these compile today and fail for the stated reason when activated. The dev phase swaps each for a typed
/// reference in the change that adds it.
/// </para>
/// <para>
/// <b>Active gates that go red by design when the routes land</b>, amended deliberately in that change:
/// <c>The_Project_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound</c> (it lists exactly four
/// routes), ARCH-503 <c>The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board</c> (widened
/// by exactly the two routes below), and <c>No_Project_request_contract_carries_anything_but_a_name</c> (its list of request
/// names gains the remove request).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string ProjectStatusRemovePattern = ProjectStatusPattern + "/remove";

    private const string ProjectStatusRestorePattern = ProjectStatusPattern + "/restore";

    private const string RemoveRequestName = "Yello.Contracts.Projects.RemoveProjectStatusRequest";

    private static readonly string[] RemovalWords = ["Remove", "Removed", "Destination", "Delete"];

    /// <summary>
    /// The removal and the add-back are <c>POST …/statuses/{statusId}/remove</c> and <c>POST …/statuses/{statusId}/restore</c>,
    /// each requiring <c>ConfigureProjectStatusDelta</c> (no new capability), neither Session-only, neither consulting a bound.
    /// </summary>
    [Fact(Skip = "red: AC11, AC12 - the remove and restore routes are not mapped (Q2)")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "FR-26")]
    public void The_remove_and_restore_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is ProjectStatusRemovePattern or ProjectStatusRestorePattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal([$"POST {ProjectStatusRemovePattern}", $"POST {ProjectStatusRestorePattern}"], routes.Keys.Order(StringComparer.Ordinal));
        Assert.All(routes.Values, endpoint => Assert.Equal(Capability.ConfigureProjectStatusDelta, endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability));
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// No endpoint accepts a removal without the mapping it requires: under a Project, the only route that names a removal is
    /// <c>POST …/remove</c>, no Project Status route answers <c>DELETE</c>, the remove request is exactly
    /// <c>{ DestinationStatusId }</c> typed <c>Guid?</c>, and no other Project request carries a member that could remove a
    /// Status or name a destination.
    /// </summary>
    [Fact(Skip = "red: AC4 - the remove route and RemoveProjectStatusRequest do not exist (AR-24, Q2)")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public void A_Status_leaves_a_Project_only_through_the_remove_route_whose_request_carries_its_destination()
    {
        var underProject = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(ProjectPattern, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Method: method, Pattern: endpoint.RoutePattern.RawText ?? string.Empty)))
            .ToList();

        Assert.Equal(
            [$"POST {ProjectStatusRemovePattern}"],
            underProject.Where(route => route.Pattern.Contains("remove", StringComparison.OrdinalIgnoreCase)).Select(route => $"{route.Method} {route.Pattern}"));
        Assert.DoesNotContain(underProject, route => route.Method == "DELETE" && route.Pattern.StartsWith(ProjectStatusesPattern, StringComparison.Ordinal));

        var request = ProductionAssemblies.Contracts.GetType(RemoveRequestName);

        Assert.True(request is not null, $"No {RemoveRequestName} in Yello.Contracts.");
        Assert.Equal(["DestinationStatusId"], PropertyNames(request));
        Assert.Equal(typeof(Guid?), request.GetProperty("DestinationStatusId")?.PropertyType);

        var others = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == typeof(Yello.Contracts.Projects.ProjectRoutes).Namespace
                && type.Name.EndsWith("Request", StringComparison.Ordinal)
                && type.FullName != RemoveRequestName)
            .ToList();

        Assert.NotEmpty(others);
        Assert.All(others, other => Assert.DoesNotContain(
            other.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => Array.Exists(RemovalWords, word => property.Name.Contains(word, StringComparison.Ordinal))));
    }

    /// <summary>
    /// The two refusals a removal adds have stable <c>urn:yello:problem:*</c> types on <c>ProblemTypes</c>:
    /// <c>status-destination-required</c> and <c>invalid-status-destination</c>.
    /// </summary>
    [Fact(Skip = "red: AC1, AC2 - ProblemTypes has neither removal refusal (Q4)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-26")]
    public void The_removal_refusals_have_stable_problem_types()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["StatusDestinationRequired"] = "urn:yello:problem:status-destination-required",
            ["InvalidStatusDestination"] = "urn:yello:problem:invalid-status-destination",
        };

        foreach (var (member, type) in expected)
        {
            var property = typeof(Yello.Contracts.ProblemTypes).GetProperty(member, BindingFlags.Public | BindingFlags.Static);

            Assert.True(property is not null, $"ProblemTypes has no {member}.");
            Assert.Equal(type, property.GetValue(null));
        }
    }

    /// <summary>
    /// The delta's kinds are exactly <c>Add</c>, <c>Rename</c>, <c>Place</c> and <c>Remove</c>: a removal of a Space default is
    /// an operation keyed on the Status's id, never a materialised list.
    /// </summary>
    [Fact(Skip = "red: AC7 - StatusDeltaKind has no Remove (Q1, AD-16)")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "FR-26")]
    public void The_delta_kinds_are_add_rename_place_and_remove()
    {
        Assert.Equal(
            ["Add", "Place", "Remove", "Rename"],
            Enum.GetNames<Yello.Domain.Statuses.StatusDeltaKind>().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A standing gate: every delta kind's name fits the <c>Kind</c> column the model declares, so a kind added to the enum
    /// is never truncated or refused by the column it is stored in as text.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "FR-26")]
    public void Every_delta_kind_fits_the_column_it_is_stored_in()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var kind = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(Yello.Domain.Statuses.StatusDeltaOperation))?
            .FindProperty(nameof(Yello.Domain.Statuses.StatusDeltaOperation.Kind));

        Assert.NotNull(kind);
        Assert.True(kind.GetMaxLength() is > 0, "The Kind column declares no maximum length.");
        Assert.All(
            Enum.GetNames<Yello.Domain.Statuses.StatusDeltaKind>(),
            name => Assert.True(name.Length <= kind.GetMaxLength(), $"{name} is longer than the Kind column's {kind.GetMaxLength()} characters."));
    }

    /// <summary>
    /// The delta's order has a database backstop: the model declares a unique index on <c>(SpaceId, ProjectId, Sequence)</c>
    /// on <c>StatusDeltaOperation</c>, so a writer that skipped the Project's lock cannot tie two operations silently.
    /// </summary>
    [Fact(Skip = "red: Task 2 - no unique (SpaceId, ProjectId, Sequence) index yet (ledger :1032, Q9)")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AD-17")]
    public void The_delta_sequence_is_unique_per_Project_in_the_model()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Yello.Domain.Statuses.StatusDeltaOperation));

        Assert.NotNull(entity);
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["SpaceId", "ProjectId", "Sequence"]));
    }
}
