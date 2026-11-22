using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of removing a Space default across every Project: the removal is one <c>POST …/remove</c> beside the
/// Space's other Status routes, declaring the capability the other Space writes declare and serving both surfaces; it is
/// the only route under the Space's Statuses that removes anything, and its request carries the Space-wide destination and
/// the per-Project exceptions; the blocked-Project refusal has a stable type; and the problem body
/// carries the occupancy report additively.
/// </summary>
/// <remarks>
/// <para>
/// <b>The contract types are found by name in the Contracts assembly</b>, so a rename fails here with the name it lost
/// rather than as a compile error in an unrelated gate.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string SpaceStatusRemovePattern = SpaceStatusPattern + "/remove";

    private static readonly string[] SpaceStatusResetWords = ["Restore", "Reset", "Inherit", "Override", "Mode"];

    /// <summary>
    /// The Space-level removal is <c>POST …/statuses/{statusId}/remove</c>, requiring <c>ConfigureSpaceDefaultStatuses</c> as
    /// the other Space writes do, not Session-only, consulting no bound.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-4")]
    public void The_Space_removal_route_declares_its_capability_serves_both_surfaces_and_consults_no_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText == SpaceStatusRemovePattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToList();

        Assert.Equal([$"POST {SpaceStatusRemovePattern}"], routes.Select(route => route.Key));

        var removal = routes[0].Endpoint;

        Assert.Equal(Capability.ConfigureSpaceDefaultStatuses, removal.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.DoesNotContain(removal.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint");
        Assert.Null(removal.Metadata.GetMetadata<RequiredBound>());
    }

    /// <summary>
    /// No endpoint removes a Space default without the mapping it requires: under the Space's Statuses the only route naming
    /// a removal is <c>POST …/remove</c>, none answers <c>DELETE</c>; its request is exactly <c>{ DestinationStatusId,
    /// Exceptions }</c> - a <c>Guid?</c> and a nullable list of <c>{ DestinationStatusId, ProjectId }</c>, both <c>Guid</c>;
    /// and no other Space Status request carries a member naming a removal, a destination, a reset or a mode.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public void A_default_leaves_the_Space_only_through_the_remove_route_whose_request_carries_its_destinations()
    {
        var underStatuses = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(SpaceStatusesPattern, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Method: method, Pattern: endpoint.RoutePattern.RawText ?? string.Empty)))
            .ToList();

        Assert.Equal(
            [$"POST {SpaceStatusRemovePattern}"],
            underStatuses.Where(route => route.Pattern.Contains("remove", StringComparison.OrdinalIgnoreCase)).Select(route => $"{route.Method} {route.Pattern}"));
        Assert.DoesNotContain(underStatuses, route => route.Method == "DELETE");
        Assert.DoesNotContain(underStatuses, route => Array.Exists(SpaceStatusResetWords, word => route.Pattern.Contains(word, StringComparison.OrdinalIgnoreCase)));

        var request = SpacesContract("RemoveSpaceStatusRequest");
        var exception = SpacesContract("SpaceStatusDestinationRequest");

        Assert.Equal(["DestinationStatusId", "Exceptions"], PropertyNames(request));
        Assert.Equal(typeof(Guid?), request.GetProperty("DestinationStatusId")?.PropertyType);

        var exceptions = request.GetProperty("Exceptions");

        Assert.NotNull(exceptions);
        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(exceptions).ReadState);
        Assert.Equal(exception, ElementOf(exceptions.PropertyType));
        Assert.Equal(["DestinationStatusId", "ProjectId"], PropertyNames(exception));
        Assert.Equal(typeof(Guid), exception.GetProperty("DestinationStatusId")?.PropertyType);
        Assert.Equal(typeof(Guid), exception.GetProperty("ProjectId")?.PropertyType);

        var others = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == "Yello.Contracts.Spaces" && type.Name.Contains("SpaceStatus", StringComparison.Ordinal) && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .Where(type => type != request && type != exception)
            .ToList();

        Assert.NotEmpty(others);
        Assert.All(others, other => Assert.DoesNotContain(
            other.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => Array.Exists(SpaceStatusForbiddenWords, word => property.Name.Contains(word, StringComparison.Ordinal))));
    }

    /// <summary>
    /// The refusal naming every blocked Project without a destination has a stable type,
    /// <c>urn:yello:problem:status-project-destination-required</c>, and the refusals the removal reuses keep theirs.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-27")]
    public void The_blocked_Project_refusal_has_a_stable_problem_type()
    {
        var type = typeof(Yello.Contracts.ProblemTypes).GetProperty("StatusProjectDestinationRequired", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(type);
        Assert.Equal("urn:yello:problem:status-project-destination-required", type.GetValue(null));
        Assert.Equal("urn:yello:problem:status-destination-required", Yello.Contracts.ProblemTypes.StatusDestinationRequired);
        Assert.Equal("urn:yello:problem:invalid-status-destination", Yello.Contracts.ProblemTypes.InvalidStatusDestination);
        Assert.Equal("urn:yello:problem:status-set-empty", Yello.Contracts.ProblemTypes.StatusSetEmpty);
    }

    /// <summary>
    /// The problem body gains exactly one nullable member after <c>Projects</c>, the occupancy report: each entry
    /// <c>{ Destinations, ProjectId, ProjectName, TaskCount }</c>, whose destinations are <c>{ Id, Name }</c> - identity only,
    /// nothing a Role or another Space could ride on.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "UX-DR16")]
    public void The_problem_body_appends_one_nullable_occupancy_report()
    {
        var parameters = typeof(Yello.Contracts.ProblemResponse).GetConstructors().Single().GetParameters();

        Assert.Equal(["Type", "Errors", "Capability", "Bound", "Limit", "Spaces", "Conflicts", "Projects", "Removal"], parameters.Select(parameter => parameter.Name));

        var removal = parameters[^1];

        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(removal).ReadState);

        var entry = ElementOf(removal.ParameterType);

        Assert.Equal(["Destinations", "ProjectId", "ProjectName", "TaskCount"], PropertyNames(entry));
        Assert.Equal(["Id", "Name"], PropertyNames(ElementOf(entry.GetProperty("Destinations")!.PropertyType)));
    }

    private static Type SpacesContract(string name)
    {
        var type = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Spaces." + name);

        Assert.True(type is not null, $"Yello.Contracts.Spaces.{name} does not exist.");

        return type;
    }
}
